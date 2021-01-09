using HarmonyLib;

using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace NobleTitles.Patches
{
    internal sealed class SaveManagerPatch
    {
        private static readonly MethodInfo? TargetMethod = AccessTools.DeclaredMethod(typeof(SaveManager), "Save");
        private static readonly MethodInfo? PatchMethod = AccessTools.DeclaredMethod(typeof(SaveManagerPatch), nameof(SavePostfix));

        internal static bool Apply(Harmony harmony)
        {
            Util.Log.Print($"Attempting to apply patch: {nameof(SaveManagerPatch)}...");

            if (TargetMethod is null)
                Util.Log.Print($">> ERROR: {nameof(TargetMethod)} is null (missing)!");

            if (PatchMethod is null)
                Util.Log.Print($">> ERROR: {nameof(PatchMethod)} is null (missing)!");

            if (TargetMethod is null || PatchMethod is null)
            {
                Util.Log.Print(">> Aborting!");
                return false;
            }

            if (harmony.Patch(TargetMethod, postfix: new HarmonyMethod(PatchMethod)) is null)
            {
                Util.Log.Print(">> ERROR: Harmony failed to create patch!");
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SavePostfix() => Campaign.Current?.CampaignBehaviorManager.GetBehavior<TitleBehavior>()?.OnAfterSave();
    }
}
