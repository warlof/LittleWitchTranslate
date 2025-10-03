using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.UnityGUI;
using SunnySideUp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;

namespace TranslatorPlugin
{
    public class TranslatorComponent : MonoBehaviour
    {
        private static GameObject Notice;
        void Start()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == "TitleScene")
            {
                AddNotice();
            }
            if (arg0.name == "Storyline")
            {
                ReplaceTextes();
            }
            //System.IO.File.AppendAllText("scene.txt", arg0.name + " _ " + arg1 + "\r\n");
        }

        void Update()
        {
            var scenes = SceneManager.GetAllScenes();
            foreach (var scene in scenes)
            {
                if (scene.name == "TitleScene")
                    AddNotice();
            }
        }
        static void AddNotice()
        {
            if (Notice == null && Translator.UseTranslation)
            {
                Notice = GameObject.Find("TranslationNotice");
                if (Notice != null)
                    return;
                var canvas = GameObject.Find("Title Canvas");
                var viewportBorder = canvas.transform.Find("ViewportBorder");
                var settingsUI = viewportBorder.transform.Find("SettingsUI");
                var visual = settingsUI.transform.Find("Visual");
                var panelContainer = visual.transform.Find("PanelContainer");
                var label = panelContainer.Find("Title").GetChild(0);
                Notice = GameObject.Instantiate(label, viewportBorder.transform).gameObject;
                Notice.name = "TranslationNotice";

                GameObject.Destroy(Notice.GetComponent<LocalizeFontAssetEvent>());
                GameObject.Destroy(Notice.GetComponent<LocalizeFontMaterialEvent>());
                GameObject.Destroy(Notice.GetComponent<LocalizeTextSettingEvent>());
                GameObject.Destroy(Notice.GetComponent<LocalizeStringEvent>());
                
                var t = Notice.GetComponent<TextMeshProUGUI>();
                t.alignment = TextAlignmentOptions.TopLeft;
                t.color = Color.white;
                t.text = "Mit Liebe übersetzt von <color=#ff6600>PotatoePet</color>";
                t.fontSizeMax = 7;
                t.fontSizeMin = 7;
                var r = Notice.GetComponent<RectTransform>();
                r.anchorMax = new Vector2(0, 1);
                r.anchorMin = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(2, -2);
                
                var noticeShadow = GameObject.Instantiate(Notice, viewportBorder.transform).gameObject;
                var r2 = noticeShadow.GetComponent<RectTransform>();
                r2.anchoredPosition = new Vector2(2.5f, -2.5f);
                var t2 = noticeShadow.GetComponent<TextMeshProUGUI>();
                t2.text = "Mit Liebe übersetzt von PotatoePet";
                t2.color = Color.black;
                t2.fontSizeMax = 7;
                t2.fontSizeMin = 7;
                noticeShadow.transform.SetAsFirstSibling();
            }
        }

        static void TranslateFields(List<Field> fields)
        {
            if (fields == null)
                return;
            foreach (var f in fields)
            {
                if (f.title.EndsWith("en") && f.value.Trim() != "")
                {
                    f.value = Translator.TranslateString(f.value, "");
                }
            }
        }
        public static void ReplaceTextes()
        {
            if (Translator.UseTranslation)
            {
                var tables = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetAllTables().WaitForCompletion();
                foreach (var table in tables)
                {
                    if (table.LocaleIdentifier.Code.StartsWith("en"))
                    {
                        foreach (var val in table)
                        {
                            val.Value.Value = Translator.TranslateString(val.Value.Value, "");
                        }
                    }
                }
                if (DialogueManager.MasterDatabase != null)
                {
                    if (DialogueManager.MasterDatabase.conversations != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.conversations)
                        {
                            TranslateFields(kv.fields);
                            if (kv.dialogueEntries != null)
                                foreach (var d in kv.dialogueEntries)
                                    TranslateFields(d.fields);
                        }
                    }
                    if (DialogueManager.MasterDatabase.keywords != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.keywords)
                        {
                            TranslateFields(kv.fields);
                        }
                    }
                    if (DialogueManager.MasterDatabase.items != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.items)
                        {
                            TranslateFields(kv.fields);
                        }
                    }
                    if (DialogueManager.MasterDatabase.locations != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.locations)
                        {
                            TranslateFields(kv.fields);
                        }
                    }
                    if (DialogueManager.MasterDatabase.actors != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.actors)
                        {
                            TranslateFields(kv.fields);
                        }
                    }
                    if (DialogueManager.MasterDatabase.variables != null)
                    {
                        foreach (var kv in DialogueManager.MasterDatabase.variables)
                        {
                            TranslateFields(kv.fields);
                        }
                    }
                }
            }
        }
    }
}