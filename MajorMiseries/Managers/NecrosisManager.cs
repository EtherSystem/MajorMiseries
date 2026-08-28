using AfflictionComponent.Components;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.DeepNecrosis;
using static MajorMiseries.Afflictions.Necrosis;
using static MajorMiseries.Afflictions.NecrosisRisk;

namespace MajorMiseries.Managers
{
    internal static class NecrosisManager
    {
        private static readonly AfflictionBodyArea[] Extremities =
        [
            AfflictionBodyArea.Head,
            AfflictionBodyArea.HandLeft,
            AfflictionBodyArea.HandRight,
            AfflictionBodyArea.FootLeft,
            AfflictionBodyArea.FootRight,
        ];

        private const float LOCAL_WOUND_MEMORY_HOURS = 12f;
        private const float TISSUE_THREAT_DECAY_PER_HOUR = 25f;
        private const float TISSUE_THREAT_SLOW_PER_HOUR = 10f;
        private const float TISSUE_THREAT_GRAVE_PER_HOUR = 20f;
        private const float TISSUE_THREAT_CATASTROPHIC_PER_HOUR = 35f;
        private const float TISSUE_THREAT_TRIGGER = 100f;
        private const float THREAT_LOG_INTERVAL_HOURS = 1f;

        private static readonly Dictionary<AfflictionBodyArea, TissueThreatSeverity> LastLoggedThreatSeverity = [];
        private static readonly Dictionary<AfflictionBodyArea, float> NextThreatDiagnosticLogTime = [];
        private static readonly Dictionary<AfflictionBodyArea, float> LastThreatLogGameTime = [];

        internal const float NECROSIS_PROPAGATION_RECOVERY_PER_HOUR = 0.1f;

        private const string NECROSIS_INFECTION_CAUSE = "GAMEPLAY_NecrosisCause";

        internal static float GetNecrosisPropagationGainPerHour()
        {
            float durationDays = GetNecrosisPresetIndex() switch
            {
                0 => 28f,
                2 => 10f,
                3 => 7f,
                4 => 4f,
                _ => 14f,
            };

            return 100f / (durationDays * 24f);
        }

        internal static float GetNecrosisTreatmentDurationHours()
        {
            return GetNecrosisPresetIndex() switch
            {
                2 => 120f,
                3 => 72f,
                4 => 48f,
                _ => 168f,
            };
        }

        internal static int GetNecrosisRequiredTablets()
        {
            return GetNecrosisPresetIndex() == 0 ? 2 : 4;
        }

