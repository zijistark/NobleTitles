// Decompiled with JetBrains decompiler
// Type: NobleTitles.TitleDb
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;


#nullable enable
namespace NobleTitles
{
    internal class TitleDb
    {
        protected Dictionary<string, TitleDb.CultureEntry> cultureMap;
        protected TitleDb.CultureEntry noCulture = new TitleDb.CultureEntry(new TitleDb.Entry("King", "Queen"), new TitleDb.Entry("Duke", "Duchess"), new TitleDb.Entry("Count", "Countess"), new TitleDb.Entry("Baron", "Baroness"), new TitleDb.Entry("Governor", "Governor"));

        internal TitleDb.Entry GetKingTitle(CultureObject culture)
        {
            TitleDb.CultureEntry cultureEntry;
            return culture != null && this.cultureMap.TryGetValue(culture.StringId, out cultureEntry) ? cultureEntry.King : this.noCulture.King;
        }

        internal TitleDb.Entry GetDukeTitle(CultureObject culture)
        {
            TitleDb.CultureEntry cultureEntry;
            return culture != null && this.cultureMap.TryGetValue(culture.StringId, out cultureEntry) ? cultureEntry.Duke : this.noCulture.Duke;
        }

        internal TitleDb.Entry GetCountTitle(CultureObject culture)
        {
            TitleDb.CultureEntry cultureEntry;
            return culture != null && this.cultureMap.TryGetValue(culture.StringId, out cultureEntry) ? cultureEntry.Count : this.noCulture.Count;
        }

        internal TitleDb.Entry GetBaronTitle(CultureObject culture)
        {
            TitleDb.CultureEntry cultureEntry;
            return culture != null && this.cultureMap.TryGetValue(culture.StringId, out cultureEntry) ? cultureEntry.Baron : this.noCulture.Baron;
        }

        internal TitleDb.Entry GetGovernorTitle(CultureObject culture)
        {
            TitleDb.CultureEntry cultureEntry;
            return culture != null && this.cultureMap.TryGetValue(culture.StringId, out cultureEntry) ? cultureEntry.Governor : this.noCulture.Baron;
        }

        internal string StripTitlePrefixes(Hero hero)
        {
            string str = hero.Name.ToString();
            string s = str;
            while (true)
            {
                foreach (TitleDb.CultureEntry cultureEntry in this.cultureMap.Values)
                {
                    if (hero.IsFemale)
                    {
                        s = this.StripTitlePrefix(s, cultureEntry.King.Female);
                        s = this.StripTitlePrefix(s, cultureEntry.Duke.Female);
                        s = this.StripTitlePrefix(s, cultureEntry.Count.Female);
                        s = this.StripTitlePrefix(s, cultureEntry.Baron.Female);
                        s = this.StripTitlePrefix(s, cultureEntry.Governor.Female);
                    }
                    else
                    {
                        s = this.StripTitlePrefix(s, cultureEntry.King.Male);
                        s = this.StripTitlePrefix(s, cultureEntry.Duke.Male);
                        s = this.StripTitlePrefix(s, cultureEntry.Count.Male);
                        s = this.StripTitlePrefix(s, cultureEntry.Baron.Male);
                        s = this.StripTitlePrefix(s, cultureEntry.Governor.Male);
                    }
                }
                s = this.StripTitlePrefix(this.StripTitlePrefix(s, "Great Khan "), "Great Khanum ");
                if (!str.Equals(s))
                    str = s;
                else
                    break;
            }
            return s;
        }

