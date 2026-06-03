using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class RegionalDistress
    {
        public class RegionalDistressAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_RegionalDistressName";
            private const string CAUSE_KEY = "GAMEPLAY_RegionalDistressCause";
            private const string DESC_KEY = "GAMEPLAY_RegionalDistressDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.RegionalDistress.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.RegionalDistress_ALT.png";

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;
            public bool DebugForced { get; set; } = false;

            public RegionalDistressAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is RegionalDistressAffliction existing)
                {
                    existing.DebugForced = DebugForced;
                    existing.ResetAffliction(resetRemedies: false);
                }
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

                if (DebugForced)
                    return;

                if (!RegionalAfflictionManager.ShouldRegionalDistressBeActive())
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

                Core.Log($"RegionalDistress refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}