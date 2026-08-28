using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;
using System.Collections;
using static MajorMiseries.Afflictions.DeepNecrosis;

namespace MajorMiseries.Afflictions
{
    internal class Necrosis
    {
        public class NecrosisAffliction : CustomAffliction, IRemedies, IInstance, IAfflictionProgressBar, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_NecrosisName";
            private const string CAUSE_KEY = "GAMEPLAY_NecrosisCause";
            private const string DESC_KEY = "GAMEPLAY_NecrosisDescription";
            private const string TREATMENT_APPLIED_KEY = "GAMEPLAY_NecrosisTreatmentApplied";
            private const string TREATMENT_EXPIRED_KEY = "GAMEPLAY_NecrosisTreatmentExpired";

            private const string HEAD_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.NecrosisHead.png";
            private const string HAND_LEFT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.NecrosisHand_Left.png";
            private const string HAND_RIGHT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.NecrosisHand_Right.png";
            private const string FOOT_LEFT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.NecrosisFoot_Left.png";
            private const string FOOT_RIGHT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.NecrosisFoot_Right.png";

            private const string HEAD_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.NecrosisHead_ALT.png";
            private const string HAND_LEFT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.NecrosisHand_Left_ALT.png";
            private const string HAND_RIGHT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.NecrosisHand_Right_ALT.png";
            private const string FOOT_LEFT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.NecrosisFoot_Left_ALT.png";
            private const string FOOT_RIGHT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.NecrosisFoot_Right_ALT.png";

            private const int ANTIBIOTIC_TABLETS_PER_DOSE = 2;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];
            public bool InstantHeal { get; set; } = false;

            public float ProgressBar { get; set; } = 1f;
            public bool InvertProgressBar { get; set; } = true;

            public float Propagation { get; set; } = 0f;
            public float TreatmentActiveUntil { get; set; } = -1f;
            public float LastUpdateTime { get; set; }
            public bool HasPropagated { get; set; } = false;
            public float NextFootInfectionRollTime { get; set; } = -1f;

            private bool _footInfectionScheduleLogged;

