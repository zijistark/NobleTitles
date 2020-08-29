using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace NobleTitles
{
	class TitleBehavior : CampaignBehaviorBase
	{
		public override void RegisterEvents()
		{
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
			CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
		}

		public override void SyncData(IDataStore dataStore)
		{
			string dtKey = $"{SubModule.Name}DeadTitles";
			string svKey = $"{SubModule.Name}SaveVersion";

			if (hasLoaded)
			{
				// Serializing dead heroes' titles:
				savedDeadTitles = new Dictionary<uint, string>();

				foreach (var at in assignedTitles.Where(item => item.Key.IsDead))
					savedDeadTitles[at.Key.Id.InternalValue] = at.Value;

				string serialized = JsonConvert.SerializeObject(savedDeadTitles);
				savedDeadTitles = null;

				dataStore.SyncData(dtKey, ref serialized);
			}
			else
			{
				// Deserializing dead heroes' titles (will be applied in OnSessionLaunched):
				hasLoaded = true;

				string serialized = null;
				dataStore.SyncData(dtKey, ref serialized);

				if (serialized.IsStringNoneOrEmpty())
					return;

				savedDeadTitles = JsonConvert.DeserializeObject<Dictionary<uint, string>>(serialized);
			}

			// Serializing current savegame version:
			dataStore.SyncData(svKey, ref saveVersion);
		}

		protected void OnSessionLaunched(CampaignGameStarter starter)
		{
			hasLoaded = true; // Ensure any future SyncData call is interpreted as serialization

			// Fix old savegames that might have suffered from chained titles (fix only applies to living heroes):
			if (saveVersion < 1)
			{
				foreach (var hero in Hero.All.Where(h => h.IsAlive))
				{
					var name = hero.Name.ToString();
					var strippedName = titleDb.StripTitlePrefixes(hero);

					if (!strippedName.IsStringNoneOrEmpty() && !name.Equals(strippedName))
					{
						hero.Name = new TextObject(strippedName);
						RefreshPartyName(hero);
					}
				}
			}

			saveVersion = CurrentSaveVersion;

			// TODO: Use a new TitleDb method to strip ALL possible title prefixes from ALL living hero names
			// if the save hasn't been marked as upgraded from v1.0.1.

			AddTitlesToLivingHeroes();

			if (savedDeadTitles == null)
				return;

			foreach (var item in savedDeadTitles)
			{
				if (!(MBObjectManager.Instance.GetObject(new MBGUID(item.Key)) is Hero hero))
				{
					Util.Log.Print($">> ERROR: Hero ID lookup failed for hero {item.Key} with title {item.Value}");
					continue;
				}

				AddTitleToHero(hero, item.Value);
			}

			savedDeadTitles = null;
		}

		protected void OnDailyTick()
		{
			// Remove and unregister all titles from living heroes
			RemoveTitlesFromLivingHeroes();

			// Now add currently applicable titles to living heroes
			AddTitlesToLivingHeroes();
		}

		// Leave no trace in the save. Remove all titles from all heroes. Keep their assignment records.
		protected void OnBeforeSave() => RemoveTitlesFromHeroes();

		internal void OnAfterSave() // Called from a Harmony patch rather than event dispatch
		{
			// Restore all title prefixes to all heroes using the still-existing assignment records.
			foreach (var at in assignedTitles)
				AddTitleToHero(at.Key, at.Value, overrideTitle: true, registerTitle: false);
		}

		protected void AddTitlesToLivingHeroes()
		{
			// All living, titled heroes are associated with kingdoms for now, so go straight to the source
			Util.Log.Print("Adding kingdom-based noble titles...");

			foreach (var k in Kingdom.All.Where(x => !x.IsEliminated))
				AddTitlesToKingdomHeroes(k);
		}

		protected void AddTitlesToKingdomHeroes(Kingdom kingdom)
		{
			var tr = new List<string> { $"Adding noble titles to {kingdom.Name}..." };

			/* The vassals first...
			 *
			 * We consider all noble, active vassal clans and sort them by their "fief score" and, as a tie-breaker,
			 * their renown in ascending order (weakest -> strongest). For the fief score, 3 castles = 1 town.
			 * Finally, we select the ordered list of their leaders.
			 */

			var vassals = kingdom.Clans
				.Where(c =>
					c != kingdom.RulingClan &&
					!c.IsClanTypeMercenary &&
					!c.IsUnderMercenaryService &&
					c.Leader != null &&
					c.Leader.IsAlive &&
					c.Leader.IsNoble)
				.OrderBy(c => GetFiefScore(c))
				.ThenBy(c => c.Renown)
				.Select(c => c.Leader)
				.ToList();

			int nBarons = 0;

			// First, pass over all barons.
			foreach (var h in vassals)
			{
				// Are they a baron?
				if (GetFiefScore(h.Clan) < 3)
				{
					++nBarons;
					AssignRulerTitle(h, titleDb.GetBaronTitle(kingdom.Culture));
					tr.Add(GetHeroTrace(h, "BARON"));
				}
				else // They must be a count or duke. We're done here.
					break;
			}

			// The allowed number of dukes is a third of the total non-baron noble vassals.
			int nBigVassals = vassals.Count - nBarons;
			int nDukes = nBigVassals / 3; // Round down
			int nCounts = nBigVassals - nDukes;
			int maxDukeIdx = vassals.Count - 1;
			int maxCountIdx = maxDukeIdx - nDukes;
			int maxBaronIdx = maxCountIdx - nCounts;

			// Counts:
			for (int i = maxCountIdx; i > maxBaronIdx; --i)
			{
				AssignRulerTitle(vassals[i], titleDb.GetCountTitle(kingdom.Culture));
				tr.Add(GetHeroTrace(vassals[i], "COUNT"));
			}

			// Dukes:
			for (int i = maxDukeIdx; i > maxCountIdx; --i)
			{
				AssignRulerTitle(vassals[i], titleDb.GetDukeTitle(kingdom.Culture));
				tr.Add(GetHeroTrace(vassals[i], "DUKE"));
			}

			// Finally, the most obvious, the ruler (King) title:
			if (kingdom.Ruler != null &&
				!Kingdom.All.Where(k => k != kingdom).SelectMany(k => k.Lords).Where(h => h == kingdom.Ruler).Any()) // fix for stale ruler status in defunct kingdoms
			{
				AssignRulerTitle(kingdom.Ruler, titleDb.GetKingTitle(kingdom.Culture));
				tr.Add(GetHeroTrace(kingdom.Ruler, "KING"));
			}

			Util.Log.Print(tr);
		}

		protected string GetHeroTrace(Hero h, string rank) =>
			$" -> {rank}: {h.Name} [Fief Score: {GetFiefScore(h.Clan)} / Renown: {h.Clan.Renown:F0}]";

		protected int GetFiefScore(Clan clan) => clan.Fortifications.Sum(t => t.IsTown ? 3 : 1);

		protected void AssignRulerTitle(Hero hero, TitleDb.Entry title)
		{
			var titlePrefix = hero.IsFemale ? title.Female : title.Male;
			AddTitleToHero(hero, titlePrefix);

			// Should their spouse also get the same title (after gender adjustment)?
			// If the spouse is the leader of a clan (as we currently assume `hero` is a clan leader too,
			//     it'd also be a different clan) and that clan belongs to any kingdom, no.
			// Else, yes.

			var spouse = hero.Spouse;

			if (spouse == null ||
				spouse.IsDead ||
				(spouse.Clan?.Leader == spouse && spouse.Clan.Kingdom != null))
				return;

			// Sure. Give the spouse the ruler consort title, which is currently and probably always will
			// be the same as the ruler title, adjusted for gender.

			titlePrefix = spouse.IsFemale ? title.Female : title.Male;
			AddTitleToHero(spouse, titlePrefix);
		}

		protected void AddTitleToHero(Hero hero, string titlePrefix, bool overrideTitle = false, bool registerTitle = true)
		{
			if (assignedTitles.TryGetValue(hero, out string oldTitlePrefix))
			{
				if (overrideTitle && !titlePrefix.Equals(oldTitlePrefix))
					RemoveTitleFromHero(hero);
				else if (!overrideTitle)
				{
					Util.Log.Print($">> WARNING: Tried to add title \"{titlePrefix}\" to hero \"{hero.Name}\" with pre-assigned title \"{oldTitlePrefix}\"");
					return;
				}
			}

			if (registerTitle)
				assignedTitles[hero] = titlePrefix;

			hero.Name = new TextObject(titlePrefix + hero.Name.ToString());
			RefreshPartyName(hero);
		}

		protected void RemoveTitlesFromLivingHeroes(bool unregisterTitles = true)
		{
			foreach (var at in assignedTitles.Where(item => item.Key.IsAlive))
				RemoveTitleFromHero(at.Key, unregisterTitles);
		}

		protected void RemoveTitlesFromHeroes()
		{
			foreach (var at in assignedTitles)
				RemoveTitleFromHero(at.Key);
		}

		protected void RemoveTitleFromHero(Hero hero, bool unregisterTitle = false)
		{
			var name = hero.Name.ToString();
			var title = assignedTitles[hero];

			if (!name.StartsWith(title))
			{
				Util.Log.Print(">> WARNING: Expected title prefix not found in hero name! Title prefix: \"{title}\" | Name: \"{name}\"");
				return;
			}

			if (unregisterTitle)
				assignedTitles.Remove(hero);

			hero.Name = new TextObject(name.Remove(0, title.Length));
			RefreshPartyName(hero);
		}

		protected void RefreshPartyName(Hero hero)
		{
			var party = hero.PartyBelongedTo;

			if (party?.LeaderHero == hero)
				party.Name = MobilePartyHelper.GeneratePartyName(hero.CharacterObject);
		}

		private Dictionary<Hero, string> assignedTitles = new Dictionary<Hero, string>();

		private Dictionary<uint, string> savedDeadTitles; // Maps an MBGUID to a static title prefix for dead heroes, only used for (de)serialization

		private readonly TitleDb titleDb = new TitleDb();

		private bool hasLoaded = false; // If true, any SyncData call will be interpreted as serialization/saving

		private const int CurrentSaveVersion = 1;
		private int saveVersion = 0;
	}
}
