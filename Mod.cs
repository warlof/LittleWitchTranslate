using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

namespace LittleWitchTranslate
{
    public class Mod : ModManagerGUI.IMod
    {
        public override bool Verify(string directory)
        {
            if (directory == null)
                return false;

            for (var i = 0; i < ModManagerGUI.ModManager.Configuration.FileNames.Length; i++)
            {
                var name = ModManagerGUI.ModManager.Configuration.FileNames[i];
#if MACOS
                var managedPath = Path.Combine(directory, "LWIW.app", "Contents", "Resources", "Data", "Managed");
#else
                var managedPath = Path.Combine(directory, name + "_Data", "Managed");
#endif
                if (File.Exists(Path.Combine(managedPath, "Assembly-CSharp.dll")) &&
                    File.Exists(Path.Combine(managedPath, "Unity.TextMeshPro.dll")) &&
                    File.Exists(Path.Combine(managedPath, "DialogueSystem.dll")) &&
                    File.Exists(Path.Combine(managedPath, "UnityEngine.CoreModule.dll")))
                    return true;
            }
            return false;
        }

        public string GetGameName(string directory)
        {
            if (directory == null)
                return null;
            for (var i = 0; i < ModManagerGUI.ModManager.Configuration.FileNames.Length; i++)
            {
                var name = ModManagerGUI.ModManager.Configuration.FileNames[i];
#if MACOS
                var managedPath = Path.Combine(directory, "LWIW.app", "Contents", "Resources", "Data", "Managed");
#else
                var managedPath = Path.Combine(directory, name + "_Data", "Managed");
#endif
                if (File.Exists(Path.Combine(managedPath, "Assembly-CSharp.dll")) &&
                    File.Exists(Path.Combine(managedPath, "Unity.TextMeshPro.dll")) &&
                    File.Exists(Path.Combine(managedPath, "DialogueSystem.dll")) &&
                    File.Exists(Path.Combine(managedPath, "UnityEngine.CoreModule.dll")))
                    return name;
            }
            return null;
        }

        private void WriteLog(string log)
        {
            if (OnLog != null)
                OnLog(log);
        }
        public override void Apply(string gameDirectory, HashSet<int> options)
        {
            var gameName = GetGameName(gameDirectory);
            WriteLog("Game name is: " + gameName);
            WriteLog("Copying files if needed...");
#if MACOS
            var managedPath = Path.Combine(gameDirectory, "LWIW.app", "Contents", "Resources", "Data", "Managed");
#else
            var managedPath = Path.Combine(gameDirectory, gameName + "_Data", "Managed");
#endif
            bool isModded = CheckIfModded(gameDirectory, gameName);
            CopyFiles(gameDirectory, isModded, gameName, options);
            var translatorBackupPath = Path.Combine(gameDirectory, "TranslatorBackup");

            WriteLog("Reading assemblies and fetching types and enums...");

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.Combine(managedPath));

            WriteLog("Fetching methods from TranslatorPlugin.dll");
            MethodDefinition translateStringMethod = null;
            MethodDefinition changeTextMeshMethod = null;
            MethodDefinition changeFontMethod = null;
            MethodDefinition getTextMethod = null;
            MethodDefinition textAssetConstructor = null;
            MethodDefinition initializeMethod = null;
            MethodDefinition updateTimeMethod = null;
            
            var translatorAssembly = AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(managedPath, "TranslatorPlugin.dll"));
            var translatorType = translatorAssembly.MainModule.GetType("TranslatorPlugin.Translator");

            foreach (var method in translatorType.Methods)
            {
                if (method.Name == "TranslateString")
                    translateStringMethod = method;
                if (method.Name == "ChangeTextMesh")
                    changeTextMeshMethod = method;
                if (method.Name == "ChangeFont")
                    changeFontMethod = method;
                if (method.Name == "Initialize")
                    initializeMethod = method;
                if (method.Name == "UpdateTime")
                    updateTimeMethod = method;
            }

