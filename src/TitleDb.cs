using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace NobleTitles
{
    class TitleDb
    {
        internal Entry GetKingTitle(CultureObject culture) =>
            culture is null || !cultureMap.TryGetValue(culture.StringId, out var culEntry) ? noCulture.King : culEntry.King;

        internal Entry GetDukeTitle(CultureObject culture) =>
            culture is null || !cultureMap.TryGetValue(culture.StringId, out var culEntry) ? noCulture.Duke : culEntry.Duke;

        internal Entry GetCountTitle(CultureObject culture) =>
            culture is null || !cultureMap.TryGetValue(culture.StringId, out var culEntry) ? noCulture.Count : culEntry.Count;

        internal Entry GetBaronTitle(CultureObject culture) =>
            culture is null || !cultureMap.TryGetValue(culture.StringId, out var culEntry) ? noCulture.Baron : culEntry.Baron;

        internal string StripTitlePrefixes(Hero hero)
        {
            var prevName = hero.Name.ToString();
            var newName = prevName;

            while (true)
            {
                foreach (var ce in cultureMap.Values)
                {
                    if (hero.IsFemale)
                    {
                        newName = StripTitlePrefix(newName, ce.King.Female);
                        newName = StripTitlePrefix(newName, ce.Duke.Female);
                        newName = StripTitlePrefix(newName, ce.Count.Female);
                        newName = StripTitlePrefix(newName, ce.Baron.Female);
                    }
                    else
                    {
                        newName = StripTitlePrefix(newName, ce.King.Male);
                        newName = StripTitlePrefix(newName, ce.Duke.Male);
                        newName = StripTitlePrefix(newName, ce.Count.Male);
                        newName = StripTitlePrefix(newName, ce.Baron.Male);
                    }
                }

                // For compatibility with savegame version 0, pre-1.1.0, as these titles left the default config:
                newName = StripTitlePrefix(newName, "Great Khan ");
                newName = StripTitlePrefix(newName, "Great Khanum ");

                if (prevName.Equals(newName)) // Made no progress, so we're done
                    return newName;
                else
                    prevName = newName;
            }
        }

        internal TitleDb()
        {
            Path = BasePath.Name + $"Modules/{SubModule.Name}/titles.json";

            cultureMap = JsonConvert.DeserializeObject<Dictionary<string, CultureEntry>>(
                File.ReadAllText(Path),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace })
                ?? throw new BadTitleDatabaseException("Failed to deserialize title database!");

            if (cultureMap.Count == 0)
                throw new BadTitleDatabaseException("Title database is empty!");

            // Must have a fallback culture entry.
            if (!cultureMap.ContainsKey("default"))
                throw new BadTitleDatabaseException("Title database must contain a fallback culture entry keyed by \"default\"!");

            foreach (var i in cultureMap)
            {
                var (cul, entry) = (i.Key, i.Value);

                if (entry.King is null || entry.Duke is null || entry.Count is null || entry.Baron is null)
                    throw new BadTitleDatabaseException($"All title types must be defined for culture '{cul}'!");

                if (string.IsNullOrWhiteSpace(entry.King.Male) || string.IsNullOrWhiteSpace(entry.Duke.Male) ||
                    string.IsNullOrWhiteSpace(entry.Count.Male) || string.IsNullOrWhiteSpace(entry.Baron.Male))
                    throw new BadTitleDatabaseException($"Missing at least one male variant of a title type for culture '{cul}'");

                // Missing feminine titles default to equivalent masculine/neutral titles:
                if (string.IsNullOrWhiteSpace(entry.King.Female)) entry.King.Female = entry.King.Male;
                if (string.IsNullOrWhiteSpace(entry.Duke.Female)) entry.Duke.Female = entry.Duke.Male;
                if (string.IsNullOrWhiteSpace(entry.Count.Female)) entry.Count.Female = entry.Count.Male;
                if (string.IsNullOrWhiteSpace(entry.Baron.Female)) entry.Baron.Female = entry.Baron.Male;

                // Commonly, we want the full title prefix, i.e. including the trailing space, so we just use
                // such strings natively instead of constantly doing string creation churn just to append a space:
                entry.King.Male += " ";
                entry.King.Female += " ";
                entry.Duke.Male += " ";
                entry.Duke.Female += " ";
                entry.Count.Male += " ";
                entry.Count.Female += " ";
                entry.Baron.Male += " ";
                entry.Baron.Female += " ";

                if (cul == "default")
                    noCulture = entry;
            }
        }

        internal void Serialize()
        {
            // Undo our baked-in trailing space
            foreach (var e in cultureMap.Values)
            {
                e.King.Male = RmEndChar(e.King.Male);
                e.King.Female = RmEndChar(e.King.Female);
                e.Duke.Male = RmEndChar(e.Duke.Male);
                e.Duke.Female = RmEndChar(e.Duke.Female);
                e.Count.Male = RmEndChar(e.Count.Male);
                e.Count.Female = RmEndChar(e.Count.Female);
                e.Baron.Male = RmEndChar(e.Baron.Male);
                e.Baron.Female = RmEndChar(e.Baron.Female);
            }

            File.WriteAllText(Path, JsonConvert.SerializeObject(cultureMap, Formatting.Indented));
        }

        private string RmEndChar(string s) => s.Substring(0, s.Length - 1);

        private string StripTitlePrefix(string s, string prefix) => s.StartsWith(prefix) ? s.Remove(0, prefix.Length) : s;

        public class CultureEntry
        {
            public readonly Entry King;
            public readonly Entry Duke;
            public readonly Entry Count;
            public readonly Entry Baron;

            public CultureEntry(Entry king, Entry duke, Entry count, Entry baron)
            {
                King = king;
                Duke = duke;
                Count = count;
                Baron = baron;
            }
        }

        public class Entry
        {
            public string Male;
            public string Female;

            public Entry(string male, string female)
            {
                Male = male;
                Female = female;
            }
        }

        public class BadTitleDatabaseException : Exception
        {
            public BadTitleDatabaseException(string message) : base(message) { }

            public BadTitleDatabaseException() { }

            public BadTitleDatabaseException(string message, Exception innerException) : base(message, innerException) { }
        }

        protected string Path { get; set; }

        // culture StringId => CultureEntry (contains bulk of title information, only further split by gender)
        protected Dictionary<string, CultureEntry> cultureMap;

        protected CultureEntry noCulture = new(new("King", "Queen"), new("Duke", "Duchess"), new("Count", "Countess"), new("Baron", "Baroness"));
    }
}
