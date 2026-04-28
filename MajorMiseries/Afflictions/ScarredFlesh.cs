using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class ScarredFlesh
    {
        public class ScarredFleshAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_ScarredFleshName";
            private const string CAUSE_KEY = "GAMEPLAY_ScarredFleshCause";
            private const string DESC_KEY = "GAMEPLAY_ScarredFleshDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.ScarredFlesh.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.ScarredFlesh_ALT.png";

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public ScarredFleshAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public ScarredFleshAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_SevereLacerations", bodyArea)
            {
                RefreshStackDisplay();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is ScarredFleshAffliction scarredFlesh)
                {
                    scarredFlesh.RefreshStackDisplay();
                }
            }

            internal void RefreshStackDisplay()
            {
                Core.State ??= new Persistence.MMState();

                int count = Math.Max(0, Core.State.ScarredFleshHistoryCount);
                string baseName = Localization.Get(NAME_KEY);

                m_Name = count > 1
                    ? $"{baseName} x{count}"
                    : baseName;

                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;
            }

            public void CureSymptoms()
            {
                // cure symptoms but not the affliction
            }

            public void OnCure()
            {
                // when the affliction is cured, apply this code
            }

            public override void OnUpdate()
            {
                // yes theres no effects, its intended
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                RefreshStackDisplay();

                Core.Log($"ScarredFlesh refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}