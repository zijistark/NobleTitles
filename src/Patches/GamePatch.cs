using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace NobleTitles.Patches
{
	[HarmonyPatch(typeof(Game), "Save")]
	class GamePatch
	{
		static void Postfix(MetaData metaData, ISaveDriver driver) =>
			Campaign.Current?.CampaignBehaviorManager.GetBehavior<TitleBehavior>().OnAfterSave();
	}
}