        internal TitleDb()
        {

            Util.Log.Print("Building DB");

            this.Path = BasePath.Name + "Modules/" + SubModule.Name + "/titles.json";
            this.cultureMap = JsonConvert.DeserializeObject<Dictionary<string, TitleDb.CultureEntry>>(File.ReadAllText(this.Path), new JsonSerializerSettings()
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            }) ?? throw new TitleDb.BadTitleDatabaseException("Failed to deserialize title database!");
            if (this.cultureMap.Count == 0)
                throw new TitleDb.BadTitleDatabaseException("Title database is empty!");
            if (!this.cultureMap.ContainsKey("default"))
                throw new TitleDb.BadTitleDatabaseException("Title database must contain a fallback culture entry keyed by \"default\"!");
            foreach (KeyValuePair<string, TitleDb.CultureEntry> culture in this.cultureMap)
            {
                string key = culture.Key;
                TitleDb.CultureEntry cultureEntry1 = culture.Value;
                string str = key;
                TitleDb.CultureEntry cultureEntry2 = cultureEntry1;
                if (cultureEntry2.King == null || cultureEntry2.Duke == null || cultureEntry2.Count == null || cultureEntry2.Baron == null || cultureEntry2.Governor == null)
                    throw new TitleDb.BadTitleDatabaseException("All title types must be defined for culture '" + str + "'!");
                if (string.IsNullOrWhiteSpace(cultureEntry2.King.Male) || string.IsNullOrWhiteSpace(cultureEntry2.Duke.Male) || string.IsNullOrWhiteSpace(cultureEntry2.Count.Male) || string.IsNullOrWhiteSpace(cultureEntry2.Baron.Male) || string.IsNullOrWhiteSpace(cultureEntry2.Governor.Male))
                    throw new TitleDb.BadTitleDatabaseException("Missing at least one male variant of a title type for culture '" + str + "'");
                if (string.IsNullOrWhiteSpace(cultureEntry2.King.Female))
                    cultureEntry2.King.Female = cultureEntry2.King.Male;
                if (string.IsNullOrWhiteSpace(cultureEntry2.Duke.Female))
                    cultureEntry2.Duke.Female = cultureEntry2.Duke.Male;
                if (string.IsNullOrWhiteSpace(cultureEntry2.Count.Female))
                    cultureEntry2.Count.Female = cultureEntry2.Count.Male;
                if (string.IsNullOrWhiteSpace(cultureEntry2.Baron.Female))
                    cultureEntry2.Baron.Female = cultureEntry2.Baron.Male;
                if (string.IsNullOrWhiteSpace(cultureEntry2.Governor.Female))
                    cultureEntry2.Governor.Female = cultureEntry2.Governor.Male;
                cultureEntry2.King.Male += " ";
                cultureEntry2.King.Female += " ";
                cultureEntry2.Duke.Male += " ";
                cultureEntry2.Duke.Female += " ";
                cultureEntry2.Count.Male += " ";
                cultureEntry2.Count.Female += " ";
                cultureEntry2.Baron.Male += " ";
                cultureEntry2.Baron.Female += " ";
                cultureEntry2.Governor.Male += " ";
                cultureEntry2.Governor.Female += " ";
                if (str == "default")
                    this.noCulture = cultureEntry2;

                Util.Log.Print($"Loaded culture {culture}");
                Util.Log.Print($"Loaded culture {cultureEntry2.King.Male}");
                Util.Log.Print($"Loaded culture {cultureEntry2.Duke.Male}");
                Util.Log.Print($"Loaded culture {cultureEntry2.Count.Male}");
                Util.Log.Print($"Loaded culture {cultureEntry2.Baron.Male}");
                Util.Log.Print($"Loaded culture {cultureEntry2.Governor.Male}");
            }
        }

        internal void Serialize()
        {
            foreach (TitleDb.CultureEntry cultureEntry in this.cultureMap.Values)
            {
                cultureEntry.King.Male = this.RmEndChar(cultureEntry.King.Male);
                cultureEntry.King.Female = this.RmEndChar(cultureEntry.King.Female);
                cultureEntry.Duke.Male = this.RmEndChar(cultureEntry.Duke.Male);
                cultureEntry.Duke.Female = this.RmEndChar(cultureEntry.Duke.Female);
                cultureEntry.Count.Male = this.RmEndChar(cultureEntry.Count.Male);
                cultureEntry.Count.Female = this.RmEndChar(cultureEntry.Count.Female);
                cultureEntry.Baron.Male = this.RmEndChar(cultureEntry.Baron.Male);
                cultureEntry.Baron.Female = this.RmEndChar(cultureEntry.Baron.Female);
                cultureEntry.Governor.Male = this.RmEndChar(cultureEntry.Governor.Male);
                cultureEntry.Governor.Female = this.RmEndChar(cultureEntry.Governor.Female);
            }
            File.WriteAllText(this.Path, JsonConvert.SerializeObject((object)this.cultureMap, Formatting.Indented));
        }

        private string RmEndChar(string s) => s.Substring(0, s.Length - 1);

        private string StripTitlePrefix(string s, string prefix) => !s.StartsWith(prefix) ? s : s.Remove(0, prefix.Length);

        protected string Path { get; set; }

        public class CultureEntry
        {
            public readonly TitleDb.Entry King;
            public readonly TitleDb.Entry Duke;
            public readonly TitleDb.Entry Count;
            public readonly TitleDb.Entry Baron;
            public readonly TitleDb.Entry Governor;

            public CultureEntry(
                TitleDb.Entry king,
                TitleDb.Entry duke,
                TitleDb.Entry count,
                TitleDb.Entry baron,
                TitleDb.Entry governor)
            {
                this.King = king;
                this.Duke = duke;
                this.Count = count;
                this.Baron = baron;
                this.Governor = governor;
            }
        }

        public class Entry
        {
            public string Male;
            public string Female;

            public Entry(string male, string female)
            {
                this.Male = male;
                this.Female = female;
            }
        }

        public class BadTitleDatabaseException : Exception
        {
            public BadTitleDatabaseException(string message)
              : base(message)
            {
            }

            public BadTitleDatabaseException()
            {
            }

            public BadTitleDatabaseException(string message, Exception innerException)
              : base(message, innerException)
            {
            }
        }
    }
}
