using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class HomeSickness
    {
        public class HomeSicknessAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_HomeSicknessName";
            private const string CAUSE_KEY = "GAMEPLAY_HomeSicknessCause";
            private const string DESC_KEY = "GAMEPLAY_HomeSicknessDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.HomeSickness.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.HomeSickness_ALT.png";

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public HomeSicknessAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public HomeSicknessAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is HomeSicknessAffliction existing)
                {
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

                if (!RegionalAfflictionLogic.ShouldHomeSicknessBeActive())
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

                Core.Log($"HomeSickness refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}