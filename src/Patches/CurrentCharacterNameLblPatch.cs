// Decompiled with JetBrains decompiler
// Type: NobleTitles.Patches.CurrentCharacterNameLblPatch
// Assembly: NobleTitles, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1ECF68F4-B6F2-4499-99A9-27E0EE6B0499
// Assembly location: G:\OneDrive - Mathis Consulting, LLC\Desktop\NobleTitles.dll

using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;


#nullable enable
namespace NobleTitles.Patches
{
  [HarmonyPatch(typeof (MissionConversationVM), "CurrentCharacterNameLbl", MethodType.Getter)]
  public class CurrentCharacterNameLblPatch
  {
    public static bool Prefix(ref string __result)
    {
      if (Hero.OneToOneConversationHero == null)
        return true;
      __result = Hero.OneToOneConversationHero.Name.ToString();
      return false;
    }
  }
}
