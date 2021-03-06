// TextGenerator.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DwarfCorp
{
    /// <summary>
    /// Generates random strings of text based on patterns. Like mad libs.
    /// </summary>
    public class TextGenerator
    {
        public static Dictionary<string, TextAtom> TextAtoms { get; set; }
        public static Dictionary<string, List<List<string>>> Templates { get; set; }
        private static bool staticsInitialized = false;

        public static string SplitCamelCase(string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }

        public static string TimeToString(TimeSpan age)
        {
            if (age.Days > 365)
            {
                return String.Format("{0} year{1}", age.Days / 365, age.Days / 365 > 1 ? "s" : "");
            }
            if (age.Days > 30)
            {
                return String.Format("{0} month{1}", age.Days / 30, age.Days / 30 > 1 ? "s" : "");
            }
            if (age.Days > 0)
            {
                return String.Format("{0} day{1}", age.Days, age.Days > 1 ? "s" : "");
            }
            if (age.Hours > 0)
            {
                return String.Format("{0} hour{1}", age.Hours, age.Hours > 1 ? "s" : "");
            }
            if (age.Minutes > 0)
            {
                return String.Format("{0} minute{1}", age.Minutes, age.Minutes > 1 ? "s" : "");
            }
            return "Moments";
        }

        public static string AgeToString(TimeSpan age)
        {
            return String.Format("{0} ago", TimeToString(age));
        }

        public static string[] Literals =
        {
            " ",
            ".",
            "!",
            "&",
            "(",
            ")",
            ",",
            "+",
            "-",
            "%",
            "_",
            "@",
            "$",
            "^",
            "*",
            "\n",
            "\t",
            "\"",
            "\'"
        };

        public static bool IsVowel(char character)
        {
            return character == 'a' || character == 'i' || character == 'e' || character == 'o' || character == 'u' ||
                   character == 'y' || character == 'A' || character == 'E' || character == 'I' || character == 'U' || character == 'Y';
        }

        public static string IndefiniteArticle(string item)
        {
            if (item.Length > 0)
            {
                return IsVowel(item[0]) ? "an " + item : "a " + item;
            }

            return item;
        }

        public static string GetListString(IEnumerable<string> tokenEnumerator)
        {
            List<string> tokens = tokenEnumerator.ToList();
            string list = "";
            for (int i = 0; i < tokens.Count; i++)
            {
                list += tokens[i];

                if (i == tokens.Count - 2 && tokens.Count > 1)
                {
                    list += " and ";
                }
                else if (tokens.Count > 1 && i < tokens.Count - 1)
                {
                    list += ", ";
                }
            }

            return list;
        }

   
        public static List<List<string>> GetAtoms(string type)
        {
            string text = "";
            using (var stream = TitleContainer.OpenStream("Content" + Path.DirectorySeparatorChar + type))
            {
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
            }
            char[] delimiters = { '!', '\'', ',', '.', '(', ')', ';', '?', '|', ' ', '\t' };
            List<List<string>> toReturn = new List<List<string>>();
            foreach (string line in text.Split('\n', '\r'))
            {

                if (string.IsNullOrEmpty(line) || line == "\n" || line == "\r")
                {
                    continue;
                }

                List<string> current = new List<string>();
                string transformed = line;
                foreach (char c in delimiters)
                {
                    var s = "" + c;
                    transformed = transformed.Replace(s, "~" + c + "~");
                }

                foreach (string word in transformed.Split('~'))
                {

                    var match = Regex.Match(word, @"\<(.*?)\>");

                    if (match.Success)
                    {
                        foreach (var atom in Regex.Split(word, @"(\<[a-zA-Z0-9_]*\>)"))
                        {
                            if (atom == "")
                                continue;
                            var replacement = Regex.Replace(atom, @"\<([a-zA-Z0-9_]*)\>", "$$$+");
                            foreach (string word2 in replacement.Split(delimiters, StringSplitOptions.None))
                            {
                                current.Add(word2);
                            }
                        }
                    }
                    else
                    {
                        current.Add(word);
                    }


                }

                toReturn.Add(current);
            }

            return toReturn;
        }

        public static string[] GetDefaultStrings(string type)
        {
            string text = "";
            using (var stream = TitleContainer.OpenStream("Content" + Path.DirectorySeparatorChar + type))
            {
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
            }
            return text.Split('\n', '\r').Where(a => a != "" && a != " " && a != "\r" && a != "\n").ToArray();
        }

        public static void CreateDefaults()
        {
            TextAtoms = new Dictionary<string, TextAtom>();
            Templates = new Dictionary<string, List<List<string>>>();
            LoadAtoms();
            LoadTemplates();
            staticsInitialized = true;
        }

        public static void LoadTemplates()
        {
            string dirname = "." + Path.DirectorySeparatorChar + "Content" + Path.DirectorySeparatorChar + "Text" + Path.DirectorySeparatorChar + "Templates";
            System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(dirname);

            if (!directoryInfo.Exists) throw new FileNotFoundException("Unable to find text directory : " + dirname);

            foreach (System.IO.FileInfo info in directoryInfo.EnumerateFiles())
            {
                var match = Regex.Match(info.Name, @"(.*)\.txt");

                if (match.Success)
                {
                    Templates["$" + match.Groups[1].Value] = GetAtoms(ProgramData.CreatePath("Text", "Templates", match.Groups[1].Value + ".txt"));
                }
            }
        }

        public static void LoadAtoms()
        {
            string dirname = "." + Path.DirectorySeparatorChar + "Content" + Path.DirectorySeparatorChar + "Text";
            System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(dirname);

            if (!directoryInfo.Exists) throw new FileNotFoundException("Unable to find text directory : " + dirname);

            foreach (System.IO.FileInfo info in directoryInfo.EnumerateFiles())
            {
                var match = Regex.Match(info.Name, @"(.*)\.txt");

                if (match.Success)
                {
                    AddAtom(new TextAtom("$" + match.Groups[1].Value,
                        GetDefaultStrings("Text" + Path.DirectorySeparatorChar + info.Name)));
                }
            }
        }

        public static string ToTitleCase(string strX)
        {
            string[] aryWords = strX.Trim().Split(' ');

            List<string> lstLetters = new List<string>();
            List<string> lstWords = new List<string>();

            foreach (string strWord in aryWords)
            {
                int iLCount = 0;
                foreach (char chrLetter in strWord.Trim())
                {
                    if (iLCount == 0)
                    {
                        lstLetters.Add(chrLetter.ToString(CultureInfo.InvariantCulture).ToUpper());
                    }
                    else
                    {
                        lstLetters.Add(chrLetter.ToString(CultureInfo.InvariantCulture).ToLower());
                    }
                    iLCount++;
                }
                lstWords.Add(string.Join("", lstLetters));
                lstLetters.Clear();
            }

            string strNewString = string.Join(" ", lstWords);

            return strNewString;
        }

        public static string ToSentenceCase(string input)
        {
            var regex = new Regex(@"(^[a-z])|[?!.:,;]\s+(.)", RegexOptions.ExplicitCapture);
            return regex.Replace(input, s => s.Value.ToUpper());
        }

        public static string GenerateRandom(List<List<string>> templates)
        {
            return GenerateRandom(Datastructures.SelectRandom(templates).ToArray());
        }

        public static string GenerateRandom(List<string> arguments, List<List<string>> templates)
        {
            return GenerateRandom(arguments, Datastructures.SelectRandom(templates).ToArray());
        }

        public static string GenerateRandom(params string[] atoms)
        {
            return GenerateRandom(new List<string>(), atoms);
        }


        public static int GetArgument(string literal)
        {
            if (Regex.Match(literal, @"#(\d+)").Success)
            {
                try
                {
                    return int.Parse(Regex.Match(literal, @"#(\d+)").Groups[1].Value);
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            return -1;
        }

        public static string GenerateRandom(List<string> arguments, params string[] atoms)
        {
            if(!staticsInitialized)
            {
                CreateDefaults();
            }
            string toReturn = "";
            foreach(string s in atoms)
            {
                int argument = GetArgument(s) - 1;
                if (argument >= 0 && argument < arguments.Count)
                {
                    toReturn += Regex.Replace(s, @"#\d+", arguments[argument]);
                }
                else if(Literals.Contains(s))
                {
                    toReturn += s;
                }
                else if(TextAtoms.ContainsKey(s))
                {
                    toReturn += ToTitleCase(TextAtoms[s].GetRandom());
                }
                else if (Templates.ContainsKey(s))
                {
                    toReturn += GenerateRandom(Templates[s]);
                }
                else if (Regex.Match(s, @"\${(.*?)}").Success)
                {
                    string match = Regex.Match(s, @"\${(.*?)}").Groups[1].Value;
                    string[] splits = match.Split(',');

                    if (splits.Length > 0)
                        toReturn += Datastructures.SelectRandom(splits);
                }
                else
                {
                    toReturn += s;
                }
            }

            return toReturn;
        }

        public TextGenerator()
        {
            if(!staticsInitialized)
            {
                TextAtoms = new Dictionary<string, TextAtom>();
                CreateDefaults();
                staticsInitialized = true;
            }
        }

        public static void AddAtom(TextAtom atom)
        {
            TextAtoms[atom.Name] = atom;
        }

        private static char[] vowels = { 'a', 'e', 'i', 'o', 'u', 'A', 'E', 'I', 'O', 'U', ' ', '-', '_', '.', ',' };

        public static string RemoveVowels(string name)
        {
            var builder = new StringBuilder();
            foreach(var letter in name.Where((c, i) => i == 0 || !vowels.Contains(c)))
            {
                builder.Append(letter);
            }
            return builder.ToString();
        }

        public static string Shorten(string name, int v)
        {
            if (name.Length > v)
            {
                var substr = RemoveVowels(name);
                return substr.Substring(0, Math.Min(v, substr.Length)) + ".";
            }
            return name;
        }
    }

}