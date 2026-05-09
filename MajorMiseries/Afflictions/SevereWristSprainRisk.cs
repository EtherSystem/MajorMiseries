using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class SevereWristSprainRisk
    {
        public class SevereWristSprainRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_SevereWristSprainRiskName";
            private const string CAUSE_KEY = "GAMEPLAY_SevereSprainCause";
            private const string DESC_KEY = "GAMEPLAY_SevereWristSprainRiskDescription";

            private float m_RiskValue = 0f;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;
            public bool Risk { get; set; } = true;
            public bool DebugForced { get; set; } = false;
            public float DebugRiskValue { get; set; } = 50f;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public bool InstantHeal { get; set; } = true;

            public SevereWristSprainRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, SevereWristSprain.SevereWristSprainAffliction.GetIcon(bodyArea), bodyArea, true)
            {
                UpdateRiskValue();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is SevereWristSprainRiskAffliction wristRisk)
                {
                    wristRisk.UpdateRiskValue();
                    wristRisk.ResetAffliction(resetRemedies: true);
                }
            }

            public float GetRiskValue()
            {
                return DebugForced ? DebugRiskValue : m_RiskValue;
            }

            public void UpdateRiskValue()
            {
                m_RiskValue = SevereSprainLogic.GetRiskValue(SevereSprainKind.Wrist, m_Location);
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
            }

            public override void OnUpdate()
            {
                if (DebugForced) return;

                if (!Settings.options.EnableSevereSprains)
                {
                    Cure();
                    return;
                }

                if (!Risk) return;

                UpdateRiskValue();

                if (SevereSprainLogic.HasSevereSprain(SevereSprainKind.Wrist, m_Location))
                {
                    Cure();
                    return;
                }

                if (GetRiskValue() <= 0f)
                {
                    Cure();
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"SevereWristSprainRisk refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
