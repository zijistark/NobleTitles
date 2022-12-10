// Decompiled with JetBrains decompiler
// Type: NobleTitles.SubModule
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using HarmonyLib;

using NobleTitles.Patches;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


#nullable enable
namespace NobleTitles
{
    public class SubModule : MBSubModuleBase
    {
        public const int SemVerMajor = 1;
        public const int SemVerMinor = 2;
        public const int SemVerPatch = 0;
        public static readonly string? SemVerSpecial = (string)null;
        private static readonly string SemVerEnd = SubModule.SemVerSpecial != null ? "-" + SubModule.SemVerSpecial : string.Empty;
        public static readonly string Version = string.Format("{0}.{1}.{2}{3}", (object)1, (object)2, (object)0, (object)SubModule.SemVerEnd);
        public static readonly string Name = typeof(SubModule).Namespace;
        public static readonly string DisplayName = "Noble Titles";
        public static readonly string HarmonyDomain = "com.zijistark.bannerlord." + SubModule.Name.ToLower();
        internal static readonly Color ImportantTextColor = Color.FromUint(15822118U);
        private bool hasLoaded;
        private bool canceled;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Util.EnableLog = true;
            Util.EnableTracer = true;
            //if (!SaveManagerPatch.Apply(new Harmony(SubModule.HarmonyDomain)))
            //{
            //  Util.Log.Print("Patch was required! Canceling " + SubModule.DisplayName + "...");
            //  this.canceled = true;
            //}
            Util.Log.Print("Patch start " + SubModule.DisplayName + "...");
            new Harmony("NobleTitles").PatchAll();
            Util.Log.Print("Patch end " + SubModule.DisplayName + "...");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Util.Log.Print("Init stasrt " + SubModule.DisplayName + "...");
            if (!this.hasLoaded && !this.canceled)
            {
                InformationManager.DisplayMessage(new InformationMessage("Loaded " + SubModule.DisplayName, SubModule.ImportantTextColor));
                this.hasLoaded = true;
            }
            if (!this.canceled)
                return;
            InformationManager.DisplayMessage(new InformationMessage("Error loading " + SubModule.DisplayName + ": Disabled!", SubModule.ImportantTextColor));
            Util.Log.Print("Init end " + SubModule.DisplayName + "...");
        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);
            Util.Log.Print("Game start " + SubModule.DisplayName + "...");
            if (this.canceled || !(game.GameType is Campaign))
                return;
            ((CampaignGameStarter)starterObject).AddBehavior((CampaignBehaviorBase)new TitleBehavior());

            Util.Log.Print("Game end" + SubModule.DisplayName + "...");
        }
    }
}