            var inventoryType = translatorAssembly.MainModule.GetType("TranslatorPlugin.Inventory");
            MethodDefinition removeItemFromStorages = null;
            MethodDefinition getItemCountFromStorages = null;
            MethodDefinition createCraftingInventory = null;
            MethodDefinition getPossessedMainIngredientCount = null;
            MethodDefinition addAllInventoriesToGroup = null;
            MethodDefinition hasItemListMethod = null;
            foreach (var method in inventoryType.Methods)
            {
                if (method.Name == "GetItemCountFromStorage")
                    getItemCountFromStorages = method;
                if (method.Name == "RemoveItemsFromStorage")
                    removeItemFromStorages = method;
                if (method.Name == "CreateCraftingInventory")
                    createCraftingInventory = method;
                if (method.Name == "GetPossessedMainIngredientCount")
                    getPossessedMainIngredientCount = method;
                if (method.Name == "AddAllInventoriesToGroup")
                    addAllInventoriesToGroup = method;
                if (method.Name == "HasItemList")
                    hasItemListMethod = method;
            }

            WriteLog("Fetching methods from UnityEngine.CoreModule.dll");

            var unityEngineCoreModule = System.IO.Path.Combine(translatorBackupPath, "UnityEngine.CoreModule.dll");
            var coreModuleAssembly = AssemblyDefinition.ReadAssembly(unityEngineCoreModule, new ReaderParameters() { AssemblyResolver = resolver });
            MethodDefinition objectSetName = null;
            MethodDefinition objectGetName = null;
            var objectClass = coreModuleAssembly.MainModule.GetType("UnityEngine.Object");
            foreach (var p in objectClass.Properties)
            {
                if (p.Name == "name")
                {
                    objectSetName = p.SetMethod;
                    objectGetName = p.GetMethod;
                }
            }
            var textAssetClass = coreModuleAssembly.MainModule.GetType("UnityEngine.TextAsset");

            foreach (var p in textAssetClass.Properties)
            {
                if (p.Name == "text")
                    getTextMethod = p.GetMethod;
            }
            foreach (var m in textAssetClass.Methods)
            {
                if (m.Name == ".ctor" && m.Parameters.Count == 1)
                    textAssetConstructor = m;
            }

            if (translateStringMethod == null || changeTextMeshMethod == null || getTextMethod == null || textAssetConstructor == null)
            {
                WriteLog("Couldn't find necessary methods. Can't proceed :(");
                return;
            }

            WriteLog("Patching Unity.TextMeshPro.dll");
            var textmeshPro = System.IO.Path.Combine(translatorBackupPath, "Unity.TextMeshPro.dll");
            var textMeshProAssembly = AssemblyDefinition.ReadAssembly(textmeshPro, new ReaderParameters() { AssemblyResolver = resolver });
            var textMeshProModule = textMeshProAssembly.MainModule;
            var tmpText = textMeshProModule.GetType("TMPro.TMP_Text");
            PropertyDefinition textProperty = null;
            PropertyDefinition fontProperty = null;
            foreach (var property in tmpText.Properties)
            {
                if (property.Name == "text")
                    textProperty = property;
                if (property.Name == "font")
                    fontProperty = property;
            }
            var getStringRef = textMeshProModule.ImportReference(translateStringMethod);
            var changeTextRef = textMeshProModule.ImportReference(changeTextMeshMethod);
            var changeFontRef = textMeshProModule.ImportReference(changeFontMethod);

