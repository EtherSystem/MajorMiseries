using MajorMiseries.Managers;

namespace MajorMiseries.Patches
{
    internal static class AuroraInfluencePatches
    {
        [HarmonyPatch(typeof(ResearchItem), nameof(ResearchItem.Read), typeof(float))]
        internal static class ResearchReadPatch
        {
            private static void Prefix(ref float timeOfDayHours)
            {
                float before = timeOfDayHours;
                float actionTimeMultiplier = AuroraInfluenceManager.GetActionTimeMultiplier();
                if (actionTimeMultiplier <= 1f) return;

                float progressMultiplier = AuroraInfluenceManager.GetResearchProgressMultiplier();
                timeOfDayHours *= progressMultiplier;

                if (!Mathf.Approximately(before, timeOfDayHours))
                {
                    AuroraInfluenceManager.LogEffectDebug(
                        "Research",
                        $"Book progress modified -> elapsed:{before:0.###}h * progress x{progressMultiplier:0.###} = {timeOfDayHours:0.###}h book progress | FullReadDuration:x{actionTimeMultiplier:0.###}"
                    );
                }
            }
        }

        private struct BreakdownTimeState
        {
            public float BaseHours;
            public float BaseSeconds;
            public float AppliedHours;
            public float AppliedSeconds;
            public float Multiplier;
        }

        private static readonly Dictionary<int, BreakdownTimeState> s_BreakdownTimeStates = [];

