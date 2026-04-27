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

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.BlackLungRisk.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.BlackLungRisk_ALT.png";

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

            //public BlackLungRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public BlackLungRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                m_LastUpdateTime = tod != null ? tod.GetHoursPlayedNotPaused() : 0f;
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

            public float GetRiskValue() => DebugForced ? DebugRiskValue : m_RiskValue;

            public override void OnUpdate()
            {
                IsActive = true;

                if (DebugForced)
                    return;

                if (!Settings.options.EnableBlackLung)
                {
                    Cure();
                    return;
                }

                if (!Risk)
                    return;

                if (BlackLungAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                UpdateRiskValue();

                if (m_RiskValue >= 100f)
                {
                    Core.Log("BlackLungRisk evolved into BlackLung.");
                    Cure(false);
                    MelonCoroutines.Start(StartBlackLungNextFrame());
                    return;
                }

                if (!AfflictionLogic.IsBlackLungScene(GameManager.m_ActiveScene) && m_RiskValue <= 0f)
                {
                    Core.Log("BlackLungRisk fully decayed.");
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

                bool inCoalScene = AfflictionLogic.IsBlackLungScene(GameManager.m_ActiveScene);

                if (inCoalScene)
                {
                    float riskIncrease = elapsedTime * AfflictionLogic.GetBlackLungRiskGainPerHour();
                    m_RiskValue = Mathf.Min(m_RiskValue + riskIncrease, 100f);
                }
                else
                {
                    float riskDecrease = elapsedTime * AfflictionLogic.GetBlackLungRiskDecayPerHour();
                    m_RiskValue = Mathf.Max(m_RiskValue - riskDecrease, 0f);
                }
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