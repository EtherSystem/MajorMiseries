using Il2CppTLD.BigCarry;

namespace MajorMiseries.Patches
{
    internal static class TravoisPatches
    {
        [HarmonyPatch(typeof(TravoisBigCarryItem), nameof(TravoisBigCarryItem.CarryCallback))]
        private static class TravoisBigCarryItem_CarryCallback
        {
            private static bool Prefix()
            {
                if (!Core.IsGameplayEnabled || !AfflictionLogic.ShouldBlockTravoisUse()) return true;

                GameAudioManager.PlayGUIError();
                HUDMessage.AddMessage(Localization.Get(AfflictionLogic.GetTravoisUseBlockedMessageKey()), 4, false);
                return false;
            }
        }
    }
}