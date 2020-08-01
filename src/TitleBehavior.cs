using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
			CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, new Action<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>(OnHeroKilled));
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
			CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
		}

		public override void SyncData(IDataStore dataStore)
		{
			string dsKey = $"{SubModule.Name}DeadTitles";

			if (hasLoaded)
			{
				// Serializing dead heroes' titles
				savedDeadTitles = new Dictionary<uint, string>();

				foreach (var at in deadTitles)
					savedDeadTitles[at.Hero.Id.InternalValue] = at.TitlePrefix;

				string serialized = JsonConvert.SerializeObject(savedDeadTitles);
				savedDeadTitles = null;

				dataStore.SyncData(dsKey, ref serialized);
			}
			else
			{
				// Deserializing dead heroes' titles (will be applied in OnSessionLaunched)
				hasLoaded = true;

				string serialized = null;
				dataStore.SyncData(dsKey, ref serialized);

				if (serialized.IsStringNoneOrEmpty())
					return;

				savedDeadTitles = JsonConvert.DeserializeObject<Dictionary<uint, string>>(serialized);
			}
		}

		protected void OnSessionLaunched(CampaignGameStarter starter)
		{
			hasLoaded = true; // Ensure any future SyncData call is interpreted as serialization
			AddTitlesToLivingHeroes();

			if (savedDeadTitles == null)
				return;

			foreach (var item in savedDeadTitles)
			{
				if (!(MBObjectManager.Instance.GetObject(new MBGUID(item.Key)) is Hero hero))
				{
					Util.Log.Print($"ERROR: Hero ID lookup failed for hero {item.Key} with title {item.Value}");
					continue;
				}

				var at = new AssignedTitle(hero, item.Value);
				AddTitleToHero(at);
				deadTitles.Add(at);
			}

			savedDeadTitles = null;
		}

		protected void OnHeroKilled(Hero victim, Hero killer,
			KillCharacterAction.KillCharacterActionDetail detail, bool showNotification) => HandleNewlyDeadHeroes();

		protected void OnDailyTick()
		{
			// Ensure dead, titled heroes are moved to the deadTitles list
			HandleNewlyDeadHeroes();

			// Remove all titles from living heroes
			RemoveTitlesFromHeroes();
			liveTitles.Clear();

			// Now add currently applicable titles to living heroes
			AddTitlesToLivingHeroes();
		}

		// Leave no trace in the save. Remove all titles from all heroes. Keep their assignment records.
		protected void OnBeforeSave() => RemoveTitlesFromHeroes(includeDeadHeroes: true);

		internal void OnAfterSave() // called from a Harmony patch rather than event dispatch
		{
			// Restore all titles to all heroes using the still-existing assignment records.
			foreach (var at in liveTitles.Concat(deadTitles))
				AddTitleToHero(at);
		}

		/* Handle titled heroes that have died since the last update.
		 * Shuffle the dead guys into deadTitles and the remainder into liveTitles. */
		protected void HandleNewlyDeadHeroes()
		{
			var newlyDeadTitledHeroes = liveTitles.Where(at => at.Hero.IsDead);

			if (newlyDeadTitledHeroes.Any())
			{
				deadTitles.AddRange(newlyDeadTitledHeroes);
				liveTitles = liveTitles.Where(at => at.Hero.IsAlive).ToList();
			}
		}

		protected void AddTitlesToLivingHeroes()
		{
			// All living, titled heroes are associated with kingdoms for now, so go straight to the source
			foreach (var k in Kingdom.All)
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
			if (kingdom.Ruler != null)
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
			var assignedTitle = new AssignedTitle(hero, hero.IsFemale ? title.Female : title.Male);
			liveTitles.Add(assignedTitle);
			AddTitleToHero(assignedTitle);

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

			assignedTitle = new AssignedTitle(spouse, spouse.IsFemale ? title.Female : title.Male);
			liveTitles.Add(assignedTitle);
			AddTitleToHero(assignedTitle);
		}

		protected void AddTitleToHero(AssignedTitle assignedTitle)
		{
			assignedTitle.Hero.Name = new TextObject(assignedTitle.TitlePrefix + assignedTitle.Hero.Name.ToString());
			RefreshPartyName(assignedTitle.Hero);
		}

		protected void RemoveTitlesFromHeroes(bool includeDeadHeroes = false)
		{
			foreach (var lt in liveTitles)
				RemoveTitleFromHero(lt);

			if (includeDeadHeroes)
				foreach (var dt in deadTitles)
					RemoveTitleFromHero(dt);
		}

		protected void RemoveTitleFromHero(AssignedTitle assignedTitle)
		{
			var name = assignedTitle.Hero.Name.ToString();

			if (!name.StartsWith(assignedTitle.TitlePrefix))
				return;

			assignedTitle.Hero.Name = new TextObject(name.Remove(0, assignedTitle.TitlePrefix.Length));
			RefreshPartyName(assignedTitle.Hero);
		}

		protected void RefreshPartyName(Hero hero)
		{
			var party = hero.PartyBelongedTo;

			if (party?.LeaderHero == hero)
				party.Name = MobilePartyHelper.GeneratePartyName(hero.CharacterObject);
		}

		protected class AssignedTitle
		{
			public readonly Hero Hero;
			public readonly string TitlePrefix;

			public AssignedTitle(Hero hero, string titlePrefix)
			{
				Hero = hero;
				TitlePrefix = titlePrefix;
			}
		}

		private List<AssignedTitle> liveTitles = new List<AssignedTitle>();
		private List<AssignedTitle> deadTitles = new List<AssignedTitle>();

		private readonly TitleDb titleDb = new TitleDb();

		private Dictionary<uint, string> savedDeadTitles; // Maps an MBGUID to a static title prefix for dead heroes, only used for (de)serialization

		private bool hasLoaded = false; // If true, any SyncData call will be interpreted as serialization/saving
	}
}
