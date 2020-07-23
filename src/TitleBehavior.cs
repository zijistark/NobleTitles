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

			if (newlyDeadTitledHeroes.Count() > 0)
				liveTitles = liveTitles.Where(at => at.Hero.IsAlive).ToList();

			// remove all titles from living heroes
			RemoveTitlesFromHeroes();
			liveTitles.Clear();

			// now add currently applicable titles to living heroes
			AddTitlesToLivingHeroes();
		}

		protected void OnSessionLaunched(CampaignGameStarter starter)
		{

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
			// First, the most obvious, the ruler (king) title:
			if (kingdom.Ruler != null)
				AssignTitle(kingdom.Ruler, titleDb.GetKingTitlePrefix(kingdom.Culture, kingdom.Ruler.IsFemale));

			/* Now, for the vassals...
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
				.ThenBy(c => c.TotalStrength)
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
					AssignTitle(h, titleDb.GetBaronTitlePrefix(kingdom.Culture, h.IsFemale));
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

			// Dukes first.
			for (int i = maxDukeIdx; i > maxCountIdx; --i)
				AssignTitle(vassals[i], titleDb.GetDukeTitlePrefix(kingdom.Culture, vassals[i].IsFemale));

			// Then counts.
			for (int i = maxCountIdx; i > maxBaronIdx; --i)
				AssignTitle(vassals[i], titleDb.GetCountTitlePrefix(kingdom.Culture, vassals[i].IsFemale));
		}

		protected int GetFiefScore(Clan clan) => clan.Fortifications.Sum(t => t.IsTown ? 3 : 1);

		protected void AssignTitle(Hero hero, string titlePrefix)
		{
			var assignedTitle = new AssignedTitle(hero, titlePrefix);
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
