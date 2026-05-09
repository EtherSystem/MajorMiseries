using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions.Buffs
{
    internal class HomeComfort
    {
        public class HomeComfortBuff : CustomAffliction, IBuff, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_HomeComfortName";
            private const string CAUSE_KEY = "GAMEPLAY_HomeComfortCause";
            private const string DESC_KEY = "GAMEPLAY_HomeComfortDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Buffs.Classic.HomeComfort.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Buffs.Alt.HomeComfort_ALT.png";

            public static bool IsActive { get; private set; } = false;

            public bool Buff { get; set; } = true;

            public bool BuffCold { get; set; } = true;
            public bool BuffFatigue { get; set; } = true;
            public bool BuffHunger { get; set; } = false;
            public bool BuffThirst { get; set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            public HomeComfortBuff(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is HomeComfortBuff existing)
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

                if (!RegionalAfflictionLogic.ShouldHomeComfortBeActive())
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

                Core.Log($"HomeComfort refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}