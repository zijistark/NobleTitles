// Decompiled with JetBrains decompiler
// Type: NobleTitles.TitleBehavior
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using Newtonsoft.Json;

using SandBox.Missions.MissionLogics;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;


#nullable enable
namespace NobleTitles
{
    internal sealed class TitleBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<Hero, string> assignedTitles = new Dictionary<Hero, string>();
        private readonly TitleDb titleDb = new TitleDb();
        private Dictionary<string, string>? savedDeadTitles;
        private int saveVersion = 0;
        private const int CurrentSaveVersion = 2;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener((object)this, new Action(this.OnDailyTick));
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnGameLoaded));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener((object)this, new Action(this.OnBeforeSave));
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener((object)this, new Action<bool, string>(this.OnAfterSave));
        }

        public override void SyncData(IDataStore dataStore)
        {
            string key1 = SubModule.Name + "DeadTitles";
            string key2 = SubModule.Name + "SaveVersion";
            dataStore.SyncData<int>(key2, ref this.saveVersion);
            if (dataStore.IsSaving)
            {
                this.savedDeadTitles = new Dictionary<string, string>();
                foreach (KeyValuePair<Hero, string> keyValuePair in this.assignedTitles.Where<KeyValuePair<Hero, string>>((Func<KeyValuePair<Hero, string>, bool>)(item => item.Key.IsDead)))
                    this.savedDeadTitles[keyValuePair.Key.StringId] = keyValuePair.Value;
                string data = JsonConvert.SerializeObject((object)this.savedDeadTitles);
                dataStore.SyncData<string>(key1, ref data);
                this.savedDeadTitles = (Dictionary<string, string>)null;
            }
            else if (this.saveVersion >= 2)
            {
                string data = (string)null;
                dataStore.SyncData<string>(key1, ref data);
                if (string.IsNullOrEmpty(data))
                    return;
                this.savedDeadTitles = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
            }
            else
                Util.Log.Print(string.Format("Savegame version of {0}: skipping deserialization of dead noble titles...", (object)this.saveVersion));
        }

        private void OnNewGameCreated(CampaignGameStarter starter) => Util.Log.Print(string.Format("Starting new campaign on {0} v{1} with savegame version of {2}...", (object)SubModule.Name, (object)SubModule.Version, (object)2));

        private void OnGameLoaded(CampaignGameStarter starter) => Util.Log.Print(string.Format("Loading campaign on {0} v{1} with savegame version of {2}...", (object)SubModule.Name, (object)SubModule.Version, (object)this.saveVersion));

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.saveVersion = 2;
            this.AddTitlesToLivingHeroes();
            if (this.savedDeadTitles == null)
                return;
            foreach (KeyValuePair<string, string> savedDeadTitle in this.savedDeadTitles)
            {
                Hero hero = Campaign.Current.CampaignObjectManager.Find<Hero>(savedDeadTitle.Key);
                if (hero == null)
                    Util.Log.Print(">> ERROR: Hero ID lookup failed for hero " + savedDeadTitle.Key + " with title " + savedDeadTitle.Value);
                else
                    this.AddTitleToHero(hero, savedDeadTitle.Value);
            }
            this.savedDeadTitles = (Dictionary<string, string>)null;
        }

        private void OnDailyTick()
        {
            this.RemoveTitlesFromLivingHeroes();
            this.AddTitlesToLivingHeroes();
        }

        private void OnBeforeSave()
        {
            Util.Log.Print("OnBeforeSave: Temporarily removing title prefixes from all heroes...");
            foreach (KeyValuePair<Hero, string> assignedTitle in this.assignedTitles)
                this.RemoveTitleFromHero(assignedTitle.Key, false);
        }

        internal void OnAfterSave(bool isSuccessful, string newSaveGameName)
        {
            Util.Log.Print("OnAfterSave: Restoring title prefixes to all heroes...");
            foreach (KeyValuePair<Hero, string> assignedTitle in this.assignedTitles)
                this.AddTitleToHero(assignedTitle.Key, assignedTitle.Value, true, false);
        }

        private void AddTitlesToLivingHeroes()
        {
            Util.Log.Print("Adding kingdom-based noble titles...");
            foreach (Kingdom kingdom in Kingdom.All.Where<Kingdom>((Func<Kingdom, bool>)(x => !x.IsEliminated)))
                this.AddTitlesToKingdomHeroes(kingdom);
        }

        private void AddTitlesToKingdomHeroes(Kingdom kingdom)
        {
            List<string> lines = new List<string>()
      {
        string.Format("Adding noble titles to {0}...", (object) kingdom.Name)
      };
            //List<Hero> list = kingdom.Clans.Where<Clan>((Func<Clan, bool>)(c => c != kingdom.RulingClan && !c.IsClanTypeMercenary && !c.IsUnderMercenaryService && c.Leader != null && c.Leader.IsAlive && c.Leader.Occupation == Occupation.Lord)).OrderBy<Clan, int>((Func<Clan, int>)(c => this.GetFiefScore(c))).ThenBy<Clan, float>((Func<Clan, float>)(c => c.Renown)).Select<Clan, Hero>((Func<Clan, Hero>)(c => c.Leader)).ToList<Hero>();

            var vassals = kingdom.Clans
               .Where(c =>
                   c != kingdom.RulingClan
                   && !c.IsClanTypeMercenary &&
                   !c.IsUnderMercenaryService
                   && c.Leader != null &&
                   c.Leader.IsAlive
                   && c.Leader.Occupation == Occupation.Lord)
               .OrderBy(c => GetFiefScore(c))
               .ThenBy(c => c.Renown)
               .Select(c => c.Leader)
               .ToList();

            int nBarons = 0;
            foreach (Hero hero in vassals)
            {
                if (this.GetFiefScore(hero.Clan) < 3)
                {
                    ++nBarons;
                    this.AssignRulerTitle(hero, this.titleDb.GetBaronTitle(kingdom.Culture));
                    lines.Add(this.GetHeroTrace(hero, "BARON"));
                }
                else
                    break;
            }
            //int num2 = vassals.Count - nBarons;
            //int num3 = num2 / 3;
            //int num4 = num2 - num3;
            //int num5 = vassals.Count - 1;
            //int num6 = num5 - num3;
            //int num7 = num6 - num4;

            int nBigVassals = vassals.Count - nBarons;
            int nDukes = nBigVassals / 3; // Round down
            int nCounts = nBigVassals - nDukes;
            int maxDukeIdx = vassals.Count - 1;
            int maxCountIdx = maxDukeIdx - nDukes;
            int maxBaronIdx = maxCountIdx - nCounts;


            for (int index = maxCountIdx; index > maxBaronIdx; --index)
            {
                this.AssignRulerTitle(vassals[index], this.titleDb.GetCountTitle(kingdom.Culture));
                lines.Add(this.GetHeroTrace(vassals[index], "COUNT"));
            }
            for (int index = maxDukeIdx; index > maxBaronIdx; --index)
            {
                this.AssignRulerTitle(vassals[index], this.titleDb.GetDukeTitle(kingdom.Culture));
                lines.Add(this.GetHeroTrace(vassals[index], "DUKE"));
            }
            if (kingdom?.RulingClan != null && !Kingdom.All.Where<Kingdom>((Func<Kingdom, bool>)(k => k != kingdom)).SelectMany<Kingdom, Hero>((Func<Kingdom, IEnumerable<Hero>>)(k => (IEnumerable<Hero>)k.Lords)).Where<Hero>((Func<Hero, bool>)(h => h == kingdom.RulingClan.Leader)).Any<Hero>())
            {
                this.AssignRulerTitle(kingdom.RulingClan.Leader, this.titleDb.GetKingTitle(kingdom.Culture));
                lines.Add(this.GetHeroTrace(kingdom.RulingClan.Leader, "KING"));
            }

            if (kingdom != null)
            {
                Util.Log.Print("Assigning Governors...");
                if (kingdom?.Fiefs != null && kingdom?.Fiefs.Count > 0)
                {
                    var governors = kingdom.Fiefs.Select(f => f.Governor).ToList();
                    foreach (var governor in governors)
                    {
                        if (governor != null)
                            if (!this.assignedTitles.Keys.Contains(governor))
                            {
                                try
                                {
                                    lines.Add("Adding fovernor titles.");
                                    this.AssignRulerTitle(governor, this.titleDb.GetGovernorTitle(kingdom.Culture));
                                }
                                catch (Exception e)
                                {
                                    lines.Add("Error adding governor title.");
                                    lines.Add(e.Message); break;
                                }
                            }
                    }
                }
                Util.Log.Print("Finished Governors...");
            }


            Util.Log.Print(lines);
        }

        private string GetHeroTrace(Hero h, string rank) => string.Format(" -> {0}: {1} [Fief Score: {2} / Renown: {3:F0}]", (object)rank, (object)h.Name, (object)this.GetFiefScore(h.Clan), (object)h.Clan.Renown);

        private int GetFiefScore(Clan clan) => clan.Fiefs.Sum(t => t.IsTown ? 3 : 1);

        private void AssignRulerTitle(Hero hero, TitleDb.Entry title)
        {
            string titlePrefix1 = hero.IsFemale ? title.Female : title.Male;
            this.AddTitleToHero(hero, titlePrefix1);
            Hero spouse = hero.Spouse;
            if (spouse == null || spouse.IsDead || spouse.Clan?.Leader == spouse && spouse.Clan.Kingdom != null)
                return;
            string titlePrefix2 = spouse.IsFemale ? title.Female : title.Male;
            this.AddTitleToHero(spouse, titlePrefix2);
        }

        private void AddTitleToHero(
          Hero hero,
          string titlePrefix,
          bool overrideTitle = false,
          bool registerTitle = true)
        {
            string str1;
            if (this.assignedTitles.TryGetValue(hero, out str1))
            {
                if (overrideTitle && !titlePrefix.Equals(str1))
                    this.RemoveTitleFromHero(hero);
                else if (!overrideTitle)
                {
                    Util.Log.Print(string.Format(">> WARNING: Tried to add title \"{0}\" to hero \"{1}\" with pre-assigned title \"{2}\"", (object)titlePrefix, (object)hero.Name, (object)str1));
                    return;
                }
            }
            if (registerTitle)
            {
                this.assignedTitles[hero] = titlePrefix;
            }

            string str2 = hero.Name.ToString();
            hero.SetName(new TextObject(titlePrefix + str2), new TextObject(str2));
        }

        private void RemoveTitlesFromLivingHeroes(bool unregisterTitles = true)
        {
            foreach (Hero hero in this.assignedTitles.Keys.Where<Hero>((Func<Hero, bool>)(h => h.IsAlive)).ToList<Hero>())
                this.RemoveTitleFromHero(hero, unregisterTitles);
        }

        private void RemoveTitleFromHero(Hero hero, bool unregisterTitle = true)
        {
            string str = hero.Name.ToString();
            string assignedTitle = this.assignedTitles[hero];
            if (!str.StartsWith(assignedTitle))
            {
                Util.Log.Print(">> WARNING: Expected title prefix not found in hero name when removing title! Title prefix: \"" + assignedTitle + "\" | Name: \"" + str + "\"");
            }
            else
            {
                if (unregisterTitle && assignedTitles.ContainsKey(hero))
                    this.assignedTitles.Remove(hero);
                hero.SetName(new TextObject(str.Remove(0, assignedTitle.Length)), new TextObject(hero.FirstName.ToString()));
            }
        }
    }
}