            public NecrosisAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, GetIcon(bodyArea), bodyArea, true)
            {
                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                LastUpdateTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : 0f;
                NecrosisManager.InitializeFootInfectionSchedule(this, LastUpdateTime);

                ResetRemedyItems();
                SyncProgressBar();
            }

            internal static string GetIcon(AfflictionBodyArea bodyArea)
            {
                bool useAlt = UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance;

                return bodyArea switch
                {
                    AfflictionBodyArea.HandLeft => useAlt ? HAND_LEFT_ALT_ICON : HAND_LEFT_ICON,
                    AfflictionBodyArea.HandRight => useAlt ? HAND_RIGHT_ALT_ICON : HAND_RIGHT_ICON,
                    AfflictionBodyArea.FootLeft => useAlt ? FOOT_LEFT_ALT_ICON : FOOT_LEFT_ICON,
                    AfflictionBodyArea.FootRight => useAlt ? FOOT_RIGHT_ALT_ICON : FOOT_RIGHT_ICON,
                    _ => useAlt ? HEAD_ALT_ICON : HEAD_ICON,
                };
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                return;
            }

            protected override bool ApplyRemedyCondition()
            {
                return !HasPropagated && !IsTreatmentActive();
            }

            public void CureSymptoms()
            {
                EnsureRemedyItemsMatchPreset();
                ConsumeExtraAntibioticTabletForDose();

                if (NeedsRemedy()) return;

                float now = GameManager.GetTimeOfDayComponent()?.GetHoursPlayedNotPaused() ?? LastUpdateTime;
                float treatmentDurationHours = NecrosisManager.GetNecrosisTreatmentDurationHours();
                TreatmentActiveUntil = now + treatmentDurationHours;
                LastUpdateTime = now;

                HUDMessage.AddMessage(Localization.Get(TREATMENT_APPLIED_KEY));
                Core.Log($"Necrosis treatment applied on {m_Location} for {treatmentDurationHours:0.##}h, until hour {TreatmentActiveUntil:0.##}.");
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public void OnCure()
            {
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public override void OnUpdate()
            {

                if (!Settings.options.EnableNecrosis)
                {
                    Cure();
                    return;
                }

                Propagation = Mathf.Clamp(Propagation, 0f, 100f);
                EnsureRemedyItemsMatchPreset();
                SyncProgressBar();

                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                if (timeOfDay == null) return;

                float now = timeOfDay.GetHoursPlayedNotPaused();
                NecrosisManager.UpdateFootInfectionSchedule(this, now, ref _footInfectionScheduleLogged);

                if (HasPropagated)
                {
                    LastUpdateTime = now;
                    return;
                }

                float elapsedHours = now - LastUpdateTime;
                if (elapsedHours <= 0f) return;

                bool treatmentWasActive = IsTreatmentActive(LastUpdateTime);
                bool treatmentIsActive = IsTreatmentActive(now);

                if (treatmentWasActive && !treatmentIsActive)
                {
                    float treatedHours = Mathf.Max(0f, TreatmentActiveUntil - LastUpdateTime);
                    float untreatedHours = Mathf.Max(0f, now - Mathf.Max(LastUpdateTime, TreatmentActiveUntil));

                    ApplyPropagationChange(treatedHours, treated: true);
                    ApplyPropagationChange(untreatedHours, treated: false);

                    TreatmentActiveUntil = -1f;
                    ResetRemedyItems();
                    HUDMessage.AddMessage(Localization.Get(TREATMENT_EXPIRED_KEY));
                    Core.Log($"Necrosis treatment expired on {m_Location}; propagation resumed.");
                }
                else
                {
                    ApplyPropagationChange(elapsedHours, treatmentIsActive);
                }

                LastUpdateTime = now;
                SyncProgressBar();

                if (Propagation < 100f) return;

                Propagation = 100f;
                HasPropagated = true;
                RemedyItems = [];
                AltRemedyItems = [];
                SyncProgressBar();

                Core.Log($"Necrosis on {m_Location} reached full propagation -> applying Deep Necrosis.");
                MelonCoroutines.Start(StartDeepNecrosisNextFrame());
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public bool IsTreatmentActive()
            {
                float now = GameManager.GetTimeOfDayComponent()?.GetHoursPlayedNotPaused() ?? LastUpdateTime;
                return IsTreatmentActive(now);
            }

            private bool IsTreatmentActive(float time)
            {
                return TreatmentActiveUntil > 0f && time < TreatmentActiveUntil;
            }

            private void ApplyPropagationChange(float elapsedHours, bool treated)
            {
                if (elapsedHours <= 0f) return;

                if (treated)
                {
                    Propagation = Mathf.Max(0f, Propagation - elapsedHours * NecrosisManager.NECROSIS_PROPAGATION_RECOVERY_PER_HOUR);
                }
                else
                {
                    Propagation = Mathf.Min(100f, Propagation + elapsedHours * NecrosisManager.GetNecrosisPropagationGainPerHour());
                }
            }

            private void ResetRemedyItems()
            {
                if (HasPropagated)
                {
                    RemedyItems = [];
                    AltRemedyItems = [];
                    return;
                }

                RemedyItems =
                [
                    Tuple.Create(
                        "GEAR_BottleAntibiotics",
                        NecrosisManager.GetNecrosisRequiredTablets(),
                        NecrosisManager.GetNecrosisRequiredTablets()),
                ];
            }

            private void EnsureRemedyItemsMatchPreset()
            {
                if (HasPropagated || IsTreatmentActive()) return;

                int requiredTablets = NecrosisManager.GetNecrosisRequiredTablets();
                if (RemedyItems != null
                    && RemedyItems.Length == 1
                    && RemedyItems[0].Item1 == "GEAR_BottleAntibiotics"
                    && RemedyItems[0].Item2 == requiredTablets)
                {
                    return;
                }

                ResetRemedyItems();
            }

            private void ConsumeExtraAntibioticTabletForDose()
            {
                if (RemedyItems == null || RemedyItems.Length <= 0) return;

                Tuple<string, int, int> current = RemedyItems[0];
                int remaining = Mathf.Max(0, current.Item3 - (ANTIBIOTIC_TABLETS_PER_DOSE - 1));
                RemedyItems[0] = Tuple.Create(current.Item1, current.Item2, remaining);
            }

            private void SyncProgressBar()
            {
                ProgressBar = 1f - Mathf.Clamp01(Propagation / 100f);
                InvertProgressBar = true;
            }

            private IEnumerator StartDeepNecrosisNextFrame()
            {
                yield return null;

                if (!Settings.options.EnableNecrosis) yield break;

                AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
                if (manager?.m_Afflictions.Any(a => a is DeepNecrosisAffliction deep && deep.SourceExtremity == m_Location) == true) yield break;

                new DeepNecrosisAffliction(m_Location).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Necrosis refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
