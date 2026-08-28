using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;
using System.Collections;
using static MajorMiseries.Afflictions.Necrosis;

namespace MajorMiseries.Afflictions
{
    internal class NecrosisRisk
    {
        public class NecrosisRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_NecrosisRiskName";
            private const string CAUSE_KEY = "GAMEPLAY_NecrosisRiskCause";
            private const string DESC_KEY = "GAMEPLAY_NecrosisRiskDescription";

            private const float UPDATE_INTERVAL_HOURS = 1f / 60f;
            private const float LOG_INTERVAL_HOURS = 1f;

            private float _nextDiagnosticLogTime = -1f;
            private float _lastLoggedRatePerHour = float.NaN;
            private bool _loggedDebugForced;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;
            public bool Risk { get; set; } = true;
            public bool DebugForced { get; set; } = false;
            public float DebugRiskValue { get; set; } = 50f;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];
            public bool InstantHeal { get; set; } = true;

            public float RiskValue { get; set; } = 0f;
            public float LastUpdateTime { get; set; }

            public NecrosisRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, NecrosisAffliction.GetIcon(bodyArea), bodyArea, true)
            {
                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                LastUpdateTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : 0f;
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is not NecrosisRiskAffliction existingRisk) return;

                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                existingRisk.LastUpdateTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : existingRisk.LastUpdateTime;

                Core.Log(
                    $"NecrosisRisk [{existingRisk.m_Location}] duplicate start ignored -> " +
                    $"existing risk retained at {existingRisk.GetRiskValue():0.###}%.");
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                Core.Log($"NecrosisRisk [{m_Location}] removed -> final risk {GetRiskValue():0.###}%.");
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public float GetRiskValue() => DebugForced ? DebugRiskValue : RiskValue;

            public override void OnUpdate()
            {

                if (DebugForced)
                {
                    if (!_loggedDebugForced)
                    {
                        Core.Log($"NecrosisRisk [{m_Location}] is debug-forced at {DebugRiskValue:0.###}% and will not evolve.");
                        _loggedDebugForced = true;
                    }

                    return;
                }

                if (!Settings.options.EnableNecrosis)
                {
                    Core.Log($"NecrosisRisk [{m_Location}] removed because the Necrosis system is disabled.");
                    Cure();
                    return;
                }

                AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
                if (manager?.m_Afflictions.Any(a => a is NecrosisAffliction necrosis && necrosis.m_Location == m_Location) == true)
                {
                    Core.Log($"NecrosisRisk [{m_Location}] removed because Necrosis already exists on the same extremity.");
                    Cure(false);
                    return;
                }

                RiskValue = Mathf.Clamp(RiskValue, 0f, 100f);

                float before = RiskValue;
                if (!TryUpdateRiskValue(out float elapsedHours, out NecrosisManager.NecrosisRiskAssessment assessment)) return;

                MaybeLogDiagnostic(before, elapsedHours, assessment);

                if (RiskValue >= 100f)
                {
                    RiskValue = 100f;
                    Core.Log($"NecrosisRisk [{m_Location}] evolved into Necrosis at 100%.");
                    Cure(false);
                    MelonCoroutines.Start(StartNecrosisNextFrame());
                    return;
                }

                if (RiskValue <= 0f && assessment.FinalRatePerHour <= 0f)
                {
                    Core.Log(
                        $"NecrosisRisk [{m_Location}] fully recovered -> " +
                        $"rate {assessment.FinalRatePerHour:+0.###;-0.###;0}/h | " +
                        $"recovery blockers:{assessment.RecoveryBlockers}.");
                    Cure();
                }
            }

            public void UpdateRiskValue()
            {
                TryUpdateRiskValue(out _, out _);
            }

            private bool TryUpdateRiskValue(
                out float elapsedHours,
                out NecrosisManager.NecrosisRiskAssessment assessment)
            {
                elapsedHours = 0f;
                assessment = default;

                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                if (timeOfDay == null)
                {
                    Core.Warn($"NecrosisRisk [{m_Location}] update skipped because TimeOfDay is unavailable.");
                    return false;
                }

                float currentTime = timeOfDay.GetHoursPlayedNotPaused();
                elapsedHours = currentTime - LastUpdateTime;

                if (elapsedHours < 0f)
                {
                    Core.Warn(
                        $"NecrosisRisk [{m_Location}] detected a negative time delta " +
                        $"({elapsedHours:0.###}h); resetting its update clock.");
                    LastUpdateTime = currentTime;
                    elapsedHours = 0f;
                    return false;
                }

                if (elapsedHours < UPDATE_INTERVAL_HOURS) return false;

                assessment = NecrosisManager.GetRiskAssessment(m_Location);

                float before = RiskValue;
                RiskValue = Mathf.Clamp(RiskValue + assessment.FinalRatePerHour * elapsedHours, 0f, 100f);
                LastUpdateTime = currentTime;

                return true;
            }

            private void MaybeLogDiagnostic(
                float before,
                float elapsedHours,
                NecrosisManager.NecrosisRiskAssessment assessment)
            {
                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                float currentTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : LastUpdateTime;

                bool rateChanged = float.IsNaN(_lastLoggedRatePerHour)
                    || !Mathf.Approximately(_lastLoggedRatePerHour, assessment.FinalRatePerHour);

                bool intervalReached = _nextDiagnosticLogTime < 0f || currentTime >= _nextDiagnosticLogTime;
                if (!rateChanged && !intervalReached) return;

                string movement = assessment.FinalRatePerHour > 0f
                    ? "progressing"
                    : assessment.FinalRatePerHour < 0f
                        ? "recovering"
                        : "stagnating";

                Core.Log(
                    $"NecrosisRisk [{m_Location}] {movement} -> " +
                    $"{before:0.###}% => {RiskValue:0.###}% over {elapsedHours:0.###}h | " +
                    $"Rate:{assessment.FinalRatePerHour:+0.###;-0.###;0}/h " +
                    $"(base {assessment.BaseRatePerHour:0.###}/h) | " +
                    $"Threat:{assessment.ThreatSeverity} | Reasons:{assessment.ThreatReasons} | " +
                    $"TissueThreat:{assessment.TissueThreat:0.###}% | " +
                    $"AmbientExposure:{assessment.AmbientExposureC:0.0}C | BodyHeat:{assessment.InternalBodyTempC:0.0}C | " +
                    $"AnyCovering:{assessment.HasAnyCovering} | FunctionalCovering:{assessment.HasFunctionalCovering} | " +
                    $"WetAvg:{assessment.AverageWetness:P0} | WetMax:{assessment.MaxWetness:P0} (diagnostic only) | " +
                    $"FrozenAvg:{assessment.AverageFrozen:P0} | FrozenMax:{assessment.MaxFrozen:P0} | " +
                    $"FrostbiteRisk:{assessment.FrostbiteRisk:0.##}% | Frostbite:{assessment.HasFrostbite} | " +
                    $"Fracture:{assessment.HasProximalFracture} | BloodLoss:{assessment.HasBloodLoss} | " +
                    $"InfectionRisk:{assessment.HasInfectionRisk} | Infection:{assessment.HasInfection} | " +
                    $"WoundMemory:{assessment.WoundMemoryHours:0.###}h | CanRecover:{assessment.CanRecover} | " +
                    $"RecoveryBlockers:{assessment.RecoveryBlockers}");

                _lastLoggedRatePerHour = assessment.FinalRatePerHour;
                _nextDiagnosticLogTime = currentTime + LOG_INTERVAL_HOURS;
            }

            private IEnumerator StartNecrosisNextFrame()
            {
                yield return null;

                if (!Settings.options.EnableNecrosis) yield break;
                if (NecrosisManager.HasNecrosisStageForExtremity(m_Location)) yield break;

                new NecrosisAffliction(m_Location).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"NecrosisRisk refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