        private static float GetTissueThreatGainPresetMultiplier()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 0.50f,
                2 => 1.50f,
                3 => 2.00f,
                4 => 3.00f,
                _ => 1.00f,
            };
        }

        private static float GetTissueThreatDecayPresetMultiplier()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 1.50f,
                2 => 0.75f,
                3 => 0.50f,
                4 => 0.25f,
                _ => 1.00f,
            };
        }

        private static float GetRiskProgressPresetMultiplier()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 0.50f,
                2 => 1.50f,
                3 => 2.00f,
                4 => 3.00f,
                _ => 1.00f,
            };
        }

        private static float GetRiskRecoveryPresetMultiplier()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 1.50f,
                2 => 0.75f,
                3 => 0.50f,
                4 => 0.25f,
                _ => 1.00f,
            };
        }

        private static float GetHandInfectionChance(bool treated)
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => treated ? 0f : 30f,
                2 => treated ? 30f : 70f,
                3 => treated ? 40f : 80f,
                4 => treated ? 50f : 100f,
                _ => treated ? 20f : 60f,
            };
        }

        private static float GetFootInfectionChance()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 10f,
                2 => 30f,
                3 => 40f,
                4 => 50f,
                _ => 20f,
            };
        }

        private static float GetFootInfectionRollIntervalHours()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => 7f * 24f,
                2 => 4f * 24f,
                3 => 3f * 24f,
                4 => 2f * 24f,
                _ => 5f * 24f,
            };
        }

        private static int GetNecrosisPresetIndex()
        {
            return Mathf.Clamp(Settings.options?.NecrosisPreset ?? 1, 0, 4);
        }

        private static string GetNecrosisPresetName()
        {
            return GetNecrosisPresetIndex() switch
            {
                0 => "Forgiving",
                2 => "Harsh",
                3 => "Brutal",
                4 => "Who Wants to Play Like This?",
                _ => "Standard",
            };
        }

        private static float GetMiseryGainMultiplier(AfflictionBodyArea bodyArea)
        {
            Condition? condition = GameManager.GetConditionComponent();
            if (condition == null) return 1f;

            float multiplier = 1f;
            bool handOrFoot = IsHand(bodyArea) || IsFoot(bodyArea);

            if (condition.HasSpecificAffliction(AfflictionType.WeakConstitution)) multiplier *= 1.25f;
            if (handOrFoot && condition.HasSpecificAffliction(AfflictionType.WeakJoints)) multiplier *= 1.25f;
            if (handOrFoot && condition.HasSpecificAffliction(AfflictionType.PoorCirculation)) multiplier *= 1.50f;
            if (condition.HasSpecificAffliction(AfflictionType.BrokenBody)) multiplier *= 2.00f;

            return multiplier;
        }

        private static float GetMiseryRecoveryMultiplier(AfflictionBodyArea bodyArea)
        {
            Condition? condition = GameManager.GetConditionComponent();
            if (condition == null) return 1f;

            float multiplier = 1f;
            bool handOrFoot = IsHand(bodyArea) || IsFoot(bodyArea);

            if (condition.HasSpecificAffliction(AfflictionType.WeakConstitution)) multiplier *= 0.75f;
            if (handOrFoot && condition.HasSpecificAffliction(AfflictionType.WeakJoints)) multiplier *= 0.75f;
            if (handOrFoot && condition.HasSpecificAffliction(AfflictionType.PoorCirculation)) multiplier *= 0.50f;
            if (condition.HasSpecificAffliction(AfflictionType.BrokenBody)) multiplier *= 0.25f;

            return multiplier;
        }

        internal static void ClampState()
        {
            foreach (AfflictionBodyArea extremity in Extremities)
            {
                SetWoundMemoryHours(extremity, Mathf.Clamp(GetWoundMemoryHours(extremity), 0f, LOCAL_WOUND_MEMORY_HOURS));
                SetTissueThreat(extremity, Mathf.Clamp(GetTissueThreat(extremity), 0f, TISSUE_THREAT_TRIGGER));
            }
        }

        internal static void Update(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f || !Settings.options.EnableNecrosis) return;

            UpdateLocalWoundMemory(gameHoursPassed);

            foreach (AfflictionBodyArea extremity in Extremities)
            {
                if (HasNecrosisStageForExtremity(extremity))
                {
                    ClearTissueThreat(extremity, "an affliction stage is already active");
                    continue;
                }

                UpdateTissueThreat(extremity, gameHoursPassed);
            }

            TryStartRisk(AfflictionBodyArea.Head);
            TryStartPairedRisk(AfflictionBodyArea.HandLeft, AfflictionBodyArea.HandRight);
            TryStartPairedRisk(AfflictionBodyArea.FootLeft, AfflictionBodyArea.FootRight);
        }

        internal static NecrosisRiskAssessment GetRiskAssessment(AfflictionBodyArea extremity)
        {
            if (!IsEligibleExtremity(extremity)) return default;

            LocalProtectionState protection = GetLocalProtectionState(extremity);
            LocalWoundState wounds = GetLocalWoundState(extremity);
            if (!protection.IsValid) return default;

            float internalBodyTemp = GetEffectiveInternalBodyTemp();
            float frostbiteRisk = GetVanillaFrostbiteRiskValue(extremity);
            bool hasFrostbite = HasVanillaFrostbiteAt(extremity);
            bool hasProximalFracture = HasProximalFracture(extremity);
            float woundMemoryHours = GetWoundMemoryHours(extremity);

            TissueThreatAssessment threat = GetTissueThreatAssessment(
                protection,
                wounds,
                internalBodyTemp,
                frostbiteRisk,
                hasFrostbite,
                hasProximalFracture,
                woundMemoryHours);

            float baseRate = threat.Severity switch
            {
                TissueThreatSeverity.Slow => 0.5f,
                TissueThreatSeverity.Grave => 1f,
                TissueThreatSeverity.Catastrophic => 2f,
                _ => 0f,
            };

            bool canRecover = CanRiskRecover(
                protection,
                wounds,
                internalBodyTemp,
                frostbiteRisk);

            string recoveryBlockers = GetRecoveryBlockers(
                protection,
                wounds,
                internalBodyTemp,
                frostbiteRisk);

            float finalRate;

            if (baseRate > 0f)
            {
                finalRate = baseRate
                    * GetRiskProgressPresetMultiplier()
                    * GetMiseryGainMultiplier(extremity)
                    * ImmunityManager.GetNecrosisRiskProgressMultiplier();
            }
            else if (canRecover)
            {
                float recovery = GetRecoveryPerHour(woundMemoryHours);
                finalRate = -recovery
                    * GetRiskRecoveryPresetMultiplier()
                    * GetMiseryRecoveryMultiplier(extremity)
                    * ImmunityManager.GetNecrosisRiskRecoveryMultiplier();
            }
            else
            {
                finalRate = 0f;
            }

            return new NecrosisRiskAssessment(
                extremity,
                threat.Severity,
                threat.Reasons,
                GetTissueThreat(extremity),
                baseRate,
                finalRate,
                threat.Severity == TissueThreatSeverity.Catastrophic,
                canRecover,
                recoveryBlockers,
                protection.AmbientExposureC,
                internalBodyTemp,
                protection.HasAnyCovering,
                protection.HasFunctionalCovering,
                protection.AverageWetness,
                protection.MaxWetness,
                protection.AverageFrozen,
                protection.MaxFrozen,
                frostbiteRisk,
                hasFrostbite,
                hasProximalFracture,
                wounds.HasBloodLoss,
                wounds.HasInfectionRisk,
                wounds.HasInfection,
                woundMemoryHours);
        }

        internal static bool IsEligibleExtremity(AfflictionBodyArea bodyArea)
        {
            return bodyArea == AfflictionBodyArea.HandLeft
                || bodyArea == AfflictionBodyArea.HandRight
                || bodyArea == AfflictionBodyArea.FootLeft
                || bodyArea == AfflictionBodyArea.FootRight
                || bodyArea == AfflictionBodyArea.Head;
        }

        internal static float GetVanillaFrostbiteRiskValue(AfflictionBodyArea bodyArea)
        {
            Frostbite? frostbite = GameManager.GetFrostbiteComponent();
            if (frostbite == null) return 0f;

            return Mathf.Max(0f, frostbite.GetFrostbiteRiskValue((int)bodyArea));
        }

        internal static bool HasVanillaFrostbiteAt(AfflictionBodyArea bodyArea)
        {
            Frostbite? frostbite = GameManager.GetFrostbiteComponent();
            return frostbite != null && frostbite.NumInstancesFrostbiteAtLocation((int)bodyArea) > 0;
        }

        internal static bool HasNecrosisStageForExtremity(AfflictionBodyArea bodyArea)
        {
            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null) return false;

            for (int i = 0; i < manager.m_Afflictions.Count; i++)
            {
                CustomAffliction affliction = manager.m_Afflictions[i];

                if (affliction is NecrosisRiskAffliction necrosisRisk && necrosisRisk.m_Location == bodyArea) return true;
                if (affliction is NecrosisAffliction necrosis && necrosis.m_Location == bodyArea) return true;
                if (affliction is DeepNecrosisAffliction deepNecrosis && deepNecrosis.SourceExtremity == bodyArea) return true;
            }

            return false;
        }


        internal static void OnCarcassHarvestCompleted()
        {
            if (!Settings.options.EnableNecrosis) return;

            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null) return;

            HashSet<AfflictionBodyArea> processedHands = [];

            for (int i = 0; i < manager.m_Afflictions.Count; i++)
            {
                if (manager.m_Afflictions[i] is not NecrosisAffliction necrosis) continue;
                if (!IsHand(necrosis.m_Location) || !processedHands.Add(necrosis.m_Location)) continue;

                bool treated = necrosis.IsTreatmentActive();
                float chance = GetHandInfectionChance(treated);
                TryApplyInfectionRisk(necrosis.m_Location, chance, "carcass harvest", treated);
            }
        }

        internal static void InitializeFootInfectionSchedule(NecrosisAffliction necrosis, float now)
        {
            if (necrosis == null || !IsFoot(necrosis.m_Location)) return;
            if (necrosis.NextFootInfectionRollTime > 0f) return;

            necrosis.NextFootInfectionRollTime = now + GetFootInfectionRollIntervalHours();
        }

        internal static void UpdateFootInfectionSchedule(NecrosisAffliction necrosis, float now, ref bool scheduleLogged)
        {
            if (necrosis == null || !IsFoot(necrosis.m_Location)) return;

            if (necrosis.NextFootInfectionRollTime <= 0f)
            {
                necrosis.NextFootInfectionRollTime = now + GetFootInfectionRollIntervalHours();
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            if (!scheduleLogged)
            {
                float remaining = Mathf.Max(0f, necrosis.NextFootInfectionRollTime - now);
                Core.Log($"Necrosis foot infection schedule [{necrosis.m_Location}] -> next {GetFootInfectionChance():0.#}% roll in {remaining:0.##}h (at hour {necrosis.NextFootInfectionRollTime:0.##}).");
                scheduleLogged = true;
            }

            if (now < necrosis.NextFootInfectionRollTime) return;

            int rollsProcessed = 0;

            while (now >= necrosis.NextFootInfectionRollTime && rollsProcessed < 1024)
            {
                float scheduledAt = necrosis.NextFootInfectionRollTime;
                float intervalHours = GetFootInfectionRollIntervalHours();
                necrosis.NextFootInfectionRollTime += intervalHours;
                rollsProcessed++;

                TryApplyInfectionRisk(
                    necrosis.m_Location,
                    GetFootInfectionChance(),
                    $"foot cycle at hour {scheduledAt:0.##}",
                    necrosis.IsTreatmentActive());
            }

            if (rollsProcessed >= 1024 && now >= necrosis.NextFootInfectionRollTime)
            {
                float intervalHours = GetFootInfectionRollIntervalHours();
                Core.Warn($"Necrosis foot infection schedule [{necrosis.m_Location}] exceeded the safety limit; next roll reset to {intervalHours:0.##}h from now.", false);
                necrosis.NextFootInfectionRollTime = now + intervalHours;
            }

            if (rollsProcessed > 0) AfflictionSaveHelper.QueueSurvivalSave();
        }

        private static void TryApplyInfectionRisk(AfflictionBodyArea bodyArea, float chance, string trigger, bool treated)
        {
            if (HasInfectionRiskOrInfection(bodyArea))
            {
                Core.Log($"Necrosis infection roll [{bodyArea}] skipped -> Trigger:{trigger} | existing Infection Risk or Infection.");
                return;
            }

            float roll = UnityEngine.Random.Range(0f, 100f);
            bool success = roll < chance;

            Core.Log(
                $"Necrosis infection roll [{bodyArea}] -> Trigger:{trigger} | " +
                $"Chance:{chance:0.#}% | Roll:{roll:0.##}% | Treated:{treated} | Result:{(success ? "Infection Risk" : "none")}.");

            if (!success) return;

            InfectionRisk? infectionRisk = GameManager.GetInfectionRiskComponent();
            if (infectionRisk == null)
            {
                Core.Warn($"Necrosis infection roll [{bodyArea}] succeeded but InfectionRisk component was unavailable.", false);
                return;
            }

            infectionRisk.InfectionRiskStart(NECROSIS_INFECTION_CAUSE, bodyArea, true);
            AfflictionSaveHelper.QueueSurvivalSave();
        }

        private static bool HasInfectionRiskOrInfection(AfflictionBodyArea bodyArea)
        {
            InfectionRisk? infectionRisk = GameManager.GetInfectionRiskComponent();
            if (infectionRisk != null)
            {
                int riskCount = infectionRisk.GetAfflictionsCount();
                for (int i = 0; i < riskCount; i++)
                {
                    if (infectionRisk.GetLocation(i) == bodyArea) return true;
                }
            }

            Infection? infection = GameManager.GetInfectionComponent();
            if (infection == null) return false;

            int infectionCount = infection.GetAfflictionsCount();
            for (int i = 0; i < infectionCount; i++)
            {
                if (infection.GetLocation(i) == bodyArea) return true;
            }

            return false;
        }

        private static bool IsHand(AfflictionBodyArea bodyArea)
        {
            return bodyArea == AfflictionBodyArea.HandLeft || bodyArea == AfflictionBodyArea.HandRight;
        }

        private static bool IsFoot(AfflictionBodyArea bodyArea)
        {
            return bodyArea == AfflictionBodyArea.FootLeft || bodyArea == AfflictionBodyArea.FootRight;
        }

        internal static AfflictionBodyArea GetDeepNecrosisLocation(AfflictionBodyArea sourceExtremity)
        {
            return sourceExtremity switch
            {
                AfflictionBodyArea.HandLeft => AfflictionBodyArea.ArmLeft,
                AfflictionBodyArea.HandRight => AfflictionBodyArea.ArmRight,
                AfflictionBodyArea.FootLeft => AfflictionBodyArea.LegLeft,
                AfflictionBodyArea.FootRight => AfflictionBodyArea.LegRight,
                AfflictionBodyArea.Head => AfflictionBodyArea.Head,
                _ => sourceExtremity,
            };
        }

        internal static float GetBiologicalShieldDrainPerHour(out string sources)
        {
            float total = 0f;
            List<string> activeSources = [];

            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null)
            {
                sources = string.Empty;
                return 0f;
            }

            for (int i = 0; i < manager.m_Afflictions.Count; i++)
            {
                if (manager.m_Afflictions[i] is NecrosisAffliction necrosis)
                {
                    if (necrosis.HasPropagated || necrosis.IsTreatmentActive()) continue;

                    float propagation = Mathf.Clamp(necrosis.Propagation, 0f, 100f);
                    float drain = propagation < 25f ? 0.10f
                        : propagation < 50f ? 0.25f
                        : propagation < 75f ? 0.50f
                        : 0.75f;

                    total += drain;
                    activeSources.Add($"Necrosis[{necrosis.m_Location}]:{drain:0.##}/h");
                    continue;
                }

                if (manager.m_Afflictions[i] is DeepNecrosisAffliction deep)
                {
                    total += 2f;
                    activeSources.Add($"DeepNecrosis[{deep.m_Location}]:2/h");
                }
            }

            sources = activeSources.Count > 0 ? string.Join(",", activeSources) : string.Empty;
            return total;
        }

        internal static float GetFeverCapC(out string sources)
        {
            float cap = 37f;
            List<string> activeSources = [];

            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null)
            {
                sources = string.Empty;
                return cap;
            }

            for (int i = 0; i < manager.m_Afflictions.Count; i++)
            {
                if (manager.m_Afflictions[i] is NecrosisAffliction necrosis)
                {
                    if (necrosis.HasPropagated || necrosis.IsTreatmentActive()) continue;

                    float propagation = Mathf.Clamp(necrosis.Propagation, 0f, 100f);
                    float localCap = propagation < 25f ? 37f
                        : propagation < 50f ? 38f
                        : propagation < 75f ? 39f
                        : 41f;

                    if (localCap <= 37f) continue;

                    cap = Mathf.Max(cap, localCap);
                    activeSources.Add($"Necrosis[{necrosis.m_Location}]:{localCap:0.#}C");
                    continue;
                }

                if (manager.m_Afflictions[i] is DeepNecrosisAffliction deep)
                {
                    cap = 43f;
                    activeSources.Add($"DeepNecrosis[{deep.m_Location}]:43C");
                }
            }

            sources = activeSources.Count > 0 ? string.Join(",", activeSources) : string.Empty;
            return cap;
        }

        internal static void DevResetRiskSystem()
        {
            Core.State ??= new MMState();

            foreach (AfflictionBodyArea extremity in Extremities)
            {
                SetWoundMemoryHours(extremity, 0f);
                SetTissueThreat(extremity, 0f);
            }

            LastLoggedThreatSeverity.Clear();
            NextThreatDiagnosticLogTime.Clear();
            LastThreatLogGameTime.Clear();

            Core.Instance?.MarkDirty();
            Core.Log("DEV: Necrosis Risk background state reset for all extremities.");
        }

        internal static void LogCurrentAssessments()
        {
            foreach (AfflictionBodyArea extremity in Extremities)
            {
                NecrosisRiskAssessment assessment = GetRiskAssessment(extremity);
                float currentRisk = GetRiskAffliction(extremity)?.RiskValue ?? 0f;

                Core.Log(
                    $"Necrosis assessment [{extremity}] -> " +
                    $"Risk:{currentRisk:0.##}% | TissueThreat:{assessment.TissueThreat:0.##}% | " +
                    $"Threat:{assessment.ThreatSeverity} | Reasons:{assessment.ThreatReasons} | " +
                    $"Rate:{assessment.FinalRatePerHour:+0.###;-0.###;0}/h | Preset:{GetNecrosisPresetName()} | " +
                    $"MiseryGain:x{GetMiseryGainMultiplier(extremity):0.#####} | MiseryRecovery:x{GetMiseryRecoveryMultiplier(extremity):0.#####} | " +
                    $"AmbientExposure:{assessment.AmbientExposureC:0.0}C | BodyHeat:{assessment.InternalBodyTempC:0.0}C | " +
                    $"AnyCovering:{assessment.HasAnyCovering} | FunctionalCovering:{assessment.HasFunctionalCovering} | " +
                    $"WetAvg:{assessment.AverageWetness:P0} | WetMax:{assessment.MaxWetness:P0} | " +
                    $"FrozenAvg:{assessment.AverageFrozen:P0} | FrozenMax:{assessment.MaxFrozen:P0} | " +
                    $"FrostbiteRisk:{assessment.FrostbiteRisk:0.##}% | Frostbite:{assessment.HasFrostbite} | " +
                    $"Fracture:{assessment.HasProximalFracture} | BloodLoss:{assessment.HasBloodLoss} | " +
                    $"InfectionRisk:{assessment.HasInfectionRisk} | Infection:{assessment.HasInfection} | " +
                    $"WoundMemory:{assessment.WoundMemoryHours:0.##}h | CanRecover:{assessment.CanRecover} | " +
                    $"RecoveryBlockers:{assessment.RecoveryBlockers}",
                    false);
            }
        }

        private static void UpdateTissueThreat(AfflictionBodyArea extremity, float gameHoursPassed)
        {
            if (GameManager.GetPlayerManagerComponent() == null || GameManager.GetWeatherComponent() == null) return;

            NecrosisRiskAssessment assessment = GetRiskAssessment(extremity);
            float before = GetTissueThreat(extremity);
            float rate = GetTissueThreatRatePerHour(extremity, assessment.ThreatSeverity, before);
            float after = Mathf.Clamp(before + rate * gameHoursPassed, 0f, TISSUE_THREAT_TRIGGER);

            if (!Mathf.Approximately(before, after))
            {
                SetTissueThreat(extremity, after);
                Core.Instance?.MarkDirty();
            }

            MaybeLogTissueThreat(extremity, before, after, gameHoursPassed, rate, assessment);
        }

        private static float GetTissueThreatRatePerHour(
            AfflictionBodyArea bodyArea,
            TissueThreatSeverity severity,
            float currentThreat)
        {
            float baseGain = severity switch
            {
                TissueThreatSeverity.Slow => TISSUE_THREAT_SLOW_PER_HOUR,
                TissueThreatSeverity.Grave => TISSUE_THREAT_GRAVE_PER_HOUR,
                TissueThreatSeverity.Catastrophic => TISSUE_THREAT_CATASTROPHIC_PER_HOUR,
                _ => 0f,
            };

            if (baseGain > 0f)
            {
                return baseGain
                    * GetTissueThreatGainPresetMultiplier()
                    * GetMiseryGainMultiplier(bodyArea);
            }

            if (currentThreat <= 0f) return 0f;

            return -TISSUE_THREAT_DECAY_PER_HOUR
                * GetTissueThreatDecayPresetMultiplier()
                * GetMiseryRecoveryMultiplier(bodyArea);
        }

        private static void MaybeLogTissueThreat(
            AfflictionBodyArea extremity,
            float before,
            float after,
            float elapsedHours,
            float rate,
            NecrosisRiskAssessment assessment)
        {
            TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
            float currentTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : 0f;

            if (LastThreatLogGameTime.TryGetValue(extremity, out float lastGameTime) && currentTime < lastGameTime)
            {
                LastLoggedThreatSeverity.Remove(extremity);
                NextThreatDiagnosticLogTime.Remove(extremity);
            }

            LastThreatLogGameTime[extremity] = currentTime;
            LastLoggedThreatSeverity.TryGetValue(extremity, out TissueThreatSeverity lastSeverity);
            NextThreatDiagnosticLogTime.TryGetValue(extremity, out float nextLogTime);

            bool severityChanged = lastSeverity != assessment.ThreatSeverity;
            bool intervalReached = (before > 0f || after > 0f || assessment.ThreatSeverity != TissueThreatSeverity.None)
                && currentTime >= nextLogTime;
            bool reachedZero = before > 0f && after <= 0f;
            bool reachedTrigger = before < TISSUE_THREAT_TRIGGER && after >= TISSUE_THREAT_TRIGGER;

            if (!severityChanged && !intervalReached && !reachedZero && !reachedTrigger) return;

            string movement = rate > 0f ? "building" : rate < 0f ? "decaying" : "inactive";

            Core.Log(
                $"Necrosis TissueThreat [{extremity}] {movement} -> " +
                $"{before:0.###}% => {after:0.###}% over {elapsedHours:0.###}h | " +
                $"Rate:{rate:+0.###;-0.###;0}/h | Severity:{assessment.ThreatSeverity} | Preset:{GetNecrosisPresetName()} | " +
                $"MiseryGain:x{GetMiseryGainMultiplier(extremity):0.#####} | MiseryRecovery:x{GetMiseryRecoveryMultiplier(extremity):0.#####} | " +
                $"Reasons:{assessment.ThreatReasons} | AmbientExposure:{assessment.AmbientExposureC:0.0}C | " +
                $"BodyHeat:{assessment.InternalBodyTempC:0.0}C | FunctionalCovering:{assessment.HasFunctionalCovering} | " +
                $"WetMax:{assessment.MaxWetness:P0} (diagnostic only) | FrozenMax:{assessment.MaxFrozen:P0} | " +
                $"FrostbiteRisk:{assessment.FrostbiteRisk:0.##}% | Frostbite:{assessment.HasFrostbite} | " +
                $"Fracture:{assessment.HasProximalFracture} | BloodLoss:{assessment.HasBloodLoss} | " +
                $"InfectionRisk:{assessment.HasInfectionRisk} | Infection:{assessment.HasInfection} | " +
                $"WoundMemory:{assessment.WoundMemoryHours:0.###}h.");

            LastLoggedThreatSeverity[extremity] = assessment.ThreatSeverity;
            NextThreatDiagnosticLogTime[extremity] = currentTime + THREAT_LOG_INTERVAL_HOURS;
        }

        private static void ClearTissueThreat(AfflictionBodyArea extremity, string reason)
        {
            float current = GetTissueThreat(extremity);
            if (current <= 0f) return;

            SetTissueThreat(extremity, 0f);
            Core.Instance?.MarkDirty();
            Core.Log($"Necrosis TissueThreat [{extremity}] cleared from {current:0.###}% because {reason}.");
        }

        private static void TryStartRisk(AfflictionBodyArea extremity)
        {
            if (HasNecrosisStageForExtremity(extremity)) return;
            if (GetTissueThreat(extremity) < TISSUE_THREAT_TRIGGER) return;

            StartRisk(extremity, GetRiskAssessment(extremity));
        }

        private static void TryStartPairedRisk(AfflictionBodyArea left, AfflictionBodyArea right)
        {
            bool leftOccupied = HasNecrosisStageForExtremity(left);
            bool rightOccupied = HasNecrosisStageForExtremity(right);

            if (leftOccupied && rightOccupied) return;

            bool leftReady = !leftOccupied && GetTissueThreat(left) >= TISSUE_THREAT_TRIGGER;
            bool rightReady = !rightOccupied && GetTissueThreat(right) >= TISSUE_THREAT_TRIGGER;
            if (!leftReady && !rightReady) return;

            NecrosisRiskAssessment leftAssessment = GetRiskAssessment(left);
            NecrosisRiskAssessment rightAssessment = GetRiskAssessment(right);

            if (!leftOccupied && !rightOccupied)
            {
                AfflictionBodyArea selected;
                NecrosisRiskAssessment selectedAssessment;

                if (!leftReady)
                {
                    selected = right;
                    selectedAssessment = rightAssessment;
                }
                else if (!rightReady)
                {
                    selected = left;
                    selectedAssessment = leftAssessment;
                }
                else if ((int)leftAssessment.ThreatSeverity > (int)rightAssessment.ThreatSeverity)
                {
                    selected = left;
                    selectedAssessment = leftAssessment;
                }
                else if ((int)rightAssessment.ThreatSeverity > (int)leftAssessment.ThreatSeverity)
                {
                    selected = right;
                    selectedAssessment = rightAssessment;
                }
                else
                {
                    bool chooseLeft = UnityEngine.Random.Range(0, 2) == 0;
                    selected = chooseLeft ? left : right;
                    selectedAssessment = chooseLeft ? leftAssessment : rightAssessment;
                }

                StartRisk(selected, selectedAssessment);
                return;
            }

            AfflictionBodyArea available = leftOccupied ? right : left;
            AfflictionBodyArea occupied = leftOccupied ? left : right;
            NecrosisRiskAssessment availableAssessment = leftOccupied ? rightAssessment : leftAssessment;

            if (GetTissueThreat(available) < TISSUE_THREAT_TRIGGER) return;

            NecrosisRiskAffliction? occupiedRisk = GetRiskAffliction(occupied);
            bool occupiedAdvanced = occupiedRisk != null && occupiedRisk.RiskValue >= 50f;
            bool occupiedIrreversible = HasIrreversibleNecrosisStage(occupied);

            if (!availableAssessment.Critical && !occupiedAdvanced && !occupiedIrreversible) return;

            StartRisk(available, availableAssessment);
        }

        private static void StartRisk(
            AfflictionBodyArea extremity,
            NecrosisRiskAssessment assessment)
        {
            const float initialRisk = 0.1f;

            Core.Log(
                $"NecrosisRisk [{extremity}] started after TissueThreat reached 100% -> " +
                $"Initial:{initialRisk:0.###}% | Severity:{assessment.ThreatSeverity} | " +
                $"Reasons:{assessment.ThreatReasons} | AmbientExposure:{assessment.AmbientExposureC:0.0}C | " +
                $"BodyHeat:{assessment.InternalBodyTempC:0.0}C | FunctionalCovering:{assessment.HasFunctionalCovering} | " +
                $"WetMax:{assessment.MaxWetness:P0} (diagnostic only) | FrozenMax:{assessment.MaxFrozen:P0} | " +
                $"FrostbiteRisk:{assessment.FrostbiteRisk:0.##}% | Frostbite:{assessment.HasFrostbite} | " +
                $"Fracture:{assessment.HasProximalFracture} | BloodLoss:{assessment.HasBloodLoss} | " +
                $"InfectionRisk:{assessment.HasInfectionRisk} | Infection:{assessment.HasInfection} | " +
                $"WoundMemory:{assessment.WoundMemoryHours:0.###}h.");

            SetTissueThreat(extremity, 0f);
            Core.Instance?.MarkDirty();

            new NecrosisRiskAffliction(extremity)
            {
                RiskValue = initialRisk,
            }.Start();

            AfflictionSaveHelper.QueueSurvivalSave();
        }

        private static bool HasIrreversibleNecrosisStage(AfflictionBodyArea extremity)
        {
            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null) return false;

            return manager.m_Afflictions.Any(a =>
                (a is NecrosisAffliction necrosis && necrosis.m_Location == extremity)
                || (a is DeepNecrosisAffliction deep && deep.SourceExtremity == extremity));
        }

        private static NecrosisRiskAffliction? GetRiskAffliction(AfflictionBodyArea extremity)
        {
            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null) return null;

            return manager.m_Afflictions
                .OfType<NecrosisRiskAffliction>()
                .FirstOrDefault(risk => risk.m_Location == extremity);
        }

        private static LocalProtectionState GetLocalProtectionState(AfflictionBodyArea extremity)
        {
            PlayerManager? player = GameManager.GetPlayerManagerComponent();
            Weather? weather = GameManager.GetWeatherComponent();

            if (player == null || weather == null) return default;

            ClothingRegion region = GetClothingRegion(extremity);
            int itemCount = 0;
            int functionalCount = 0;
            float wetness = 0f;
            float frozen = 0f;
            float maxWetness = 0f;
            float maxFrozen = 0f;

            for (int layerIndex = (int)ClothingLayer.Base; layerIndex < (int)ClothingLayer.NumLayers; layerIndex++)
            {
                ClothingLayer layer = (ClothingLayer)layerIndex;
                GearItem? gear = player.GetClothingInSlot(region, layer);
                ClothingItem? clothing = gear?.m_ClothingItem;
                if (gear == null || clothing == null) continue;

                itemCount++;

                float wet01 = Mathf.Clamp01(clothing.GetWetnessNormalized());
                float frozen01 = Mathf.Clamp01(clothing.GetFrozenNormalized());

                wetness += wet01;
                frozen += frozen01;
                maxWetness = Mathf.Max(maxWetness, wet01);
                maxFrozen = Mathf.Max(maxFrozen, frozen01);

                if (gear.CurrentHP > 0f) functionalCount++;
            }

            float averageWetness = itemCount > 0 ? wetness / itemCount : 0f;
            float averageFrozen = itemCount > 0 ? frozen / itemCount : 0f;
            float ambientExposureC = weather.GetCurrentTemperature() + weather.GetCurrentWindchill();

            return new LocalProtectionState(
                true,
                itemCount > 0,
                functionalCount > 0,
                ambientExposureC,
                averageWetness,
                averageFrozen,
                maxWetness,
                maxFrozen);
        }

        private static TissueThreatAssessment GetTissueThreatAssessment(
            LocalProtectionState protection,
            LocalWoundState wounds,
            float internalBodyTemp,
            float frostbiteRisk,
            bool hasFrostbite,
            bool hasProximalFracture,
            float woundMemoryHours)
        {
            bool severeFrozen = protection.HasFunctionalCovering && protection.MaxFrozen >= 0.75f;
            bool unprotected = !protection.HasFunctionalCovering;
            bool hypothermia = HasVanillaAffliction(AfflictionType.Hypothermia);
            bool recentLocalWound = woundMemoryHours > 0f;

            List<string> catastrophicReasons = [];
            List<string> graveReasons = [];
            List<string> slowReasons = [];

            if (internalBodyTemp <= 34f && severeFrozen)
            {
                catastrophicReasons.Add("BodyHeat<=34C+FrozenCovering>=75%");
            }

            if (internalBodyTemp <= 34f && unprotected && frostbiteRisk >= 75f)
            {
                catastrophicReasons.Add("BodyHeat<=34C+Unprotected+FrostbiteRisk>=75%");
            }

            if (hasFrostbite && wounds.HasInfection)
            {
                catastrophicReasons.Add("Frostbite+Infection");
            }

            if (hasFrostbite && wounds.HasBloodLoss && internalBodyTemp < 35f)
            {
                catastrophicReasons.Add("Frostbite+BloodLoss+BodyHeat<35C");
            }

            if (catastrophicReasons.Count > 0)
            {
                return new TissueThreatAssessment(
                    TissueThreatSeverity.Catastrophic,
                    string.Join(";", catastrophicReasons));
            }

            if (severeFrozen && internalBodyTemp < 35f)
            {
                graveReasons.Add("FrozenCovering>=75%+BodyHeat<35C");
            }

            if (unprotected && frostbiteRisk >= 90f)
            {
                graveReasons.Add("Unprotected+FrostbiteRisk>=90%");
            }

            if (hasFrostbite && (wounds.HasBloodLoss || wounds.HasInfectionRisk))
            {
                graveReasons.Add("Frostbite+LocalWound");
            }

            if (wounds.HasInfection && (hasProximalFracture || wounds.HasBloodLoss))
            {
                graveReasons.Add("Infection+FractureOrBloodLoss");
            }

            if (frostbiteRisk >= 75f && hypothermia)
            {
                graveReasons.Add("FrostbiteRisk>=75%+Hypothermia");
            }

            if (graveReasons.Count > 0)
            {
                return new TissueThreatAssessment(
                    TissueThreatSeverity.Grave,
                    string.Join(";", graveReasons));
            }

            if (severeFrozen && internalBodyTemp >= 35f && internalBodyTemp < 36f)
            {
                slowReasons.Add("FrozenCovering>=75%+BodyHeat35-36C");
            }

            if (unprotected && frostbiteRisk >= 75f && frostbiteRisk < 90f)
            {
                slowReasons.Add("Unprotected+FrostbiteRisk75-89%");
            }

            if (wounds.HasBloodLoss && wounds.HasInfectionRisk)
            {
                slowReasons.Add("BloodLoss+InfectionRisk");
            }

            if (wounds.HasBloodLoss && internalBodyTemp >= 35f && internalBodyTemp < 36f)
            {
                slowReasons.Add("BloodLoss+BodyHeat35-36C");
            }

            if (hasProximalFracture && (wounds.HasInfectionRisk || wounds.HasBloodLoss))
            {
                slowReasons.Add("Fracture+LocalWound");
            }

            if (recentLocalWound && (severeFrozen || frostbiteRisk >= 75f))
            {
                slowReasons.Add("RecentLocalWound+SevereColdDamage");
            }

            if (slowReasons.Count > 0)
            {
                return new TissueThreatAssessment(
                    TissueThreatSeverity.Slow,
                    string.Join(";", slowReasons));
            }

            return new TissueThreatAssessment(TissueThreatSeverity.None, "None");
        }

        private static bool CanRiskRecover(
            LocalProtectionState protection,
            LocalWoundState wounds,
            float internalBodyTemp,
            float frostbiteRisk)
        {
            if (!protection.HasFunctionalCovering) return false;
            if (protection.MaxFrozen >= 0.50f) return false;
            if (internalBodyTemp < 36f) return false;
            if (HasVanillaAffliction(AfflictionType.Hypothermia)) return false;
            if (frostbiteRisk >= 75f) return false;
            if (wounds.HasBloodLoss || wounds.HasInfectionRisk || wounds.HasInfection) return false;

            return true;
        }

        private static string GetRecoveryBlockers(
            LocalProtectionState protection,
            LocalWoundState wounds,
            float internalBodyTemp,
            float frostbiteRisk)
        {
            List<string> blockers = [];

            if (!protection.HasFunctionalCovering) blockers.Add("NoFunctionalCovering");
            if (protection.MaxFrozen >= 0.50f) blockers.Add($"MaxFrozen={protection.MaxFrozen:P0}");
            if (internalBodyTemp < 36f) blockers.Add($"BodyHeat={internalBodyTemp:0.0}C");
            if (HasVanillaAffliction(AfflictionType.Hypothermia)) blockers.Add("Hypothermia");
            if (frostbiteRisk >= 75f) blockers.Add($"FrostbiteRisk={frostbiteRisk:0.##}%");
            if (wounds.HasBloodLoss) blockers.Add("BloodLoss");
            if (wounds.HasInfectionRisk) blockers.Add("InfectionRisk");
            if (wounds.HasInfection) blockers.Add("Infection");

            return blockers.Count > 0 ? string.Join(",", blockers) : "None";
        }

        private static float GetRecoveryPerHour(float woundMemoryHours)
        {
            if (woundMemoryHours > 0f || GetEffectiveInternalBodyTemp() < 37f) return 0.5f;

            Freezing? freezing = GameManager.GetFreezingComponent();
            return freezing != null && freezing.m_CurrentWarmingPerHour > 0f ? 2f : 1.5f;
        }

        private static bool HasProximalFracture(AfflictionBodyArea extremity)
        {
            AfflictionManager? manager = AfflictionManager.GetAfflictionManagerInstance();
            if (manager?.m_Afflictions == null) return false;

            AfflictionBodyArea proximal = extremity switch
            {
                AfflictionBodyArea.HandLeft => AfflictionBodyArea.ArmLeft,
                AfflictionBodyArea.HandRight => AfflictionBodyArea.ArmRight,
                AfflictionBodyArea.FootLeft => AfflictionBodyArea.LegLeft,
                AfflictionBodyArea.FootRight => AfflictionBodyArea.LegRight,
                _ => AfflictionBodyArea.Head,
            };

            if (extremity == AfflictionBodyArea.Head) return false;

            return manager.m_Afflictions.Any(a =>
                (a is BrokenArmAffliction arm && arm.m_Location == proximal)
                || (a is BrokenLegAffliction leg && leg.m_Location == proximal));
        }

        private static bool HasVanillaAffliction(AfflictionType afflictionType)
        {
            Condition? condition = GameManager.GetConditionComponent();
            return condition != null && condition.HasSpecificAffliction(afflictionType);
        }

        private static float GetEffectiveInternalBodyTemp()
        {
            if (Settings.options == null || !Settings.options.EnableBodyHeat) return 37f;

            return Mathf.Clamp(Core.State.InternalBodyTemp, 34f, 43f);
        }


        private static LocalWoundState GetLocalWoundState(AfflictionBodyArea extremity)
        {
            bool hasBloodLoss = false;
            bool hasInfectionRisk = false;
            bool hasInfection = false;

            try
            {
                BloodLoss? bloodLoss = GameManager.GetBloodLossComponent();
                int bloodLossCount = bloodLoss?.GetAfflictionsCount() ?? 0;

                for (int i = 0; i < bloodLossCount; i++)
                {
                    if (MapWoundLocationToExtremity(bloodLoss!.GetLocation(i)) == extremity)
                    {
                        hasBloodLoss = true;
                        break;
                    }
                }

                InfectionRisk? infectionRisk = GameManager.GetInfectionRiskComponent();
                int infectionRiskCount = infectionRisk?.GetAfflictionsCount() ?? 0;

                for (int i = 0; i < infectionRiskCount; i++)
                {
                    if (MapWoundLocationToExtremity(infectionRisk!.GetLocation(i)) == extremity)
                    {
                        hasInfectionRisk = true;
                        break;
                    }
                }

                Infection? infection = GameManager.GetInfectionComponent();
                int infectionCount = infection?.GetAfflictionsCount() ?? 0;

                for (int i = 0; i < infectionCount; i++)
                {
                    if (MapWoundLocationToExtremity(infection!.GetLocation(i)) == extremity)
                    {
                        hasInfection = true;
                        break;
                    }
                }
            }
            catch
            {
            }

            return new LocalWoundState(hasBloodLoss, hasInfectionRisk, hasInfection);
        }

        private static void UpdateLocalWoundMemory(float gameHoursPassed)
        {
            bool changed = false;

            foreach (AfflictionBodyArea extremity in Extremities)
            {
                LocalWoundState wounds = GetLocalWoundState(extremity);
                float before = GetWoundMemoryHours(extremity);
                float after = wounds.HasBloodLoss || wounds.HasInfectionRisk
                    ? LOCAL_WOUND_MEMORY_HOURS
                    : Mathf.Max(0f, before - gameHoursPassed);

                if (Mathf.Approximately(before, after)) continue;

                SetWoundMemoryHours(extremity, after);
                changed = true;
            }

            if (changed) Core.Instance?.MarkDirty();
        }

        private static AfflictionBodyArea? MapWoundLocationToExtremity(AfflictionBodyArea area)
        {
            return area switch
            {
                AfflictionBodyArea.Head or AfflictionBodyArea.Neck => AfflictionBodyArea.Head,
                AfflictionBodyArea.ArmLeft or AfflictionBodyArea.HandLeft => AfflictionBodyArea.HandLeft,
                AfflictionBodyArea.ArmRight or AfflictionBodyArea.HandRight => AfflictionBodyArea.HandRight,
                AfflictionBodyArea.LegLeft or AfflictionBodyArea.FootLeft => AfflictionBodyArea.FootLeft,
                AfflictionBodyArea.LegRight or AfflictionBodyArea.FootRight => AfflictionBodyArea.FootRight,
                _ => null,
            };
        }

        private static ClothingRegion GetClothingRegion(AfflictionBodyArea extremity)
        {
            return extremity switch
            {
                AfflictionBodyArea.Head => ClothingRegion.Head,
                AfflictionBodyArea.HandLeft or AfflictionBodyArea.HandRight => ClothingRegion.Hands,
                AfflictionBodyArea.FootLeft or AfflictionBodyArea.FootRight => ClothingRegion.Feet,
                _ => ClothingRegion.NumRegions,
            };
        }

        private static float GetWoundMemoryHours(AfflictionBodyArea extremity)
        {
            return extremity switch
            {
                AfflictionBodyArea.Head => Core.State.NecrosisHeadWoundMemoryHours,
                AfflictionBodyArea.HandLeft => Core.State.NecrosisHandLeftWoundMemoryHours,
                AfflictionBodyArea.HandRight => Core.State.NecrosisHandRightWoundMemoryHours,
                AfflictionBodyArea.FootLeft => Core.State.NecrosisFootLeftWoundMemoryHours,
                AfflictionBodyArea.FootRight => Core.State.NecrosisFootRightWoundMemoryHours,
                _ => 0f,
            };
        }

        private static void SetWoundMemoryHours(AfflictionBodyArea extremity, float value)
        {
            switch (extremity)
            {
                case AfflictionBodyArea.Head: Core.State.NecrosisHeadWoundMemoryHours = value; break;
                case AfflictionBodyArea.HandLeft: Core.State.NecrosisHandLeftWoundMemoryHours = value; break;
                case AfflictionBodyArea.HandRight: Core.State.NecrosisHandRightWoundMemoryHours = value; break;
                case AfflictionBodyArea.FootLeft: Core.State.NecrosisFootLeftWoundMemoryHours = value; break;
                case AfflictionBodyArea.FootRight: Core.State.NecrosisFootRightWoundMemoryHours = value; break;
            }
        }

        private static float GetTissueThreat(AfflictionBodyArea extremity)
        {
            return extremity switch
            {
                AfflictionBodyArea.Head => Core.State.NecrosisHeadTissueThreat,
                AfflictionBodyArea.HandLeft => Core.State.NecrosisHandLeftTissueThreat,
                AfflictionBodyArea.HandRight => Core.State.NecrosisHandRightTissueThreat,
                AfflictionBodyArea.FootLeft => Core.State.NecrosisFootLeftTissueThreat,
                AfflictionBodyArea.FootRight => Core.State.NecrosisFootRightTissueThreat,
                _ => 0f,
            };
        }

        private static void SetTissueThreat(AfflictionBodyArea extremity, float value)
        {
            switch (extremity)
            {
                case AfflictionBodyArea.Head: Core.State.NecrosisHeadTissueThreat = value; break;
                case AfflictionBodyArea.HandLeft: Core.State.NecrosisHandLeftTissueThreat = value; break;
                case AfflictionBodyArea.HandRight: Core.State.NecrosisHandRightTissueThreat = value; break;
                case AfflictionBodyArea.FootLeft: Core.State.NecrosisFootLeftTissueThreat = value; break;
                case AfflictionBodyArea.FootRight: Core.State.NecrosisFootRightTissueThreat = value; break;
            }
        }

        internal enum TissueThreatSeverity
        {
            None = 0,
            Slow = 1,
            Grave = 2,
            Catastrophic = 3,
        }

        internal readonly struct NecrosisRiskAssessment
        {
            internal readonly AfflictionBodyArea Extremity;
            internal readonly TissueThreatSeverity ThreatSeverity;
            internal readonly string ThreatReasons;
            internal readonly float TissueThreat;
            internal readonly float BaseRatePerHour;
            internal readonly float FinalRatePerHour;
            internal readonly bool Critical;
            internal readonly bool CanRecover;
            internal readonly string RecoveryBlockers;
            internal readonly float AmbientExposureC;
            internal readonly float InternalBodyTempC;
            internal readonly bool HasAnyCovering;
            internal readonly bool HasFunctionalCovering;
            internal readonly float AverageWetness;
            internal readonly float MaxWetness;
            internal readonly float AverageFrozen;
            internal readonly float MaxFrozen;
            internal readonly float FrostbiteRisk;
            internal readonly bool HasFrostbite;
            internal readonly bool HasProximalFracture;
            internal readonly bool HasBloodLoss;
            internal readonly bool HasInfectionRisk;
            internal readonly bool HasInfection;
            internal readonly float WoundMemoryHours;

            internal NecrosisRiskAssessment(
                AfflictionBodyArea extremity,
                TissueThreatSeverity threatSeverity,
                string threatReasons,
                float tissueThreat,
                float baseRatePerHour,
                float finalRatePerHour,
                bool critical,
                bool canRecover,
                string recoveryBlockers,
                float ambientExposureC,
                float internalBodyTempC,
                bool hasAnyCovering,
                bool hasFunctionalCovering,
                float averageWetness,
                float maxWetness,
                float averageFrozen,
                float maxFrozen,
                float frostbiteRisk,
                bool hasFrostbite,
                bool hasProximalFracture,
                bool hasBloodLoss,
                bool hasInfectionRisk,
                bool hasInfection,
                float woundMemoryHours)
            {
                Extremity = extremity;
                ThreatSeverity = threatSeverity;
                ThreatReasons = threatReasons;
                TissueThreat = tissueThreat;
                BaseRatePerHour = baseRatePerHour;
                FinalRatePerHour = finalRatePerHour;
                Critical = critical;
                CanRecover = canRecover;
                RecoveryBlockers = recoveryBlockers;
                AmbientExposureC = ambientExposureC;
                InternalBodyTempC = internalBodyTempC;
                HasAnyCovering = hasAnyCovering;
                HasFunctionalCovering = hasFunctionalCovering;
                AverageWetness = averageWetness;
                MaxWetness = maxWetness;
                AverageFrozen = averageFrozen;
                MaxFrozen = maxFrozen;
                FrostbiteRisk = frostbiteRisk;
                HasFrostbite = hasFrostbite;
                HasProximalFracture = hasProximalFracture;
                HasBloodLoss = hasBloodLoss;
                HasInfectionRisk = hasInfectionRisk;
                HasInfection = hasInfection;
                WoundMemoryHours = woundMemoryHours;
            }
        }

        [HarmonyPatch(typeof(Panel_BodyHarvest), "HarvestSuccessful")]
        private static class PanelBodyHarvestHarvestSuccessfulPatch
        {
            private static void Postfix()
            {
                OnCarcassHarvestCompleted();
            }
        }

        private readonly struct TissueThreatAssessment
        {
            internal readonly TissueThreatSeverity Severity;
            internal readonly string Reasons;

            internal TissueThreatAssessment(TissueThreatSeverity severity, string reasons)
            {
                Severity = severity;
                Reasons = reasons;
            }
        }

        private readonly struct LocalProtectionState
        {
            internal readonly bool IsValid;
            internal readonly bool HasAnyCovering;
            internal readonly bool HasFunctionalCovering;
            internal readonly float AmbientExposureC;
            internal readonly float AverageWetness;
            internal readonly float AverageFrozen;
            internal readonly float MaxWetness;
            internal readonly float MaxFrozen;

            internal LocalProtectionState(
                bool isValid,
                bool hasAnyCovering,
                bool hasFunctionalCovering,
                float ambientExposureC,
                float averageWetness,
                float averageFrozen,
                float maxWetness,
                float maxFrozen)
            {
                IsValid = isValid;
                HasAnyCovering = hasAnyCovering;
                HasFunctionalCovering = hasFunctionalCovering;
                AmbientExposureC = ambientExposureC;
                AverageWetness = averageWetness;
                AverageFrozen = averageFrozen;
                MaxWetness = maxWetness;
                MaxFrozen = maxFrozen;
            }
        }

        private readonly struct LocalWoundState
        {
            internal readonly bool HasBloodLoss;
            internal readonly bool HasInfectionRisk;
            internal readonly bool HasInfection;

            internal LocalWoundState(bool hasBloodLoss, bool hasInfectionRisk, bool hasInfection)
            {
                HasBloodLoss = hasBloodLoss;
                HasInfectionRisk = hasInfectionRisk;
                HasInfection = hasInfection;
            }
        }
    }
}
