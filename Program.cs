using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ZstdSharp.Unsafe;

namespace LittleWitchTranslate
{
    class Program
    {

        static void CombineTables()
        {
            var lines1 = System.IO.File.ReadAllLines("data/table.orig");
            var lines2 = System.IO.File.ReadAllLines("data/table.trans");
            var lines3 = System.IO.File.ReadAllLines("data/newtable.orig");
            Dictionary<string, string> dict = new Dictionary<string, string>();
            for (var i = 0; i < lines1.Length; i++)
            {
                dict.Add(lines1[i], lines2[i]);
            }

            var t = "";
            var t2 = "";
            for (var i = 0; i < lines3.Length; i++)
            {
                t += lines3[i] + "\r\n";
                if (dict.ContainsKey(lines3[i]))
                    t2 += dict[lines3[i]] + "\r\n";
                else
                    t2 += "\r\n";
            }

            System.IO.File.WriteAllText("newtable.orig", t);
            System.IO.File.WriteAllText("newtable.trans", t2);
        }

        static void Compress()
        {
            var files = System.IO.Directory.GetFiles("libs/");
            foreach (var file in files)
            {
                var outputPath = file + ".zstd";
                using (var input = new System.IO.FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (var encoder = new ZstdSharp.CompressionStream(output))
                {
                    input.CopyTo(encoder, 4096);
                }
            }
        }
        [STAThread]
        static void Main(string[] args)
        {
            /*Compress();
            return;*/
            /*
            var lines1 = System.IO.File.ReadAllLines("data/table.orig");
            var lines2 = System.IO.File.ReadAllLines("data/table.trans");
            var lines3 = System.IO.File.ReadAllLines("data/newtable.orig");
            var kv = new Dictionary<string, string>();
            for (var i = 0; i < lines1.Length; i++)
            {
                kv.Add(lines1[i], lines2[i]);
                
                var depth = 0;
                var valid = true;
                var bracketContent = new List<string>();
                var start = 0;
                var originalLine = lines1[i];
                for (var j = 0; j < lines1[i].Length; j++)
                {
                    var c = lines1[i][j];
                    if (c == '[' || c == '<')
                    {
                        if (depth == 0)
                            start = j;
                        depth++;
                    }
                    if (c == ']' || c == '>')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            bracketContent.Add(lines1[i].Substring(start, (j + 1) - start));
                        }
                        if (depth < -1)
                        {
                            System.IO.File.AppendAllText("log.txt", "Line is not closing correctly: " + lines1[i] + "\r\n");
                            valid = false;
                            break;
                        }
                    }
                }

                if (valid)
                {
                    if (bracketContent.Count > 0)
                    {
                        var translatedLine = lines2[i];
                        var num = 0;
                        start = 0;
                        depth = 0;
                        var end = 0;
                        var newLine = "";
                        for (var j = 0; j < lines2[i].Length; j++)
                        {
                            var c = lines2[i][j];
                            if (c == '[' || c == '<')
                            {
                                if (depth == 0)
                                    start = j;
                                depth++;
                            }
                            if (c == ']' || c == '>')
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    newLine += lines2[i].Substring(end, start - end) + bracketContent[num];
                                    end = j + 1;
                                    num++;
                                }
                                if (depth < -1)
                                {
                                    System.IO.File.AppendAllText("log.txt", "Line is not closing correctly: " + lines1[i] + "\r\n");
                                    valid = false;
                                    break;
                                }
                            }
                        }
                        newLine += lines2[i].Substring(end);
                        lines2[i] = newLine;
                    }
                }
                kv.Add(lines1[i], lines2[i]);
            }
            var t1 = "";
            var t2 = "";
            var found = new HashSet<string>();
            for (var i = 0; i < lines3.Length; i++)
            {
                if (kv.ContainsKey(lines3[i]))
                {
                    t1 += lines3[i] + "\r\n";
                    t2 += kv[lines3[i]] + "\r\n";
                    found.Add(lines3[i]);
                }
                else
                {
                    
                }
            }
            foreach (var kk in kv)
            {
                if (!found.Contains(kk.Key))
                {
                    t1 += kk.Key + "\r\n";
                    t2 += kk.Value + "\r\n";
                }
            }
            System.IO.File.WriteAllText("table.orig", t1);
            System.IO.File.WriteAllText("table.trans", t2);
            return;*/
            
            ModManagerGUI.ModManager.Start(new Mod(), new ModManagerGUI.Configuration() {
                ApplicationName = "Little Witch Translator",
                GameName = "Little Witch in the Woods",
                FileNames = new string[] { "LWIW", "Little Witch in the Woods" },
                DeveloperName = "SUNNY SIDE UP",
                SteamAppID = "1594940",
                AdditionalMods = new string[][] { new string[] { "German translation", "Deutsche Übersetzung" }, new string[] { "French translation", "Französische Übersetzung" }, new string[] { "Craft from storage", "Herstellen aus Lager" }, new string[] { "Sleep anytime", "Jederzeit schlafen" } },
                StandardMods = new int[]{ 0 }
            });
        }
    }
}
