using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

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
		}

		protected void OnDailyTick()
		{
			/* Handle titled heroes that have died since the last update.
			 * Shuffle the dead guys into deadTitles and the remainder into liveTitles. */
			var newlyDeadTitledHeroes = liveTitles.Where(at => at.Hero.IsDead);
			deadTitles.AddRange(newlyDeadTitledHeroes);

			if (newlyDeadTitledHeroes.Any())
				liveTitles = liveTitles.Where(at => at.Hero.IsAlive).ToList();

			// remove all titles from living heroes
			RemoveTitlesFromHeroes();
			liveTitles.Clear();

			// now add currently applicable titles to living heroes
			AddTitlesToLivingHeroes();
		}

		protected void OnSessionLaunched(CampaignGameStarter starter)
		{
			AddTitlesToLivingHeroes();
		}

		protected void OnBeforeSave()
		{
			Util.EventTracer.Trace();

			// Leave no trace in the save. Remove all titles from all heroes. Keep their assignment records.
			RemoveTitlesFromHeroes(includeDeadHeroes: true);
		}

		internal void OnAfterSave()
		{
			Util.EventTracer.Trace();

			// Restore all titles to all heroes using the still-existing assignment records.
			foreach (var at in liveTitles.Concat(deadTitles))
				AddTitleToHero(at);
		}

		protected void AddTitlesToLivingHeroes()
		{
			// all living, titled heroes are associated with kingdoms for now, so go straight to the source
			foreach (var k in Kingdom.All)
				AddTitlesToKingdomHeroes(k);
		}

		protected void AddTitlesToKingdomHeroes(Kingdom kingdom)
		{
			var tr = new List<string>
			{
				@"-----------------------------------------------------------------------------------\",
				$"Adding noble titles to {kingdom.Name}..."
			};

			/* The vassals...
			 *
			 * We consider all noble, active vassal clans and sort them by their "fief score" and, as a tie-breaker,
			 * their total strength in ascending order (weakest -> strongest). For the fief score, 3 castles = 1 town.
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

			tr.Add($"Total Vassals: {vassals.Count}");
			tr.Add($"Barons:        {nBarons} ({(float)nBarons / vassals.Count * 100:F0}%)");
			tr.Add($"Counts:        {nCounts} ({(float)nCounts / vassals.Count * 100:F0}%)");
			tr.Add($"Dukes:         {nDukes} ({(float)nDukes / vassals.Count * 100:F0}%)");
			Util.Log.Print(tr);
		}

		protected string GetHeroTrace(Hero h, string rank) =>
			$" -> {rank}: {h.Name} [Fief Score: {GetFiefScore(h.Clan)} // Renown: {h.Clan.Renown:F0}]";

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
			// be the same as the ruler title.

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
	}
}
