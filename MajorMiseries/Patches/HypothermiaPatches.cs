using HarmonyLib;
using MajorMiseries.Managers;

namespace MajorMiseries.Patches
{
    [HarmonyPatch(typeof(Hypothermia), nameof(Hypothermia.Update))]
    internal static class HypothermiaBodyHeatSourcePatch
    {
        private readonly struct FreezingSnapshot
        {
            internal readonly Freezing? Component;
            internal readonly float CurrentFreezing;
            internal readonly float CurrentWarmingPerHour;
            internal readonly bool Applied;

            internal FreezingSnapshot(Freezing component)
            {
                Component = component;
                CurrentFreezing = component.m_CurrentFreezing;
                CurrentWarmingPerHour = component.m_CurrentWarmingPerHour;
                Applied = true;
            }
        }

        private static void Prefix(Hypothermia __instance, ref FreezingSnapshot __state)
        {
            bool bodyHeatSourceActive = ImmunityManager.IsBodyHeatHypothermiaSourceActive();
            bool recoveryBlocked = !ImmunityManager.IsBodyHeatHypothermiaRecoveryAllowed()
                && (__instance.HasHypothermiaRisk() || __instance.HasHypothermia());

            if (!bodyHeatSourceActive && !recoveryBlocked) return;

            Freezing? freezing = GameManager.GetFreezingComponent();
            if (freezing == null) return;

            __state = new FreezingSnapshot(freezing);
            freezing.m_CurrentFreezing = Mathf.Max(freezing.m_CurrentFreezing, Mathf.Max(1f, freezing.m_MaxFreezing));
            freezing.m_CurrentWarmingPerHour = 0f;
        }

        private static void Postfix(FreezingSnapshot __state)
        {
            if (!__state.Applied || __state.Component == null) return;

            __state.Component.m_CurrentFreezing = __state.CurrentFreezing;
            __state.Component.m_CurrentWarmingPerHour = __state.CurrentWarmingPerHour;
        }
    }

    [HarmonyPatch(typeof(Hypothermia), nameof(Hypothermia.SetWarmingUp))]
    internal static class HypothermiaBodyHeatRecoveryPatch
    {
        private static void Prefix(ref bool isWarming)
        {
            if (isWarming && !ImmunityManager.IsBodyHeatHypothermiaRecoveryAllowed()) isWarming = false;
        }
    }
}
