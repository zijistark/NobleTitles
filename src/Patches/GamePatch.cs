using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace NobleTitles.Patches
{
    [HarmonyPatch(typeof(Game), "Save")]
    internal sealed class GamePatch
    {
        private static void Postfix(MetaData metaData, ISaveDriver driver)
        {
            _ = (metaData, driver);
            Campaign.Current?.CampaignBehaviorManager.GetBehavior<TitleBehavior>()?.OnAfterSave();
        }
    }
}
