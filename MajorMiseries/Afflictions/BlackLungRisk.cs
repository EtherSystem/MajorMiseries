using static MajorMiseries.Afflictions.BlackLung;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class BlackLungRisk
    {
        public class BlackLungRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_BlackLungRiskName";
            private const string CAUSE_KEY = "GAMEPLAY_BlackLungRiskCause";
            private const string DESC_KEY = "GAMEPLAY_BlackLungRiskDescription";

            private const string ICON = "Major_Miseries.Resources.Icons.Afflictions.Classic.BlackLungRisk.png";
            private const string ALT_ICON = "Major_Miseries.Resources.Icons.Afflictions.Alt.BlackLungRisk_ALT.png";

            private const float RISK_PER_HOUR = 2f;

            public const float START_THRESHOLD = 75f;
            public const float CURE_THRESHOLD = 50f;

            public static bool IsActive { get; private set; } = false;

            private float m_RiskValue = 0f;
            private float m_LastUpdateTime;

            public InstanceType Type { get; set; } = InstanceType.Single;
            public bool Risk { get; set; } = true;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public BlackLungRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public BlackLungRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                m_LastUpdateTime = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
            }

            public void CureSymptoms()
            {
                // cure symptoms but not the affliction
            }

            public void OnCure()
            {
                IsActive = false;
            }

            public float GetRiskValue() => m_RiskValue;

            public override void OnUpdate()
            {
                IsActive = true;

                if (!Risk)
                    return;

                if (BlackLungAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                float exposure = Core.State?.BlackLungExposure ?? 0f;

                if (exposure < CURE_THRESHOLD)
                {
                    Core.Log($"BlackLungRisk cured naturally, exposure dropped to {exposure:0.##}.");
                    Cure();
                    return;
                }

                if (m_RiskValue >= 100f)
                {
                    Core.Log("BlackLungRisk evolved into BlackLung.");
                    Cure(false);
                    MelonCoroutines.Start(StartBlackLungNextFrame());
                    return;
                }

                if (m_RiskValue < 0f)
                {
                    Cure();
                    return;
                }

                UpdateRiskValue();
            }

            public void UpdateRiskValue()
            {
                float currentTime = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                float elapsedTime = currentTime - m_LastUpdateTime;

                if (elapsedTime <= 0f)
                    return;

                float riskIncrease = elapsedTime * RISK_PER_HOUR;
                m_RiskValue = Mathf.Min(m_RiskValue + riskIncrease, 100f);
                m_LastUpdateTime = currentTime;
            }

            private void ResetProgressTimer()
            {
                m_LastUpdateTime = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
            }

            private IEnumerator StartBlackLungNextFrame()
            {
                yield return null;

                float durationHours = Settings.options.BlackLungDurationMode == 1 ? 360f : 3600f;
                Core.Log($"BlackLungRisk evolved into BlackLung -> applying for {durationHours:0.##}h.");

                new BlackLungAffliction(AfflictionBodyArea.Head, durationHours).Start();
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"BlackLungRisk refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}