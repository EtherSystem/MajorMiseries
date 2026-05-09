using static MajorMiseries.Afflictions.CorpseSickness;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class CorpseSicknessRisk
    {
        public class CorpseSicknessRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_CorpseSicknessRiskName";
            private const string CAUSE_KEY = "GAMEPLAY_CorpseSicknessCause";
            private const string DESC_KEY = "GAMEPLAY_CorpseSicknessRiskDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.CorpseSickness.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.CorpseSickness_ALT.png";

            private const float CORPSE_SICKNESS_MIN_DURATION_HOURS = 48f;
            private const float CORPSE_SICKNESS_MAX_DURATION_HOURS = 96f;

            public static bool IsActive { get; private set; } = false;

            private float m_RiskValue = 0f;
            private float m_LastUpdateTime;

            public InstanceType Type { get; set; } = InstanceType.Single;
            public bool Risk { get; set; } = true;
            public bool DebugForced { get; set; } = false;
            public float DebugRiskValue { get; set; } = 50f;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            public CorpseSicknessRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                m_LastUpdateTime = tod != null ? tod.GetHoursPlayedNotPaused() : 0f;
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
            }

            public float GetRiskValue() => DebugForced ? DebugRiskValue : m_RiskValue;

            public override void OnUpdate()
            {
                IsActive = true;

                if (DebugForced)
                    return;

                if (!Settings.options.EnableCorpseSickness)
                {
                    Cure();
                    return;
                }

                if (!Risk)
                    return;

                if (CorpseSicknessAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                UpdateRiskValue();

                if (m_RiskValue >= 100f)
                {
                    Core.Log("CorpseSicknessRisk evolved into CorpseSickness.");
                    Cure(false);
                    MelonCoroutines.Start(StartCorpseSicknessNextFrame());
                    return;
                }

                if (!AfflictionLogic.IsPlayerNearCorpseSource() && m_RiskValue <= 0f)
                {
                    Core.Log("CorpseSicknessRisk fully decayed.");
                    Cure();
                    return;
                }
            }

            public void UpdateRiskValue()
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                float currentTime = tod.GetHoursPlayedNotPaused();
                float elapsedTime = currentTime - m_LastUpdateTime;

                if (elapsedTime <= 0f)
                    return;

                m_LastUpdateTime = currentTime;

                if (AfflictionLogic.IsPlayerNearCorpseSource())
                {
                    float riskIncrease = elapsedTime * AfflictionLogic.GetCorpseRiskGainPerHour();
                    m_RiskValue = Mathf.Min(m_RiskValue + riskIncrease, 100f);
                }
                else
                {
                    float riskDecrease = elapsedTime * AfflictionLogic.GetCorpseRiskDecayPerHour();
                    m_RiskValue = Mathf.Max(m_RiskValue - riskDecrease, 0f);
                }
            }

            private IEnumerator StartCorpseSicknessNextFrame()
            {
                yield return null;

                float durationHours = UnityEngine.Random.Range(
                    CORPSE_SICKNESS_MIN_DURATION_HOURS,
                    CORPSE_SICKNESS_MAX_DURATION_HOURS);

                Core.Log($"CorpseSicknessRisk evolved into CorpseSickness -> applying for {durationHours:0.##}h.");

                new CorpseSicknessAffliction(AfflictionBodyArea.Head, durationHours).Start();
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"CorpseSicknessRisk refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}