            if (textProperty != null && translateStringMethod != null)
            {
                var mRef = textMeshProModule.ImportReference(translateStringMethod);
                var setMethod = textProperty.SetMethod;
                var getMethod = textProperty.GetMethod;
                var body = setMethod.Body;
                var processor = body.GetILProcessor();
                var firstInstruction = body.Instructions[0];
                if (firstInstruction.OpCode.Code == Code.Ldarg_0)
                {
                    WriteLog("Patching TMPro.TMP_Text.text setter...");

                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_0));
                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Call, changeTextRef));

                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_1));
                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldstr, setMethod.FullName));
                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Call, mRef));
                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Starg_S, setMethod.Parameters[0]));
                }
            }
            if (fontProperty != null)
            {
                var mRef = textMeshProModule.ImportReference(translateStringMethod);
                var setMethod = fontProperty.SetMethod;
                var body = setMethod.Body;
                var processor = body.GetILProcessor();
                var firstInstruction = body.Instructions[0];
                if (firstInstruction.OpCode.Code == Code.Ldarg_0)
                {
                    WriteLog("Patching TMPro.TMP_Text.font setter...");

                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_1));
                    processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Call, changeFontRef));
                }
            }

            var textMeshProGUI = textMeshProModule.GetType("TMPro.TextMeshProUGUI");
            MethodDefinition startMethod = null;
            foreach (var method in textMeshProGUI.Methods)
            {
                if (method.Name == "Start")
                    startMethod = method;
            }

            if (startMethod == null)
            {
                WriteLog("Adding start method to TMPro.TextMeshProUGUI...");
                startMethod = new MethodDefinition("Start", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, textMeshProModule.TypeSystem.Void);
                textMeshProGUI.Methods.Add(startMethod);
            }

            if (startMethod != null)
            {
                WriteLog("Filling method body of TMPro.TextMeshProUGUI.Start...");
                var setMethod = textProperty.SetMethod;
                var getMethod = textProperty.GetMethod;
                var body = startMethod.Body;
                body.Instructions.Clear();
                var processor = body.GetILProcessor();

                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, textMeshProModule.ImportReference(getMethod));
                processor.Emit(OpCodes.Ldstr, startMethod.FullName);
                processor.Emit(OpCodes.Call, getStringRef);
                processor.Emit(OpCodes.Call, textMeshProModule.ImportReference(setMethod));
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, changeTextRef);
                processor.Emit(OpCodes.Nop);
                processor.Emit(OpCodes.Ret);
            }

            WriteLog("Saving Unity.TextMeshPro.dll");
            textMeshProAssembly.Write(System.IO.Path.Combine(managedPath, "Unity.TextMeshPro.dll"));
            WriteLog("Assembly saved successfully!");
            var config = "";

            if (options.Contains(0) || options.Contains(1))
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(translatorBackupPath, "DialogueSystem.dll")))
                    System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "DialogueSystem.dll"), System.IO.Path.Combine(managedPath, "DialogueSystem.dll"), true);

                WriteLog("Patching SunnySideUp.HUD.Contents.dll");
                var hudContents = System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.HUD.Contents.dll");
                var hudContentsAssembly = AssemblyDefinition.ReadAssembly(hudContents, new ReaderParameters() { AssemblyResolver = resolver });
                var hudContentsModule = hudContentsAssembly.MainModule;
                var timeHUDController = hudContentsModule.GetType("SunnySideUp.TimeHUDController");

                foreach (var method in timeHUDController.Methods)
                {
                    if (method.Name == "RefreshView")
                    {
                        var body = method.Body;
                        body.Instructions.Clear();
                        var proc = body.GetILProcessor();
                        proc.Append(proc.Create(OpCodes.Ldarg_0));
                        proc.Append(proc.Create(OpCodes.Call, method.Module.ImportReference(updateTimeMethod)));
                        proc.Append(proc.Create(OpCodes.Ret));
                        body.Optimize();
                    }
                }
                WriteLog("Saving SunnySideUp.HUD.Contents.dll");
                hudContentsAssembly.Write(System.IO.Path.Combine(managedPath, "SunnySideUp.HUD.Contents.dll"));
                WriteLog("Assembly saved successfully!");
                config += "translate\r\n";
            }
            else
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(translatorBackupPath, "DialogueSystem.dll")))
                    System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "DialogueSystem.dll"), System.IO.Path.Combine(managedPath, "DialogueSystem.dll"), true);

                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.HUD.Contents.dll"), System.IO.Path.Combine(managedPath, "SunnySideUp.HUD.Contents.dll"), true);
            }
            if (options.Contains(2))
            {
                WriteLog("Applying storage crafting mod...");
                var items = System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.Item.dll");
                var itemsAssembly = AssemblyDefinition.ReadAssembly(items, new ReaderParameters() { AssemblyResolver = resolver });
                var itemsModule = itemsAssembly.MainModule;
                var inventoryUtility = itemsModule.GetType("SunnySideUp.InventoryUtility");
                MethodDefinition getItemCountMethod = null;
                foreach (var method in inventoryUtility.Methods)
                {
                    if (method.Name == "RemoveItem")
                    {
                        var body = method.Body;
                        var processor = body.GetILProcessor();
                        body.SimplifyMacros();
                        var lastInstruction = body.Instructions[body.Instructions.Count - 1];
                        processor.InsertBefore(lastInstruction, processor.Create(OpCodes.Ldloc_0));
                        processor.InsertBefore(lastInstruction, processor.Create(OpCodes.Ldarg_0));
                        processor.InsertBefore(lastInstruction, processor.Create(OpCodes.Ldarg_1));
                        processor.InsertBefore(lastInstruction, processor.Create(OpCodes.Ldarg_2));
                        processor.InsertBefore(lastInstruction, processor.Create(OpCodes.Call, method.Module.ImportReference(removeItemFromStorages)));
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                    if (method.Name == "GetItemCount")
                    {
                        if (method.Parameters[1].ParameterType.Name == "ItemDefinition")
                        {
                            getItemCountMethod = method;
                            var body = method.Body;
                            var processor = body.GetILProcessor();
                            body.SimplifyMacros();
                            for (var i = 0; i < body.Instructions.Count; i++)
                            {
                                var inst = body.Instructions[i];
                                if (inst.OpCode == OpCodes.Ldc_I4 && inst.Operand is int num && num == 0)
                                {
                                    processor.InsertBefore(inst, processor.Create(OpCodes.Ldarg_0));
                                    processor.InsertBefore(inst, processor.Create(OpCodes.Ldarg_1));
                                    processor.InsertBefore(inst, processor.Create(OpCodes.Ldarg_2));
                                    processor.InsertBefore(inst, processor.Create(OpCodes.Call, method.Module.ImportReference(getItemCountFromStorages)));
                                    processor.RemoveAt(i + 4);
                                    break;
                                }
                            }
                            body.OptimizeMacros();
                            body.Optimize();
                        }
                    }
                }
                itemsModule.Write(System.IO.Path.Combine(managedPath, "SunnySideUp.Item.dll"));

                var uiBrewing = System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.Brewing.dll");
                var uiBrewingAssembly = AssemblyDefinition.ReadAssembly(uiBrewing, new ReaderParameters() { AssemblyResolver = resolver });
                var uiBrewingModule = uiBrewingAssembly.MainModule;
                var viewModel = uiBrewingModule.GetType("SunnySideUp.UI.BrewingFlowUI.ViewModels.BrewingFlowPageUIViewModel");

                TypeReference itemCollectionReference = null;
                foreach (var nestedType in viewModel.NestedTypes)
                {
                    if (nestedType.Name == "<>c__DisplayClass22_0")
                    {
                        foreach (var subNestedType in nestedType.NestedTypes)
                        {
                            if (subNestedType.Name == "<<OpenIngredientSelectUI>g__DelayedRelayout|0>d")
                            {
                                foreach (var m in subNestedType.Methods)
                                {
                                    if (m.Name == "MoveNext")
                                    {
                                        var body = m.Body;
                                        var processor = body.GetILProcessor();
                                        for (var i = 0; i < body.Instructions.Count; i++)
                                        {
                                            var inst = body.Instructions[i];
                                            if (inst.OpCode == OpCodes.Callvirt && inst.Operand is MethodReference mref && mref.Name == "get_MainItemCollection")
                                            {
                                                itemCollectionReference = mref.ReturnType;
                                                processor.Remove(inst);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        foreach (var field in nestedType.Fields)
                        {
                            if (field.Name == "playerInventory")
                                field.FieldType = itemCollectionReference;
                        }
                    }
                }
                foreach (var method in viewModel.Methods)
                {
                    if (method.Name == "OpenIngredientSelectUI")
                    {
                        var body = method.Body;
                        var processor = body.GetILProcessor();
                        body.SimplifyMacros();
                        VariableDefinition inventoryVariable = null;
                        foreach (var variable in body.Variables)
                        {
                            if (variable.VariableType.Name == "Inventory")
                            {
                                variable.VariableType = itemCollectionReference;
                                inventoryVariable = variable;
                                break;
                            }
                        }

                        for (var i = 0; i < body.Instructions.Count; i++)
                        {
                            var inst = body.Instructions[i];
                            if (inst.OpCode == OpCodes.Call && inst.Operand is MethodReference mref && mref.Name == "get_PlayerInventory")
                            {
                                processor.InsertBefore(inst, processor.Create(OpCodes.Ldarg_0));
                                processor.InsertBefore(inst, processor.Create(OpCodes.Call, method.Module.ImportReference(createCraftingInventory)));
                                processor.Remove(inst);
                                break;
                            }
                        }
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                uiBrewingModule.Write(System.IO.Path.Combine(managedPath, "SunnySideUp.UI.Brewing.dll"));


                var uiManufacture = System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.Manufacture.dll");
                var uiManufactureAssembly = AssemblyDefinition.ReadAssembly(uiManufacture, new ReaderParameters() { AssemblyResolver = resolver });
                var uiManufactureModule = uiManufactureAssembly.MainModule;
                var manufactureModel = uiManufactureModule.GetType("SunnySideUp.UI.Manufacture.ManufactureModel");

                MethodDefinition getPossessed = null;
                foreach (var method in manufactureModel.Methods)
                {
                    if (method.Name == "GetPossessedMainIngredientCount")
                    {
                        getPossessed = method;
                        var body = method.Body;
                        var processor = body.GetILProcessor();
                        body.Instructions.Clear();
                        processor.Append(processor.Create(OpCodes.Ldarg_1));
                        processor.Append(processor.Create(OpCodes.Call, method.Module.ImportReference(getPossessedMainIngredientCount)));
                        processor.Append(processor.Create(OpCodes.Ret));
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                var manufactureUICraftRecipeDataModel = uiManufactureModule.GetType("SunnySideUp.UI.Manufacture.ManufactureUICraftRecipeDataModel");
                MethodDefinition getRecipeMethod = null;
                foreach (var pr in manufactureUICraftRecipeDataModel.Properties)
                {
                    if (pr.Name == "Recipe")
                        getRecipeMethod = pr.GetMethod;
                }

                var outputDetailedViewModel = uiManufactureModule.GetType("SunnySideUp.UI.Manufacture.OutputDetailedViewMenu");

                foreach (var method in outputDetailedViewModel.Methods)
                {
                    if (method.Name == "UpdateIngredientCountText")
                    {
                        var body = method.Body;
                        var processor = body.GetILProcessor();
                        for (var i = 0; i < body.Instructions.Count; i++)
                        {
                            if (body.Instructions[i].OpCode == OpCodes.Ldflda)
                            {
                                processor.InsertAfter(body.Instructions[i], processor.Create(OpCodes.Call, method.Module.ImportReference(getPossessedMainIngredientCount)));
                                processor.InsertAfter(body.Instructions[i], processor.Create(OpCodes.Call, method.Module.ImportReference(getRecipeMethod)));
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i + 3);
                                processor.RemoveAt(i - 2);
                            }
                        }
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                uiManufactureModule.Write(System.IO.Path.Combine(managedPath, "SunnySideUp.UI.Manufacture.dll"));


                var inventorySystem = System.IO.Path.Combine(translatorBackupPath, "Opsive.UltimateInventorySystem.dll");
                var inventorySystemAssembly = AssemblyDefinition.ReadAssembly(inventorySystem, new ReaderParameters() { AssemblyResolver = resolver });
                var inventorySystemModule = inventorySystemAssembly.MainModule;
                var craftingProcessor = inventorySystemModule.GetType("Opsive.UltimateInventorySystem.Crafting.Processors.SimpleCraftingProcessor");

                foreach (var method in craftingProcessor.Methods)
                {
                    if (method.Name == "TryAutoSelectIngredients")
                    {
                        var body = method.Body;
                        VariableDefinition itemCollectionGroup = null;
                        foreach (var variable in body.Variables)
                        {
                            if (variable.VariableType.Name == "ItemCollectionGroup")
                                itemCollectionGroup = variable;
                        }
                        var processor = body.GetILProcessor();
                        body.SimplifyMacros();
                        for (var i = 0; i < body.Instructions.Count - 1; i++)
                        {
                            if (body.Instructions[i].OpCode == OpCodes.Ldnull && body.Instructions[i + 1].OpCode == OpCodes.Stloc)
                            {
                                var inst = body.Instructions[i + 4];

                                processor.InsertBefore(inst, processor.Create(OpCodes.Ldloc, itemCollectionGroup));
                                processor.InsertBefore(inst, processor.Create(OpCodes.Call, method.Module.ImportReference(addAllInventoriesToGroup)));
                                processor.InsertBefore(inst, processor.Create(OpCodes.Stloc, itemCollectionGroup));
                                break;
                            }
                        }
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                var inventory = inventorySystemModule.GetType("Opsive.UltimateInventorySystem.Core.InventoryCollections.Inventory");

                foreach (var method in inventory.Methods)
                {
                    if (method.Name == "HasItemList" && ((GenericInstanceType)method.Parameters[0].ParameterType).GenericArguments[0].Name == "ItemInfo")
                    {
                        var body = method.Body;
                        body.Instructions.Clear();
                        var processor = body.GetILProcessor();
                        body.SimplifyMacros();
                        processor.Append(processor.Create(OpCodes.Ldarg_0));
                        processor.Append(processor.Create(OpCodes.Ldarg_1));
                        processor.Append(processor.Create(OpCodes.Call, method.Module.ImportReference(hasItemListMethod)));
                        processor.Append(processor.Create(OpCodes.Ret));
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                inventorySystemModule.Write(System.IO.Path.Combine(managedPath, "Opsive.UltimateInventorySystem.dll"));


                var collectiblesCraftingUI = System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.CollectiblesCraftingUI.dll");
                var collectiblesCraftingUIAssembly = AssemblyDefinition.ReadAssembly(collectiblesCraftingUI, new ReaderParameters() { AssemblyResolver = resolver });
                var collectiblesCraftingUIModule = collectiblesCraftingUIAssembly.MainModule;
                var craftingListContainerItemUIViewModel = collectiblesCraftingUIModule.GetType("SunnySideUp.CraftingListContainerItemUIViewModel");

                foreach (var method in craftingListContainerItemUIViewModel.Methods)
                {
                    if (method.Name == "SetContent")
                    {
                        var body = method.Body;
                        var processor = body.GetILProcessor();
                        body.SimplifyMacros();
                        for (var i = 0; i < body.Instructions.Count; i++)
                        {
                            if (body.Instructions[i].OpCode == OpCodes.Callvirt && body.Instructions[i].Operand is MethodReference mref && mref.Name == "GetItemAmount")
                            {
                                var inst = body.Instructions[i];
                                processor.InsertBefore(inst, processor.Create(OpCodes.Ldnull));
                                processor.InsertAfter(inst, processor.Create(OpCodes.Call, method.Module.ImportReference(getItemCountMethod)));
                                processor.RemoveAt(i - 2);
                                processor.RemoveAt(i - 2);
                                processor.Remove(inst);
                                break;
                            }
                        }
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                collectiblesCraftingUIModule.Write(System.IO.Path.Combine(managedPath, "SunnySideUp.UI.CollectiblesCraftingUI.dll"));


            }
            else
            {
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.Item.dll"), System.IO.Path.Combine(managedPath, "SunnySideUp.Item.dll"), true);
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.Brewing.dll"), System.IO.Path.Combine(managedPath, "SunnySideUp.UI.Brewing.dll"), true);
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.Manufacture.dll"), System.IO.Path.Combine(managedPath, "SunnySideUp.UI.Manufacture.dll"), true);
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "Opsive.UltimateInventorySystem.dll"), System.IO.Path.Combine(managedPath, "Opsive.UltimateInventorySystem.dll"), true);
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "SunnySideUp.UI.CollectiblesCraftingUI.dll"), System.IO.Path.Combine(managedPath, "SunnySideUp.UI.CollectiblesCraftingUI.dll"), true);
            }

            if (options.Contains(3))
            {
                WriteLog("Applying sleep anytime mod...");
                var sleep = System.IO.Path.Combine(translatorBackupPath, "Sleep.dll");
                var sleepAssembly = AssemblyDefinition.ReadAssembly(sleep, new ReaderParameters() { AssemblyResolver = resolver });
                var sleepModule = sleepAssembly.MainModule;
                var sleepController = sleepModule.GetType("SunnySideUp.SleepController");
                foreach (var method in sleepController.Methods)
                {
                    if (method.Name == "CheckSleepTime")
                    {
                        var body = method.Body;
                        body.Instructions.Clear();
                        var processor = body.GetILProcessor();
                        processor.Append(processor.Create(OpCodes.Ldc_I4_1));
                        processor.Append(processor.Create(OpCodes.Ret));
                        body.OptimizeMacros();
                        body.Optimize();
                    }
                }
                sleepModule.Write(System.IO.Path.Combine(managedPath, "Sleep.dll"));
            }
            else
            {
                System.IO.File.Copy(System.IO.Path.Combine(translatorBackupPath, "Sleep.dll"), System.IO.Path.Combine(managedPath, "Sleep.dll"), true);
            }

            var assemblyCSharp = System.IO.Path.Combine(translatorBackupPath, "Assembly-CSharp.dll");
            var assemblyCSharpAssembly = AssemblyDefinition.ReadAssembly(assemblyCSharp, new ReaderParameters { AssemblyResolver = resolver });
            var assemblyCSharpModule = assemblyCSharpAssembly.MainModule;
            assemblyCSharpModule.Resources.Add(new EmbeddedResource("Modded", ManifestResourceAttributes.Public, new byte[] { }));

            WriteLog("Saving Assembly-CSharp.dll");
            assemblyCSharpAssembly.Write(System.IO.Path.Combine(managedPath, "Assembly-CSharp.dll"));

            WriteLog("Saving configuration...");
            System.IO.File.WriteAllText(System.IO.Path.Combine(gameDirectory, "config"), config);

            WriteLog("Patching complete! :)");
        }

        void CopyFiles(string gameDirectory, bool isModded, string gameName, HashSet<int> options)
        {
            var translatorBackupPath = Path.Combine(gameDirectory, "TranslatorBackup");
            if (!Directory.Exists(translatorBackupPath))
            {
                WriteLog("Translator Backup directory doesn't exist yet. Creating now.");
                Directory.CreateDirectory(translatorBackupPath);
            }

            var currentDirectory = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
            if (Path.GetFullPath(currentDirectory) != Path.GetFullPath(gameDirectory))
            {
                if (options.Contains(0) || options.Contains(1))
                {
                    string translatingFile = "table.de-DE.trans"; // default translation remain german
                    if (options.Contains(1))
                    {
                        translatingFile = "table.fr-FR.trans";
                    }

                    var copyFiles = new string[] { "table.orig", "table.trans", "font" };
                    foreach (var copyFile in copyFiles)
                    {
                        string srcFile = copyFile;
                        string dstFile = copyFile;

                        if (dstFile == "table.trans")
                        {
                            srcFile = translatingFile;
                        }

                        WriteLog("Copying " + srcFile);
                        File.Copy(Path.Combine(currentDirectory, "data", srcFile), Path.Combine(gameDirectory, dstFile), true);
                    }
                }
            }

#if MACOS
            var managedPath = Path.Combine(gameDirectory, "LWIW.app", "Contents", "Resources", "Data", "Managed");
#else
            var managedPath = Path.Combine(gameDirectory, gameName + "_Data", "Managed");
#endif

            WriteLog("Copying TranslatorPlugin.dll to Managed directory...");
            File.Copy(Path.Combine(currentDirectory, "data", "TranslatorPlugin.dll"), Path.Combine(managedPath, "TranslatorPlugin.dll"), true);

            var checkFiles = new string[] { "Assembly-CSharp.dll", "Unity.TextMeshPro.dll", "DialogueSystem.dll", "UnityEngine.CoreModule.dll", "SunnySideUp.HUD.Contents.dll", "SunnySideUp.Item.dll", "Sleep.dll", "SunnySideUp.UI.Brewing.dll", "SunnySideUp.UI.Manufacture.dll", "Opsive.UltimateInventorySystem.dll", "SunnySideUp.UI.CollectiblesCraftingUI.dll" };
            foreach (var checkFile in checkFiles)
            {
                if (!isModded || !System.IO.File.Exists(Path.Combine(translatorBackupPath, checkFile)))
                {
                    WriteLog(checkFile + " need to be copied. Copying now.");
                    File.Copy(Path.Combine(managedPath, checkFile), Path.Combine(translatorBackupPath, checkFile), true);
                }
            }
        }

        bool CheckIfModded(string gameDirectory, string gameName)
        {
#if MACOS
            var managedPath = Path.Combine(gameDirectory, "LWIW.app", "Contents", "Resources", "Data", "Managed");
#else
            var managedPath = Path.Combine(gameDirectory, gameName + "_Data", "Managed");
#endif
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.Combine(managedPath));
            var assemblyCSharp = System.IO.Path.Combine(managedPath, "Assembly-CSharp.dll");
            var assemblyCSharpAssembly = AssemblyDefinition.ReadAssembly(assemblyCSharp, new ReaderParameters { AssemblyResolver = resolver });
            var assemblyCSharpModule = assemblyCSharpAssembly.MainModule;
            try
            {
                foreach (var resource in assemblyCSharpModule.Resources)
                {
                    if (resource.Name == "Modded")
                        return true;
                }
                return false;
            }
            finally
            {
                assemblyCSharpModule.Dispose();
                assemblyCSharpAssembly.Dispose();
            }

        }

        public override void Extract(string gameDirectory)
        {
            
        }

        public override void SetLanguage(string languageCode)
        {
        }

        public override string[] GetSupportedLanguages()
        {
            return new string[] { "DE", "EN" };
        }

        public override (string, Action)[] GetDebugActions()
        {
            return new (string, Action)[0];
        }
    }
}