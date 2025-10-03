using Opsive.Shared.Utility;
using Opsive.UltimateInventorySystem.Core;
using Opsive.UltimateInventorySystem.Core.DataStructures;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;
using Opsive.UltimateInventorySystem.Crafting;
using SunnySideUp;
using SunnySideUp.UI.BrewingFlowUI.ViewModels;
using SunnySideUp.UI.Core.UVMP.Components.UltimateInventorySystem;
using SunnySideUp.UI.Manufacture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TranslatorPlugin
{
    public class Inventory
    {
        private static FieldInfo BrewingInventoryViewModel;

        public static bool HasItemList(Opsive.UltimateInventorySystem.Core.InventoryCollections.Inventory inventory, ListSlice<ItemInfo> itemInfos)
        {
            for (int i = 0; i < itemInfos.Count; i++)
            {
                if (itemInfos[i].Amount > InventoryUtility.GetItemCount(inventory, InventorySystemManager.GetItemDefinition(itemInfos[i].Item.ItemDefinitionID), null))
                    return false;
            }
            return true;
        }

        public static ItemCollectionGroup AddAllInventoriesToGroup(ItemCollectionGroup group)
        {
            if (group == null)
            {
                group = new ItemCollectionGroup();
                group.AddItemCollection(InventoryUtility.PlayerInventory.MainItemCollection);

                var storages = UnityEngine.Object.FindObjectsByType<SunnySideUp.ItemStorageBox>(UnityEngine.FindObjectsSortMode.None);
                //var storages = ItemStorageBoxCache.GetAllItemStorage();
                foreach (var storage in storages)
                {
                    if (storage.Inventory != null)
                    {
                        group.AddItemCollection(storage.Inventory.MainItemCollection);
                    }
                }
            }

            return group;
        }
        public static int GetPossessedMainIngredientCount(CraftingRecipe recipe)
        {
            return InventoryUtility.GetItemCount(InventoryUtility.PlayerInventory, recipe.Ingredients.ItemDefinitionAmounts.Array[0].ItemDefinition, null);
        }

        public static ItemCollection CreateCraftingInventory(BrewingFlowPageUIViewModel brewingPage)
        {
            if (BrewingInventoryViewModel == null)
            {
                var fields = typeof(BrewingFlowPageUIViewModel).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.Name == "_inventoryViewModel")
                    {
                        BrewingInventoryViewModel = f;
                        break;
                    }
                }
            }

            var inventoryUIModel = (InventoryUIViewModel)BrewingInventoryViewModel.GetValue(brewingPage);

            int slotCount = 0;
            var inventory = new ItemCollection();
            inventory.Initialize(null, true);
            Dictionary<uint, int> items = new Dictionary<uint, int>();
            if (InventoryUtility.PlayerInventory != null)
            {
                foreach (var itemInfo in InventoryUtility.PlayerInventory.AllItemInfos)
                {
                    if (itemInfo.Item != null)
                    {
                        var id = itemInfo.Item.ItemDefinitionID;
                        if (!items.ContainsKey(id))
                            items.Add(id, itemInfo.Amount);
                        else
                            items[id] = itemInfo.Amount;
                    }
                }
                var storages = UnityEngine.Object.FindObjectsByType<SunnySideUp.ItemStorageBox>(UnityEngine.FindObjectsSortMode.None);
                //var storages = ItemStorageBoxCache.GetAllItemStorage();
                foreach (var storage in storages)
                {
                    if (storage.Inventory != null)
                    {
                        foreach (var itemInfo in storage.Inventory.AllItemInfos)
                        {
                            if (itemInfo.Item != null)
                            {
                                var id = itemInfo.Item.ItemDefinitionID;
                                if (!items.ContainsKey(id))
                                    items.Add(id, itemInfo.Amount);
                                else
                                    items[id] = itemInfo.Amount;
                            }
                        }
                    }
                }
            }
            foreach (var kv in items)
                inventory.AddItem(InventorySystemManager.GetItemDefinition(kv.Key), kv.Value);
            if (inventoryUIModel != null)
                inventoryUIModel.CustomMaxSlotCount = items.Count;
            return inventory;
        }

        public static int GetItemCountFromStorage(Opsive.UltimateInventorySystem.Core.InventoryCollections.Inventory inventory, ItemDefinition itemDefinition, ItemCollection collection)
        {
            if (InventoryUtility.PlayerInventory != null && inventory == InventoryUtility.PlayerInventory)
            {
                try
                {
                    //var storages = ItemStorageBoxCache.GetAllItemStorage();
                    var storages = UnityEngine.Object.FindObjectsByType<SunnySideUp.ItemStorageBox>(UnityEngine.FindObjectsSortMode.None);
                    var count = 0;
                    foreach (var storage in storages)
                    {
                        if (storage.Inventory != null)
                            count += InventoryUtility.GetItemCount(storage.Inventory, itemDefinition, collection);
                    }
                    return count;
                }
                catch (Exception e)
                {
                    System.IO.File.AppendAllText("exceptions.txt", e.ToString() + "\r\n\r\n");
                }
            }
            return 0;
        }

        public static void RemoveItemsFromStorage(int removed, Opsive.UltimateInventorySystem.Core.InventoryCollections.Inventory inventory, ItemInfo itemInfo, ItemCollection collection)
        {
            if (InventoryUtility.PlayerInventory != null && inventory == InventoryUtility.PlayerInventory)
            {
                var toRemove = itemInfo.Amount - removed;
                var storages = UnityEngine.Object.FindObjectsByType<SunnySideUp.ItemStorageBox>(UnityEngine.FindObjectsSortMode.None);
                foreach (var storage in storages)
                {
                    if (toRemove == 0)
                        break;
                    var itemDefinition = InventorySystemManager.GetItemDefinition(itemInfo.ItemAmount.Item.ItemDefinitionID);
                    var count = InventoryUtility.GetItemCount(storage.Inventory, itemDefinition);
                    if (count > toRemove)
                    {
                        InventoryUtility.RemoveItem(storage.Inventory, new ItemInfo(itemDefinition, toRemove));
                        toRemove = 0;
                    }
                    else
                    {
                        InventoryUtility.RemoveItem(storage.Inventory, new ItemInfo(itemDefinition, count));
                        toRemove -= count;
                    }
                }
            }
        }
    }
}
