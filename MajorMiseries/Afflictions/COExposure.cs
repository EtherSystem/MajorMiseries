using static MajorMiseries.Afflictions.COPoisoning;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class COExposure
    {
        public class COExposureAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_COExposureName";
            private const string CAUSE_KEY = "GAMEPLAY_COExposureCause";
            private const string DESC_KEY = "GAMEPLAY_COExposureDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.COExposure.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.COExposure_ALT.png";

            private const float TIME_TO_CO_POISONING_HOURS = 30f / 60f; // 30 in-game minutes
            private const float CO_POISONING_MIN_DURATION_HOURS = 6f;
            private const float CO_POISONING_MAX_DURATION_HOURS = 24f;

            public static bool IsActive { get; private set; } = false;

            private float m_RiskValue = 0f;
            private float m_LastUpdateTime;

            public InstanceType Type { get; set; } = InstanceType.Single;
            public bool Risk { get; set; } = true;
            public bool DebugForced { get; set; } = false;
            public float DebugRiskValue { get; set; } = 50f;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;

            public COExposureAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                m_LastUpdateTime = GameManager.GetTimeOfDayComponent()?.GetHoursPlayedNotPaused() ?? 0f;
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                IsActive = false;
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public float GetRiskValue() => DebugForced ? DebugRiskValue : m_RiskValue;

            public override void OnUpdate()
            {
                IsActive = true;

                if (DebugForced)
                    return;

                if (!Settings.options.EnableCarbonMonoxide)
                {
                    Cure();
                    return;
                }

                if (!Risk)
                    return;

                if (COPoisoningAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                if (!AfflictionLogic.IsPlayerStillInActiveCOScene())
                {
                    AfflictionLogic.ResetCORespiratorProtectionState();
                    Core.Log("COExposure cured because player is no longer in an active CO-contaminated scene.");
                    Cure();
                    return;
                }

                UpdateRiskValue();

                if (m_RiskValue >= 100f)
                {
                    Core.Log("COExposure matured into COPoisoning.");
                    Cure(false);
                    MelonCoroutines.Start(StartCOPoisoningNextFrame);
                }
            }

            public void UpdateRiskValue()
            {
                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return;

                float nowHours = tod.GetHoursPlayedNotPaused();
                float elapsed = nowHours - m_LastUpdateTime;

                if (elapsed <= 0f) return;

                m_LastUpdateTime = nowHours;

                string sceneName = GameManager.m_ActiveScene;
                if (string.IsNullOrEmpty(sceneName)) return;

                float unprotectedElapsed = AfflictionLogic.UpdateCORespiratorProtectionAndGetUnprotectedHours(elapsed, sceneName);

                if (unprotectedElapsed <= 0f) return;

                float timeToPoisoningHours = Mathf.Max(0.01f, AfflictionLogic.GetCOExposureTimeToPoisoningHours());
                float riskIncrease = (unprotectedElapsed / timeToPoisoningHours) * 100f;
                m_RiskValue = Mathf.Min(m_RiskValue + riskIncrease, 100f);
            }

            private static IEnumerator StartCOPoisoningNextFrame
            {
                get
                {
                    yield return null;

                    float durationHours = UnityEngine.Random.Range(CO_POISONING_MIN_DURATION_HOURS, CO_POISONING_MAX_DURATION_HOURS);
                    Core.Log($"COExposure matured into COPoisoning -> rolled duration {durationHours:0.##}h.");

                    new COPoisoningAffliction(AfflictionBodyArea.Head, durationHours).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"COExposure refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}