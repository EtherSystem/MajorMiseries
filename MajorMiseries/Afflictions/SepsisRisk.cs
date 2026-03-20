using static MajorMiseries.Afflictions.Sepsis;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class SepsisRisk
    {
        public class SepsisRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_SepsisRiskName";
            private const string CAUSE_KEY = "GAMEPLAY_SepsisCause";
            private const string DESC_KEY = "GAMEPLAY_SepsisRiskDescription";

            private const float RISK_PER_HOUR = 25f;

            private readonly AfflictionBodyArea m_BodyArea;
            private float m_RiskValue = 0f;
            private float m_LastUpdateTime;

            public InstanceType Type { get; set; } = InstanceType.Open;
            public bool Risk { get; set; } = true;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public SepsisRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "Major_Miseries.Resources.Icons.Afflictions.SepsisRisk.png", bodyArea, true)
            public SepsisRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_infectedWound", bodyArea)
            {
                m_BodyArea = bodyArea;
                m_LastUpdateTime = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is SepsisRiskAffliction sepsisRisk && sepsisRisk.m_BodyArea == m_BodyArea)
                {
                    sepsisRisk.ResetProgressTimer();
                }
            }

            public void CureSymptoms()
            {
                // Cure symptoms but not the affliction
            }

            public void OnCure()
            {
                // When the affliction is cured, apply this code
            }

            public float GetRiskValue() => m_RiskValue;

            public override void OnUpdate()
            {
                Panel_FirstAid firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
                if (firstAid != null && firstAid.isActiveAndEnabled)
                    return;

                if (!Risk)
                    return;

                if (SepsisAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                if (m_RiskValue >= 100f)
                {
                    Cure(false);
                    MelonCoroutines.Start(StartSepsisNextFrame());
                    return;
                }

                if (m_RiskValue < 0f)
                {
                    Cure();
                    return;
                }

                Infection infection = GameManager.GetInfectionComponent();
                if (infection == null)
                {
                    Cure();
                    return;
                }

                int infectionIndex = GetMatchingInfectionIndex(infection);
                if (infectionIndex < 0)
                {
                    int count = infection.GetAfflictionsCount();
                    Core.Log($"SepsisRisk [{m_BodyArea}] -> no matching vanilla infection, count={count}");

                    for (int i = 0; i < count; i++)
                    {
                        Core.Log($"  infection[{i}] location={infection.GetLocation(i)} antibiotics={infection.HasTakenAntibiotics(i)}");
                    }

                    Cure();
                    return;
                }

                if (infection.HasTakenAntibiotics(infectionIndex))
                {
                    Core.Log($"SepsisRisk [{m_BodyArea}] -> antibiotics taken, curing.");
                    Cure();
                    return;
                }

                UpdateRiskValue();
            }

            private int GetMatchingInfectionIndex(Infection infection)
            {
                int count = infection.GetAfflictionsCount();

                for (int i = 0; i < count; i++)
                {
                    if (infection.GetLocation(i) == m_BodyArea)
                        return i;
                }

                return -1;
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

            private IEnumerator StartSepsisNextFrame()
            {
                yield return null;
                new SepsisAffliction(m_BodyArea).Start();
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"SepsisRisk refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}