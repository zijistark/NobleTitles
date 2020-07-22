using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			// Leave no trace in the save. Remove all titles from all heroes. Keep their assignment records.
			RemoveTitlesFromHeroes(includeDeadHeroes: true);
		}

		internal void OnAfterSave()
		{
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
	}
}
