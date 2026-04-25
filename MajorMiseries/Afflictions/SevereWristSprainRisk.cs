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

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.SevereWristSprainRisk.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.SevereWristSprainRisk_ALT.png";

            private float m_RiskValue = 0f;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;
            public bool Risk { get; set; } = true;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public bool InstantHeal { get; set; } = true;

            //public SevereWristSprainRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public SevereWristSprainRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_sprainedWrist", bodyArea)
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
                return m_RiskValue;
            }

            public void UpdateRiskValue()
            {
                m_RiskValue = SevereSprainLogic.GetRiskValue(SevereSprainKind.Wrist, m_Location);
            }

            public void CureSymptoms()
            {
                // Risk affliction has no symptoms to cure.
            }

            public void OnCure()
            {
                // State cleanup is handled by SevereSprainLogic.
            }

            public override void OnUpdate()
            {
                if (!Settings.options.EnableSevereSprains)
                {
                    Cure();
                    return;
                }

                if (!Risk)
                    return;

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