using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions.RequiemStagesAfflictions
{
    internal class Requiem
    {
        public class RequiemAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_RequiemName";
            private const string CAUSE_KEY = "GAMEPLAY_RequiemCause";
            private const string DESC_KEY = "GAMEPLAY_RequiemDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.Requiem.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.Requiem_ALT.png";

            public InstanceType Type { get; set; } = InstanceType.Single;
            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is RequiemAffliction requiem)
                {
                    requiem.ResetAffliction(resetRemedies: false);
                    var now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                    requiem.EndTime = now + requiem.Duration;
                }
            }

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;

            public RequiemAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public override void OnUpdate()
            {
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Requiem refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}