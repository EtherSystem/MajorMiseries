using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class Fever
    {
        public class FeverAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_FeverName";
            private const string CAUSE_KEY = "GAMEPLAY_FeverCause";
            private const string DESC_KEY = "GAMEPLAY_FeverDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.Fever.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.Fever_ALT.png";

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            public FeverAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is FeverAffliction existing)
                    existing.ResetAffliction(resetRemedies: false);
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                IsActive = false;
            }

            public override void OnUpdate()
            {
                IsActive = true;

                if (!Settings.options.EnableImmunityShield)
                {
                    Cure();
                    return;
                }

                if (!ImmunityManager.ShouldFeverAfflictionBeVisible())
                {
                    Cure();
                    return;
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Fever refresh -> '{oldName}' => '{m_Name}'");
            }
        }

        [HarmonyPatch(typeof(Fatigue), nameof(Fatigue.CalculateFatigueIncrease))]
        private static class FeverFatiguePatch
        {
            private static void Postfix(ref float __result)
            {
                float multiplier = ImmunityManager.GetFeverFatigueMultiplier();
                if (multiplier <= 1f) return;

                __result *= multiplier;
            }
        }

        [HarmonyPatch(typeof(Thirst), nameof(Thirst.Update))]
        private static class BodyHeatHydrationPatch
        {
            private struct ThirstPatchState
            {
                public bool Applied;
                public float OriginalThirstIncreasePerDay;
                public float OriginalThirstIncreasePerDayWhenResting;
            }

            private static void Prefix(Thirst __instance, ref ThirstPatchState __state)
            {
                __state = default;

                if (__instance == null) return;

                float multiplier = ImmunityManager.GetBodyHeatHydrationMultiplier();
                if (multiplier <= 1f) return;

                __state.Applied = true;
                __state.OriginalThirstIncreasePerDay = __instance.m_ThirstIncreasePerDay;
                __state.OriginalThirstIncreasePerDayWhenResting = __instance.m_ThirstIncreasePerDayWhenResting;

                __instance.m_ThirstIncreasePerDay *= multiplier;
                __instance.m_ThirstIncreasePerDayWhenResting *= multiplier;
            }

            private static void Postfix(Thirst __instance, ThirstPatchState __state)
            {
                if (__instance == null || !__state.Applied) return;

                __instance.m_ThirstIncreasePerDay = __state.OriginalThirstIncreasePerDay;
                __instance.m_ThirstIncreasePerDayWhenResting = __state.OriginalThirstIncreasePerDayWhenResting;
            }
        }
    }
}