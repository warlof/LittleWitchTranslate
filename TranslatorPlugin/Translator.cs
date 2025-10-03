using Opsive.Shared.UI;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.ChatMapper;
using SunnySideUp;
using SunnySideUp.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace TranslatorPlugin
{
    public class Translator
    {
        public static bool UseTranslation = false;
        public static HashSet<string> AllTranslations = new HashSet<string>();
        public static HashSet<string> MissingTranslations = new HashSet<string>();
        public static Dictionary<string, string> Translations = new Dictionary<string, string>();
        private static bool Initialized = false;
        private static bool ComponentsAdded = false;
        private static Dictionary<string, TMP_FontAsset> Fonts = new Dictionary<string, TMP_FontAsset>();
        public static void Initialize()
        {
            if (!Initialized)
            {
                Initialized = true;
                var config = System.IO.File.ReadAllLines("config");
                foreach (var c in config)
                {
                    if (c == "translate")
                        UseTranslation = true;
                }

                if (UseTranslation)
                {
                    var assetBundle = AssetBundle.LoadFromFile("font");
                    Fonts.Add("dalmoori", assetBundle.LoadAsset<TMP_FontAsset>("dalmoori"));
                    Fonts.Add("SDSamliphopangcheTTFBasic", assetBundle.LoadAsset<TMP_FontAsset>("SDSamliphopangcheBasic"));
                    Fonts.Add("D2Coding Static", assetBundle.LoadAsset<TMP_FontAsset>("D2Coding"));
                    Fonts.Add("Cafe24Ohsquare SDF", assetBundle.LoadAsset<TMP_FontAsset>("Cafe24Ohsquare"));
                    Fonts.Add("SDSamliphopangcheTTFBasic SDF_40", assetBundle.LoadAsset<TMP_FontAsset>("SDSamliphopangcheBasicHinted"));
                    Fonts.Add("SDSamliphopangcheTTFBasic SDF", assetBundle.LoadAsset<TMP_FontAsset>("SDSamliphopangcheBasicHintedSDFAA"));
                    Fonts.Add("JejuHallasan", assetBundle.LoadAsset<TMP_FontAsset>("JejuHallasan"));
                    Fonts.Add("JejuHallasan SDF_40", assetBundle.LoadAsset<TMP_FontAsset>("JejuHallasanSDF"));
                    Fonts.Add("Handwriting-Regular SDF", assetBundle.LoadAsset<TMP_FontAsset>("Handwriting"));

                    //Ignore.Initialize();
                    var lines1 = System.IO.File.ReadAllLines("table.orig");
                    var lines2 = System.IO.File.ReadAllLines("table.trans");
                    if (lines2.Length >= lines1.Length)
                    {
                        for (var i = 0; i < lines1.Length; i++)
                        {
                            var l = lines1[i];
                            var ind = l.IndexOf("||");
                            if (ind > 0)
                                l = l.Substring(0, ind).Trim() + "||" + l.Substring(ind + 2).Trim();
                            else l = l.Trim();
                            //System.IO.File.AppendAllText("log.txt", l + "\r\n");
                            if (!Translations.ContainsKey(l))
                            {
                                Translations.Add(l, lines2[i]);
                                AllTranslations.Add(l.Trim());
                                AllTranslations.Add(lines2[i].Trim());
                            }
                        }
                    }
                }
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            }
        }

        private static FieldInfo DateTextField;
        private static FieldInfo TimeTextField;
        private static string _dateFormat = "{0} <mspace=0.65em>{1:00}</mspace> {2}";
        private static string _timeFormat = "{0:00}:{1:00} <size=8>{2}</size>";
        private static Dictionary<DayOfWeek, string> DayNames = new Dictionary<DayOfWeek, string>()
        {
            { DayOfWeek.Monday, "Montag" },
            { DayOfWeek.Tuesday, "Dienstag" },
            { DayOfWeek.Wednesday, "Mittwoch" },
            { DayOfWeek.Thursday, "Donnerstag" },
            { DayOfWeek.Friday, "Freitag" },
            { DayOfWeek.Saturday, "Samstag" },
            { DayOfWeek.Sunday, "Sonntag" },
        };
        public static void UpdateTime(TimeHUDController controller)
        {
            if (UseTranslation)
            {
                if (DateTextField == null)
                {
                    var fields = typeof(TimeHUDController).GetFields(System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in fields)
                    {
                        if (f.Name == "_dateText")
                            DateTextField = f;
                        if (f.Name == "_timeText")
                            TimeTextField = f;
                    }
                }

                try
                {
                    GameDateTime currentTime = TimeManager.CurrentTime;
                    int num = currentTime.Month - 1;
                    int day = currentTime.Day;
                    string arg = DayNames[currentTime.GetDayOfWeek()].Substring(0, 3).ToUpper();
                    int hour = currentTime.Hour;
                    int minute = currentTime.Minute;
                    (DateTextField.GetValue(controller) as TextMeshProUGUI).text = string.Format(_dateFormat, $"<sprite={num}>", day, arg);
                    (TimeTextField.GetValue(controller) as TextMeshProUGUI).text = string.Format(_timeFormat, hour, minute, "");
                }
                catch (Exception e)
                {

                }
            }
        }

        private static void SceneManager_sceneLoaded(UnityEngine.SceneManagement.Scene arg0, UnityEngine.SceneManagement.LoadSceneMode arg1)
        {
            if (!ComponentsAdded)
            {
                var go = new UnityEngine.GameObject();
                UnityEngine.Object.DontDestroyOnLoad(go);
                //go.AddComponent<Extractor>();
                go.AddComponent<TranslatorComponent>();
                ComponentsAdded = true; 
            }
        }

        public static bool IsUnknown(string line)
        {
            return !AllTranslations.Contains(line.Trim());
        }

        public static void CheckForUnknown(string text)
        {
            if (!text.Contains("<alpha=#00 id = \"a\">"))
            {
                var lines = text.Split(new char[] { '\r', '\n' });
                foreach (var line in lines)
                {
                    if (!AllTranslations.Contains(line.Trim()) && !MissingTranslations.Contains(line.Trim()))
                    {
                        MissingTranslations.Add(line.Trim());
                        //System.IO.File.AppendAllText("missing.txt", line + "\r\n");
                    }
                }
            }
        }

        private static HashSet<string> DontTouchText = new HashSet<string>() {
        };

        private static HashSet<string> DontTouchTextParent = new HashSet<string>() {
        };

        private static HashSet<string> TextFields = new HashSet<string>();
        private static TMP_FontAsset FontAsset;

        private static List<string> FontNames = new List<string>();
        private static void AnalyzeFont(TMP_FontAsset font)
        {
            var fontName = font.name;
            foreach (var kv in Fonts)
                if (kv.Value == font)
                    return;
            if (!FontNames.Contains(fontName))
            {
                FontNames.Add(fontName);
                var text = fontName + ": \r\nHeight: " + font.atlasHeight + "\r\nWidth: " + font.atlasWidth + "\r\nPadding: " + font.atlasPadding + "\r\nSize: " + font.creationSettings.pointSize + "\r\nStyle: " + font.creationSettings.fontStyle + "\r\nChars: " + font.creationSettings.characterSequence + "\r\nFormat: " + font.atlasRenderMode.ToString() + "\r\n";
                System.IO.File.AppendAllText("font.txt", text + "\r\n");
            }
        }
        private static string GetPath(Transform t)
        {
            var ret = t.name;
            var p = t.parent;
            while (p != null) 
            {
                ret = p.name + "." + ret;
                p = p.parent;
            }
            return ret;
        }

        public static void ChangeFont(TMPro.TMP_FontAsset font)
        {
            if (UseTranslation)
            {
                if (Fonts.ContainsKey(font.name))
                {
                    if (!font.fallbackFontAssetTable.Contains(Fonts[font.name]))
                        font.fallbackFontAssetTable.Add(Fonts[font.name]);
                }
                //else
                //    AnalyzeFont(font);
            }
        }
        public static void ChangeTextMesh(TMPro.TextMeshProUGUI text)
        {
            if (UseTranslation)
            {
                var path = GetPath(text.transform);
                if (path.EndsWith("MailDetailsPage.Content.ContentArea.LocalizedLabel"))
                {
                    var textSettings = text.transform.GetComponent<LocalizeTextSettingEvent>();
                    if (textSettings != null)
                        GameObject.Destroy(textSettings);
                    text.fontSizeMin = 4;
                    text.fontSizeMax = 7;
                }

                ChangeFont(text.font);
            }
        }

        public static string TrimAndGetWhitespaces(string str, out string before, out string after)
        {
            var emptySpacesBefore = 0;
            var emptySpacesAfter = 0;
            for (var i = 0; i < str.Length; i++)
            {
                if (str[i] != ' ')
                    break;
                emptySpacesBefore++;
            }
            for (var i = str.Length - 1; i >= 0; i--)
            {
                if (str[i] != ' ')
                    break;
                emptySpacesAfter++;
            }
            before = new string(' ', emptySpacesBefore);
            after = new string(' ', emptySpacesAfter);
            return str.Trim();
        }

        private static string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static string TranslateLine(string st, string context)
        {
            if (UseTranslation)
            {
                var ret = "";
                var trimmed = TrimAndGetWhitespaces(st, out var trimBefore, out var trimAfter);
                if (trimmed != "")
                {
                    if (context != null && Translations.ContainsKey(context + "||" + trimmed))
                        return trimBefore + Translations[context + "||" + trimmed] + trimAfter;
                    else if (Translations.ContainsKey(trimmed))
                        return trimBefore + Translations[trimmed] + trimAfter;
                    else
                    {
                        var i = 0;
                        for (; i < trimmed.Length; i++)
                        {
                            if (Characters.IndexOf(trimmed[i]) != -1)
                                break;
                        }
                        var pre = trimmed.Substring(0, i);
                        var t = TrimAndGetWhitespaces(trimmed.Substring(i), out var trimBefore2, out var trimAfter2);
                        if (Translations.ContainsKey(t))
                            return trimBefore + pre + trimBefore2 + Translations[t] + trimAfter2 + trimAfter;
                        else
                            return st;
                    }
                }
            }
            return st;
        }
        public static string TranslateString(string st, string context)
        {
            Initialize();
            if (UseTranslation)
            {
                if (st == null) return null;
                context = context.Trim();
                var currentStr = "";
                var ret = "";
                for (var i = 0; i < st.Length; i++)
                {
                    if (st[i] == '\r' || st[i] == '\n')
                    {
                        if (currentStr != "")
                        {
                            ret += TranslateLine(currentStr, context);
                        }
                        ret += st[i];
                        currentStr = "";
                    }
                    else currentStr += st[i];
                }

                ret += TranslateLine(currentStr, context);
                return ret;
            }
            else return st;
        }
    }
}