        [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.GetModifiedActionDuration))]
        internal static class InventoryRepairDurationPatch
        {
            private static void Postfix(Panel_Inventory_Examine __instance, ref int __result)
            {
                if (!IsInventoryRepairContext(__instance)) return;

                ApplyAuroraActionTimeToMinutes(ref __result, "RepairDuration");
            }
        }

        [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.UpdateDurationLabel))]
        internal static class BreakdownDurationLabelPatch
        {
            private static void Prefix(Panel_BreakDown __instance)
            {
                ApplyAuroraBreakdownDisplayMultiplier(__instance);
            }

            private static void Postfix(Panel_BreakDown __instance)
            {
                ApplyAuroraBreakdownDisplayMultiplier(__instance);
            }
        }

        [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.OnBreakDown))]
        internal static class BreakdownStartPatch
        {
            private static void Prefix(Panel_BreakDown __instance)
            {
                ApplyAuroraBreakdownTimeMultiplier(__instance, "breakdown start");
            }
        }

        [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.BreakDownFinished))]
        internal static class BreakdownFinishedPatch
        {
            private static void Postfix(Panel_BreakDown __instance)
            {
                RestoreAuroraBreakdownTime(__instance);
            }
        }

        [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.OnCancel))]
        internal static class BreakdownCancelPatch
        {
            private static void Prefix(Panel_BreakDown __instance)
            {
                RestoreAuroraBreakdownTime(__instance);
            }
        }

        [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.Enable))]
        internal static class BreakdownEnablePatch
        {
            private static void Prefix(Panel_BreakDown __instance, bool enable)
            {
                if (enable || __instance == null) return;
                RestoreAuroraBreakdownTime(__instance);
            }
        }

        private static bool IsInventoryRepairContext(Panel_Inventory_Examine panel)
        {
            if (panel == null) return false;
            if (panel.m_RepairInProgress) return true;
            if (panel.m_RepairPanel != null && panel.m_RepairPanel.activeInHierarchy) return true;
            if (panel.m_SafehouseCustomizationRepairPanel != null && panel.m_SafehouseCustomizationRepairPanel.activeInHierarchy) return true;

            return false;
        }

        private static void ApplyAuroraActionTimeToMinutes(ref int durationMinutes, string effectName)
        {
            int before = durationMinutes;
            float multiplier = AuroraInfluenceManager.GetActionTimeMultiplier();
            if (multiplier <= 1f) return;

            durationMinutes = Mathf.CeilToInt(durationMinutes * multiplier);

            if (before != durationMinutes)
            {
                AuroraInfluenceManager.LogEffectDebug(effectName, $"Duration modified -> {before}min * x{multiplier:0.###} = {durationMinutes}min");
            }
        }

        private static void ApplyAuroraBreakdownDisplayMultiplier(Panel_BreakDown panel)
        {
            if (panel == null || panel.m_IsBreakingDown) return;

            float multiplier = AuroraInfluenceManager.GetActionTimeMultiplier();
            if (multiplier <= 1f)
            {
                RestoreAuroraBreakdownTime(panel);
                return;
            }

            int key = panel.GetInstanceID();
            float currentHours = panel.m_DurationHours;
            float currentSeconds = panel.m_SecondsToBreakDown;

            if (s_BreakdownTimeStates.TryGetValue(key, out BreakdownTimeState state))
            {
                bool currentHoursAreApplied = Mathf.Approximately(currentHours, state.AppliedHours);
                bool currentHoursAreBase = Mathf.Approximately(currentHours, state.BaseHours);

                if (!currentHoursAreApplied && !currentHoursAreBase && currentHours > 0f)
                {
                    state.BaseHours = currentHours;
                    state.BaseSeconds = currentSeconds;
                    state.AppliedHours = currentHours * multiplier;
                    state.AppliedSeconds = currentSeconds > 0f ? currentSeconds * multiplier : 0f;
                    state.Multiplier = multiplier;
                    s_BreakdownTimeStates[key] = state;
                }
                else if (!Mathf.Approximately(multiplier, state.Multiplier))
                {
                    state.AppliedHours = state.BaseHours > 0f ? state.BaseHours * multiplier : 0f;
                    state.AppliedSeconds = state.BaseSeconds > 0f ? state.BaseSeconds * multiplier : 0f;
                    state.Multiplier = multiplier;
                    s_BreakdownTimeStates[key] = state;
                }

                if (state.AppliedHours > 0f) panel.m_DurationHours = state.AppliedHours;
                return;
            }

            if (currentHours <= 0f && currentSeconds <= 0f) return;

            float appliedHours = currentHours > 0f ? currentHours * multiplier : 0f;
            float appliedSeconds = currentSeconds > 0f ? currentSeconds * multiplier : 0f;

            s_BreakdownTimeStates[key] = new BreakdownTimeState
            {
                BaseHours = currentHours,
                BaseSeconds = currentSeconds,
                AppliedHours = appliedHours,
                AppliedSeconds = appliedSeconds,
                Multiplier = multiplier
            };

            if (appliedHours > 0f) panel.m_DurationHours = appliedHours;
        }

        private static void ApplyAuroraBreakdownTimeMultiplier(Panel_BreakDown panel, string reason)
        {
            if (panel == null || panel.m_IsBreakingDown) return;

            float multiplier = AuroraInfluenceManager.GetActionTimeMultiplier();
            if (multiplier <= 1f) return;

            int key = panel.GetInstanceID();
            float currentHours = panel.m_DurationHours;
            float currentSeconds = panel.m_SecondsToBreakDown;

            if (!s_BreakdownTimeStates.TryGetValue(key, out BreakdownTimeState state))
            {
                float baseHours = currentHours;
                float baseSeconds = currentSeconds;

                if (baseHours <= 0f && baseSeconds <= 0f) return;

                state = new BreakdownTimeState
                {
                    BaseHours = baseHours,
                    BaseSeconds = baseSeconds,
                    AppliedHours = baseHours > 0f ? baseHours * multiplier : 0f,
                    AppliedSeconds = baseSeconds > 0f ? baseSeconds * multiplier : 0f,
                    Multiplier = multiplier
                };
            }
            else if (!Mathf.Approximately(multiplier, state.Multiplier))
            {
                state.AppliedHours = state.BaseHours > 0f ? state.BaseHours * multiplier : 0f;
                state.AppliedSeconds = state.BaseSeconds > 0f ? state.BaseSeconds * multiplier : 0f;
                state.Multiplier = multiplier;
            }

            bool alreadyFullyApplied = Mathf.Approximately(currentHours, state.AppliedHours) && Mathf.Approximately(currentSeconds, state.AppliedSeconds);
            if (alreadyFullyApplied) return;

            panel.m_DurationHours = state.AppliedHours;
            panel.m_SecondsToBreakDown = state.AppliedSeconds;
            s_BreakdownTimeStates[key] = state;

            AuroraInfluenceManager.LogEffectDebug(
                "BreakdownDuration",
                $"Duration modified -> Reason:{reason} | Hours:{state.BaseHours:0.###}h * x{state.Multiplier:0.###} = {state.AppliedHours:0.###}h | Seconds:{state.BaseSeconds:0.###}s -> {state.AppliedSeconds:0.###}s"
            );
        }

        private static void RestoreAuroraBreakdownTime(Panel_BreakDown panel)
        {
            if (panel == null) return;

            int key = panel.GetInstanceID();
            if (!s_BreakdownTimeStates.TryGetValue(key, out BreakdownTimeState state)) return;

            if (Mathf.Approximately(panel.m_DurationHours, state.AppliedHours)) panel.m_DurationHours = state.BaseHours;
            if (Mathf.Approximately(panel.m_SecondsToBreakDown, state.AppliedSeconds)) panel.m_SecondsToBreakDown = state.BaseSeconds;

            s_BreakdownTimeStates.Remove(key);
        }

        [HarmonyPatch(typeof(FireManager), nameof(FireManager.CalculateFireStartSuccess))]
        internal static class FireStartChancePatch
        {
            private static void Postfix(ref float __result)
            {
                float before = __result;
                float penalty = AuroraInfluenceManager.GetChancePenaltyPercent();
                __result = Mathf.Clamp(__result - penalty, 0f, 100f);

                if (!Mathf.Approximately(before, __result))
                {
                    AuroraInfluenceManager.LogEffectDebug("FireStartChance", $"Chance modified -> {before:0.##}% - {penalty:0.##}% = {__result:0.##}%");
                }
            }
        }


        [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.StartRest))]
        internal static class NightTerrorBlockRestPatch
        {
            private static bool Prefix()
            {
                if (!AuroraInfluenceManager.ShouldBlockSleepAfterNightTerror(out int minutesRemaining)) return true;

                HUDMessage.AddMessage(string.Format(Localization.Get("GAMEPLAY_AuroraInfluenceNightTerrorSleepBlocked"), minutesRemaining), AuroraInfluenceManager.AURORA_INFLUENCE_HUD_DISPLAY_SECONDS, false);
                AuroraInfluenceManager.LogNightTerrorSleepBlocked(minutesRemaining);
                return false;
            }
        }
    }
}