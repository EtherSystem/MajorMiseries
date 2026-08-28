using AfflictionComponent.Components;
using MajorMiseries.Afflictions.Buffs;
using static MajorMiseries.Afflictions.Fever;

namespace MajorMiseries.Managers
{
    internal static class ImmunityManager
    {
        private static readonly ClothingRegion[] BodyHeatRegions =
        [
            ClothingRegion.Head,
            ClothingRegion.Chest,
            ClothingRegion.Hands,
            ClothingRegion.Legs,
            ClothingRegion.Feet
        ];

        private const float FirstAidWidgetBackgroundWidthReduction = 0.2f;
        private static readonly HashSet<int> s_FirstAidReducedCloneBackgrounds = [];

        private static int s_LastLoggedShieldBucket = -1;
        private static string s_LastLoggedMode = string.Empty;
        private static int s_LastLoggedDrainBucket = -1;
        private static int s_LastLoggedBodyDrainBucket = -1;
        private static int s_LastLoggedBiologicalDrainBucket = -1;
        private static int s_LastLoggedLowStatCount = -1;
        private static int s_LastLoggedZeroStatCount = -1;
        private static FeverMode s_LastLoggedShieldFeverMode = FeverMode.None;
        private static string s_LastLoggedBiologicalSources = string.Empty;

        private static FeverMode s_FeverMode = FeverMode.None;
        private static float s_FeverInfectionRiskRecoveryHours;
        private static float s_FeverTargetBodyTempC;
        private static float s_LingeringFeverTargetBodyTempC;

        private static double s_BodyHeatGameSecondsAccum;

        private static float s_HeatStaggerPhase;
        private static float s_HeatHeadachePulseRealTimer;
        private static bool s_LastHeatHeadacheActive;

        private static FeverMode s_LastLoggedFeverModeState = FeverMode.None;
        private static int s_LastLoggedFeverTargetBucket = int.MinValue;
        private static string s_LastLoggedFeverSources = string.Empty;
        private static bool s_DevFeverSuppressedUntilThreatClears;

        internal static void ResetRuntime()
        {
            s_LastLoggedShieldBucket = -1;
            s_LastLoggedMode = string.Empty;
            s_LastLoggedDrainBucket = -1;
            s_LastLoggedBodyDrainBucket = -1;
            s_LastLoggedBiologicalDrainBucket = -1;
            s_LastLoggedLowStatCount = -1;
            s_LastLoggedZeroStatCount = -1;
            s_LastLoggedShieldFeverMode = FeverMode.None;
            s_LastLoggedBiologicalSources = string.Empty;

            s_FeverMode = FeverMode.None;
            s_FeverInfectionRiskRecoveryHours = 0f;
            s_FeverTargetBodyTempC = 0f;
            s_LingeringFeverTargetBodyTempC = 0f;

            s_BodyHeatGameSecondsAccum = 0.0;

            s_HeatHeadachePulseRealTimer = 0f;
            s_LastHeatHeadacheActive = false;

            s_LastLoggedFeverModeState = FeverMode.None;
            s_LastLoggedFeverTargetBucket = int.MinValue;
            s_LastLoggedFeverSources = string.Empty;
            s_DevFeverSuppressedUntilThreatClears = false;

            ResetHeatNeurologicalEffects();
        }

        internal static void ClampState()
        {
            Core.State.ImmunityShield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);
            Core.State.FeverOnsetHours = Mathf.Clamp(Core.State.FeverOnsetHours, 0f, 1f);
            Core.State.FeverLingerHoursRemaining = Mathf.Clamp(Core.State.FeverLingerHoursRemaining, 0f, 3f);

            if (Core.State.InternalBodyTemp < 30f || Core.State.InternalBodyTemp > 43f)
                Core.State.InternalBodyTemp = 37f;

            Core.State.InternalBodyTemp = Mathf.Clamp(Core.State.InternalBodyTemp, 34f, 43f);

            if (Core.State.InternalBodyTemp <= 34.5f) Core.State.BodyHeatHypothermiaSourceActive = true;
            else if (Core.State.InternalBodyTemp >= 35f) Core.State.BodyHeatHypothermiaSourceActive = false;
        }

        internal static void OnStateLoaded()
        {
            ClampState();
            ResetRuntime();
        }

        internal static void Update(float gameHoursPassed)
        {
            if (!IsEnabled())
            {
                bool changed = Core.State.FeverOnsetHours > 0f || Core.State.FeverLingerHoursRemaining > 0f || s_FeverMode != FeverMode.None;

                Core.State.FeverOnsetHours = 0f;
                Core.State.FeverLingerHoursRemaining = 0f;
                s_FeverMode = FeverMode.None;
                s_FeverInfectionRiskRecoveryHours = 0f;
                s_FeverTargetBodyTempC = 0f;
                s_LingeringFeverTargetBodyTempC = 0f;

                SyncFeverAffliction(false);

                if (changed) Core.Instance?.MarkDirty();
                return;
            }

            if (gameHoursPassed <= 0f || gameHoursPassed > 12f) return;
            if (!TryGetBodyStats(out BodyStats stats)) return;

            float oldShield = Core.State.ImmunityShield;

            float bodyDrainPerHour = GetBodyStatDrainPerHour(stats, out int activeDrainStats, out int zeroStats, out float zeroDrainMultiplier);
            float biologicalDrainPerHour = GetBiologicalThreatDrainPerHour(out bool biologicalThreat, out string biologicalSources);

            if (biologicalDrainPerHour > 0f) biologicalDrainPerHour *= zeroDrainMultiplier;

            float drainPerHour = Mathf.Clamp(bodyDrainPerHour + biologicalDrainPerHour, 0f, 8f);

            string mode;

            if (drainPerHour > 0f)
            {
                Core.State.ImmunityShield -= drainPerHour * gameHoursPassed;
                mode = BuildDrainMode(activeDrainStats, zeroStats, bodyDrainPerHour, biologicalDrainPerHour, biologicalSources);
            }
            else
            {
                float regenPerHour = GetRegenPerHour(stats, biologicalThreat);

                if (regenPerHour > 0f)
                {
                    Core.State.ImmunityShield += regenPerHour * gameHoursPassed;
                    mode = "regenerating";
                }
                else
                {
                    mode = biologicalThreat ? "engaged" : "stable";
                }
            }

            Core.State.ImmunityShield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);

            UpdateFeverState(gameHoursPassed);
            SyncFeverAffliction(ShouldFeverAfflictionBeVisible());

            UpdateFeverInfectionRiskRecovery(gameHoursPassed);

            if (!Mathf.Approximately(oldShield, Core.State.ImmunityShield)) Core.Instance?.MarkDirty();

            LogStateIfChanged(mode, stats, drainPerHour, bodyDrainPerHour, biologicalDrainPerHour, activeDrainStats, zeroStats, biologicalSources);
        }

        internal static void DevCureFever()
        {
            Core.State ??= new Persistence.MMState();
            Core.State.FeverOnsetHours = 0f;
            Core.State.FeverLingerHoursRemaining = 0f;
            s_FeverMode = FeverMode.None;
            s_FeverInfectionRiskRecoveryHours = 0f;
            s_FeverTargetBodyTempC = 0f;
            s_LingeringFeverTargetBodyTempC = 0f;
            s_DevFeverSuppressedUntilThreatClears = true;
            SyncFeverAffliction(false);
            Core.Instance?.MarkDirty();
            AfflictionSaveHelper.QueueSurvivalSave();
            Core.Log("DEV: Fever cured and suppressed until all fever-producing threats clear.");
        }

        internal static bool IsEffectiveFeverActive()
        {
            return s_FeverMode == FeverMode.Moderate || s_FeverMode == FeverMode.Severe || s_FeverMode == FeverMode.Critical;
        }

        internal static bool ShouldFeverAfflictionBeVisible()
        {
            return s_FeverMode == FeverMode.Moderate || s_FeverMode == FeverMode.Severe || s_FeverMode == FeverMode.Critical || s_FeverMode == FeverMode.Lingering;
        }

        internal static float GetFeverFatigueMultiplier()
        {
            if (s_FeverMode == FeverMode.Moderate) return 2f;
            if (s_FeverMode == FeverMode.Severe) return 2.5f;
            if (s_FeverMode == FeverMode.Critical) return 3f;
            if (s_FeverMode == FeverMode.Lingering) return 1.25f;

            return 1f;
        }

        internal static float GetBodyHeatHydrationMultiplier()
        {
            if (!IsBodyHeatEnabled()) return 1f;

            float bodyTemp = Core.State.InternalBodyTemp;

            if (bodyTemp >= 43f) return 4f;
            if (bodyTemp >= 42f) return 3f;
            if (bodyTemp >= 41f) return 2.5f;
            if (bodyTemp >= 40f) return 2f;
            if (bodyTemp >= 39f) return 1.5f;
            if (bodyTemp >= 38f) return 1f;

            return 1f;
        }

        internal static float GetRiskProgressMultiplier()
        {
            if (!IsEnabled()) return 1f;

            float shield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);
            float multiplier;

            if (shield >= 50f)
            {
                multiplier = Mathf.Lerp(1f, 0.5f, (shield - 50f) / 50f);
            }
            else
            {
                multiplier = Mathf.Lerp(4f, 1f, shield / 50f);
            }

            if (IsEffectiveFeverActive()) multiplier *= 0.90f;

            return Mathf.Clamp(multiplier, 0.45f, 4f);
        }

        internal static float GetNecrosisRiskProgressMultiplier()
        {
            if (!IsEnabled()) return 1f;

            float shield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);

            if (shield >= 75f) return 0.85f;
            if (shield >= 50f) return 1f;
            if (shield >= 25f) return 1.25f;
            if (shield > 0f) return 1.5f;

            return 1.75f;
        }

        internal static float GetNecrosisRiskRecoveryMultiplier()
        {
            if (!IsEnabled()) return 1f;

            float shield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);

            if (shield >= 75f) return 1.25f;
            if (shield >= 50f) return 1f;
            if (shield >= 25f) return 0.75f;
            if (shield > 0f) return 0.5f;

            return 0.25f;
        }

        internal static string GetFirstAidText()
        {
            if (!IsEnabled()) return "0%";

            return $"{Core.State.ImmunityShield:0}%";
        }

        internal static string GetBodyHeatFirstAidText()
        {
            return $"{Core.State.InternalBodyTemp:0.0}°C";
        }

        internal static void UpdateBodyHeatRealtimeEffects(string scene)
        {
            if (GameManager.m_Instance == null || GameManager.m_IsPaused)
            {
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            if (Settings.options == null || !IsBodyHeatEnabled() || scene == "MainMenu" || scene == "Boot" || scene == "Empty")
            {
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            UpdateHeatNeurologicalEffects();
            UpdateHeatHeadacheEffect();
        }

        internal static void UpdateBodyHeat(string scene, float gameHoursPassed)
        {
            if (GameManager.m_Instance == null || GameManager.m_IsPaused)
            {
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            if (Settings.options == null)
            {
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            if (!IsBodyHeatEnabled())
            {
                s_BodyHeatGameSecondsAccum = 0.0;
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            if (scene == "MainMenu" || scene == "Boot" || scene == "Empty")
            {
                s_BodyHeatGameSecondsAccum = 0.0;
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            if (gameHoursPassed <= 0f) return;

            if (gameHoursPassed > 12f)
            {
                s_BodyHeatGameSecondsAccum = 0.0;

                Core.Warn("Body Heat time jump detected (>12 game hours). Reset accumulator.");

                return;
            }

            PlayerManager pm = GameManager.GetPlayerManagerComponent();
            Weather wc = GameManager.GetWeatherComponent();
            Freezing freezing = GameManager.GetFreezingComponent();

            if (pm == null || wc == null || freezing == null)
            {
                ResetHeatNeurologicalEffects();
                ResetHeatHeadacheEffect();
                return;
            }

            float oldBodyTemp = Core.State.InternalBodyTemp;

            GetMovementState(pm, out bool walking, out bool encumbered, out bool sprinting, out bool climbing);

            float ambientFeelsLikeC = GetAmbientFeelsLikeC(pm, wc);
            float fireHeatC = GetNearbyFireHeatC();
            float totalFeelsLikeC = ambientFeelsLikeC + fireHeatC;

            float warmth01 = GetWarmth01(freezing);
            bool effortHeatAllowed = warmth01 > 0.25f;

            float baseTargetC = Mathf.Lerp(34f, 37f, warmth01);
            float targetC = baseTargetC;

            float passive01 = Mathf.Clamp01(Mathf.InverseLerp(10f, 30f, ambientFeelsLikeC));
            float fire01 = Mathf.Clamp01(fireHeatC / 80f);

            if (passive01 > 0f)
            {
                float passiveTargetC = effortHeatAllowed
                    ? Mathf.Lerp(37f, 39f, passive01)
                    : Mathf.Lerp(baseTargetC, 37f, passive01);

                targetC = Mathf.Max(targetC, passiveTargetC);
            }

            if (fire01 > 0f)
            {
                float fireTargetC = effortHeatAllowed
                    ? Mathf.Lerp(37f, 40f, fire01)
                    : Mathf.Lerp(baseTargetC, 37f, fire01);

                targetC = Mathf.Max(targetC, fireTargetC);
            }

            if (effortHeatAllowed)
            {
                if (walking) targetC = Mathf.Max(targetC, 37.5f);
                if (encumbered) targetC = Mathf.Max(targetC, 38f);
                if (sprinting) targetC = Mathf.Max(targetC, 39f);
                if (climbing) targetC = Mathf.Max(targetC, 39f);
            }

            float rawFeverTargetC = GetFeverBodyHeatTargetC();
            float feverTargetC = GetEffectiveFeverBodyHeatTargetC(rawFeverTargetC, baseTargetC, totalFeelsLikeC);

            if (feverTargetC > 37f) targetC = Mathf.Max(targetC, feverTargetC);

            targetC = Mathf.Clamp(targetC, 34f, 43f);

            float heatGainPerHour = 0f;

            if (passive01 > 0f) heatGainPerHour += Settings.options.PassiveHeatGain * passive01;
            if (fire01 > 0f) heatGainPerHour += 8f * fire01;

            if (effortHeatAllowed && Core.State.InternalBodyTemp < 39f)
            {
                if (walking) heatGainPerHour += Settings.options.WalkHeatGain;
                if (encumbered) heatGainPerHour += Settings.options.EncumberedHeatGain;
                if (sprinting) heatGainPerHour += Settings.options.SprintHeatGain;
                if (climbing) heatGainPerHour += Settings.options.ClimbHeatGain;
            }

            if (feverTargetC > 37f) heatGainPerHour += 4f * Mathf.Clamp01(Mathf.InverseLerp(37f, 43f, feverTargetC));

            float coolingPerHour = GetBodyHeatCoolingPerHour(totalFeelsLikeC, warmth01);
            float changePerHour = targetC > Core.State.InternalBodyTemp ? heatGainPerHour : coolingPerHour;

            if (changePerHour > 0f)
                Core.State.InternalBodyTemp = Mathf.MoveTowards(Core.State.InternalBodyTemp, targetC, changePerHour * gameHoursPassed);

            Core.State.InternalBodyTemp = Mathf.Clamp(Core.State.InternalBodyTemp, 34f, 43f);

            if (!Mathf.Approximately(oldBodyTemp, Core.State.InternalBodyTemp)) Core.Instance?.MarkDirty();

            float sweat01 = Mathf.Clamp01(Mathf.InverseLerp(37.5f, 39.5f, Core.State.InternalBodyTemp));
            bool triggered = sweat01 > 0f;


            if (!triggered)
            {
                s_BodyHeatGameSecondsAccum = 0.0;
                return;
            }

            s_BodyHeatGameSecondsAccum += (double)gameHoursPassed * 3600.0;
            if (s_BodyHeatGameSecondsAccum < 5f) return;

            int ticks = (int)(s_BodyHeatGameSecondsAccum / 5f);
            s_BodyHeatGameSecondsAccum -= ticks * 5f;

            float perTick = Mathf.Lerp(0.02f, 0.2f, sweat01);
            float totalAmount = perTick * ticks;


            ApplySweat(pm, totalAmount);
        }

        private static bool IsEnabled()
        {
            return Settings.options != null && Settings.options.EnableImmunityShield;
        }

        private static bool IsBodyHeatEnabled()
        {
            return Settings.options != null && Settings.options.EnableBodyHeat;
        }

        internal static bool IsBodyHeatHypothermiaSourceActive()
        {
            bool before = Core.State.BodyHeatHypothermiaSourceActive;
            bool after = before;

            if (!Core.IsGameplayEnabled || !IsBodyHeatEnabled())
            {
                after = false;
            }
            else
            {
                float bodyHeat = Mathf.Clamp(Core.State.InternalBodyTemp, 34f, 43f);

                if (!before && bodyHeat <= 34.5f) after = true;
                else if (before && bodyHeat >= 35f) after = false;
            }

            if (before != after)
            {
                Core.State.BodyHeatHypothermiaSourceActive = after;
                Core.Instance?.MarkDirty();
                Core.Log($"Body Heat hypothermia source -> {(after ? "active" : "inactive")} at {Core.State.InternalBodyTemp:0.0}C.");
            }

            return after;
        }

        internal static bool IsBodyHeatHypothermiaRecoveryAllowed()
        {
            if (!Core.IsGameplayEnabled || !IsBodyHeatEnabled()) return true;

            return Core.State.InternalBodyTemp >= 35f;
        }

        private static float GetAmbientFeelsLikeC(PlayerManager pm, Weather wc)
        {
            float airTemp = wc.GetCurrentTemperature();
            float clothingBonus = pm.m_WarmthBonusFromClothing;
            float windChill = wc.GetCurrentWindchill();
            float clothingWindBonus = pm.m_WindproofBonusFromClothing;
            float netWindChill = Mathf.Min(windChill + clothingWindBonus, 0f);

            return airTemp + clothingBonus + netWindChill;
        }

        private static float GetWarmth01(Freezing freezing)
        {
            if (freezing == null) return 1f;

            float maxFreezing = Mathf.Max(1f, freezing.m_MaxFreezing);
            return Mathf.Clamp01(1f - freezing.m_CurrentFreezing / maxFreezing);
        }

        private static float GetFeverBodyHeatTargetC()
        {
            if (s_FeverMode == FeverMode.Moderate ||
                s_FeverMode == FeverMode.Severe ||
                s_FeverMode == FeverMode.Critical ||
                s_FeverMode == FeverMode.Lingering)
            {
                return Mathf.Clamp(s_FeverTargetBodyTempC, 37f, 43f);
            }

            return 0f;
        }

        private static float GetEffectiveFeverBodyHeatTargetC(float rawFeverTargetC, float baseTargetC, float totalFeelsLikeC)
        {
            if (rawFeverTargetC <= 37f) return 0f;

            float cold01 = Mathf.Clamp01(Mathf.InverseLerp(10f, -30f, totalFeelsLikeC));
            if (cold01 <= 0f) return rawFeverTargetC;

            float coldTargetC = Mathf.Min(37f, baseTargetC);
            float suppression01 = cold01 * 0.75f;

            return Mathf.Clamp(Mathf.Lerp(rawFeverTargetC, coldTargetC, suppression01), coldTargetC, rawFeverTargetC);
        }

        private static float GetBodyHeatCoolingPerHour(float totalFeelsLikeC, float warmth01)
        {
            float baseCooling = Settings.options.CoolingLoss;

            float ambientMultiplier = 1f;

            if (totalFeelsLikeC < 10f)
            {
                float coldPressure01 = Mathf.Clamp01(Mathf.InverseLerp(10f, -30f, totalFeelsLikeC));
                ambientMultiplier = Mathf.Lerp(1f, 4f, coldPressure01);
            }

            float severeColdMultiplier = 1f;

            if (warmth01 < 0.25f)
            {
                float severeCold01 = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0f, warmth01));
                severeColdMultiplier = Mathf.Lerp(1f, 4f, severeCold01);
            }

            return baseCooling * ambientMultiplier * severeColdMultiplier;
        }

        private static float GetNearbyFireHeatC()
        {
            FireManager fireManager = GameManager.GetFireManagerComponent();
            Transform playerTransform = GameManager.GetPlayerTransform();

            if (fireManager == null || playerTransform == null) return 0f;
            if (FireManager.m_Fires == null) return 0f;

            Vector3 playerPos = playerTransform.position;

            float strongest = 0f;
            float extra = 0f;

            for (int i = 0; i < FireManager.m_Fires.Count; i++)
            {
                Fire fire = FireManager.m_Fires[i];
                if (fire == null || !fire.IsBurning()) continue;
                if (fire.m_HeatSource == null) continue;

                float heatAtPlayer = GetFireHeatAtPosition(fire, playerPos);
                if (heatAtPlayer <= 0f) continue;

                if (heatAtPlayer > strongest)
                {
                    extra += strongest;
                    strongest = heatAtPlayer;
                }
                else
                {
                    extra += heatAtPlayer;
                }
            }

            return strongest + extra * 0.33f;
        }

        private static float GetFireHeatAtPosition(Fire fire, Vector3 playerPos)
        {
            if (fire == null || fire.m_HeatSource == null) return 0f;

            try
            {
                return Mathf.Max(0f, fire.m_HeatSource.GetTempIncrease(playerPos));
            }
            catch
            {
                return Mathf.Max(0f, fire.GetCurrentTempIncrease());
            }
        }

        private static void UpdateHeatNeurologicalEffects()
        {
            float stagger01 = Mathf.Clamp01(Mathf.InverseLerp(42f, 43f, Core.State.InternalBodyTemp));

            if (stagger01 <= 0f)
            {
                ResetHeatNeurologicalEffects();
                return;
            }

            ApplyHeatPhysicalStagger(stagger01);
        }

        private static void UpdateHeatHeadacheEffect()
        {
            float headache01 = Mathf.Clamp01(Mathf.InverseLerp(40f, 43f, Core.State.InternalBodyTemp));

            if (headache01 <= 0f)
            {
                ResetHeatHeadacheEffect();

                if (s_LastHeatHeadacheActive)
                {
                    s_LastHeatHeadacheActive = false;
                    Core.Log($"Heat headache camera effect ended -> Body:{Core.State.InternalBodyTemp:0.0}C");
                }

                return;
            }

            s_HeatHeadachePulseRealTimer += Time.unscaledDeltaTime;

            float pulseInterval = Mathf.Lerp(14f, 5f, headache01);
            if (s_HeatHeadachePulseRealTimer < pulseInterval) return;

            s_HeatHeadachePulseRealTimer = 0f;

            try
            {
                CameraStatusEffects cameraStatusEffects = GameManager.GetCameraStatusEffects();
                if (cameraStatusEffects == null) return;

                float amount = Mathf.Lerp(0.1f, 0.6f, headache01);

                cameraStatusEffects.HeadachePulse(amount);

                if (!s_LastHeatHeadacheActive)
                {
                    s_LastHeatHeadacheActive = true;
                    Core.Log($"Heat headache camera effect started -> Body:{Core.State.InternalBodyTemp:0.0}C | Intensity:{headache01:0.00} | Amount:{amount:0.00}");
                }
            }
            catch
            {
            }
        }

        private static void ResetHeatHeadacheEffect()
        {
            s_HeatHeadachePulseRealTimer = 0f;
        }

        private static void ResetHeatNeurologicalEffects()
        {
            s_HeatStaggerPhase = 0f;
        }

        private static void ApplyHeatPhysicalStagger(float stagger01)
        {
            if (stagger01 <= 0f) return;

            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return;
                if (!pm.PlayerIsWalking() && !pm.PlayerIsSprinting()) return;

                var player = GameManager.GetVpFPSPlayer();
                if (player == null || player.Controller == null) return;

                Transform transform = player.transform;
                if (transform == null) return;

                float smoothStagger01 = stagger01 * stagger01 * stagger01;

                s_HeatStaggerPhase += Time.deltaTime * 0.70f;

                float side = Mathf.Sin(s_HeatStaggerPhase) * 0.00045f * smoothStagger01;
                float forward = Mathf.Sin(s_HeatStaggerPhase * 0.61f + 2.2f) * 0.00006f * smoothStagger01;

                Vector3 force = transform.right * side + transform.forward * forward;

                player.Controller.AddForce(force);
            }
            catch
            {
            }
        }

        private static void GetMovementState(PlayerManager pm, out bool walking, out bool encumbered, out bool sprinting, out bool climbing)
        {
            var enc = GameManager.GetEncumberComponent();

            walking = false;
            encumbered = false;
            sprinting = false;
            climbing = false;

            if (pm == null || enc == null) return;

            walking = pm.PlayerIsWalking();
            encumbered = enc.IsEncumbered();
            sprinting = pm.PlayerIsSprinting();
            climbing = pm.PlayerIsClimbing();
        }

        private static void ApplySweat(PlayerManager pm, float amount)
        {
            foreach (ClothingRegion region in BodyHeatRegions)
            {
                float overflow = 0f;

                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Base, amount * 0.55f + overflow, 80f);
                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Mid, amount * 0.25f + overflow, 60f);
                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Top, amount * 0.15f + overflow, 40f);
                AddWetnessUpTo(pm, region, ClothingLayer.Top2, amount * 0.05f + overflow, 30f);
            }
        }

        private static float AddWetnessUpTo(PlayerManager pm, ClothingRegion region, ClothingLayer layer, float add, float cap)
        {
            if (add <= 0f) return 0f;

            var gi = pm.GetClothingInSlot(region, layer);
            if (gi == null || gi.m_ClothingItem == null) return add;

            var ci = gi.m_ClothingItem;

            float before = ci.m_PercentWet;
            if (before >= cap) return add;

            float wanted = Mathf.Min(add, cap - before);
            if (wanted <= 0f) return add;

            float target = Mathf.Min(before + wanted, cap);
            ci.m_PercentWet = target;

            float after = ci.m_PercentWet;
            float delta = after - before;

            if (delta <= 0.0001f) return add;

            return add - delta;
        }

        private static bool TryGetBodyStats(out BodyStats stats)
        {
            stats = default;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            Hunger hunger = GameManager.GetHungerComponent();
            Thirst thirst = GameManager.GetThirstComponent();
            Freezing freezing = GameManager.GetFreezingComponent();
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            Condition condition = GameManager.GetConditionComponent();

            if (fatigue == null || hunger == null || thirst == null || freezing == null || player == null || condition == null) return false;

            float fatigue01 = Mathf.Clamp01(fatigue.GetNormalizedFatigue());
            float hungerMax = Mathf.Max(1f, hunger.GetAdjustedMaxReserveCalories());
            float thirstMax = Mathf.Max(1f, thirst.m_MaxThirst);
            float freezingMax = Mathf.Max(1f, freezing.m_MaxFreezing);

            stats.EnergyPercent = Mathf.Clamp01(1f - fatigue01) * 100f;
            stats.CaloriesPercent = Mathf.Clamp01(hunger.m_CurrentReserveCalories / hungerMax) * 100f;
            stats.HydrationPercent = Mathf.Clamp01(1f - thirst.m_CurrentThirst / thirstMax) * 100f;
            stats.WarmthPercent = Mathf.Clamp01(1f - freezing.m_CurrentFreezing / freezingMax) * 100f;
            stats.ConditionPercent = Mathf.Clamp01(condition.GetNormalizedCondition()) * 100f;
            stats.Sleeping = player.PlayerIsSleeping();

            return true;
        }

        private static float GetBodyStatDrainPerHour(BodyStats stats, out int activeDrainStats, out int zeroStats, out float zeroDrainMultiplier)
        {
            float[] values =
            [
                stats.EnergyPercent,
                stats.CaloriesPercent,
                stats.HydrationPercent,
                stats.WarmthPercent
            ];

            zeroStats = 0;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0.01f) zeroStats++;
            }

            zeroDrainMultiplier = zeroStats <= 0 ? 1f : 1f + zeroStats * 0.5f;

            activeDrainStats = 0;
            float rawTotal = 0f;

            for (int i = 0; i < values.Length; i++)
            {
                float drain = GetSingleStatDrainPerHour(values[i]);
                if (drain <= 0f) continue;

                activeDrainStats++;
                rawTotal += drain;
            }

            return rawTotal * zeroDrainMultiplier;
        }

        private static float GetSingleStatDrainPerHour(float statPercent)
        {
            if (statPercent >= 25f) return 0f;

            float pressure01 = Mathf.Clamp01((25f - statPercent) / 25f);
            return pressure01 * pressure01;
        }

        private static float GetRegenPerHour(BodyStats stats, bool biologicalThreat)
        {
            if (biologicalThreat) return 0f;

            float conditionRegenThreshold = Settings.options.MaxConditionPenaltiesBlockImmunityRegen ? 50f : 20f;

            if (stats.ConditionPercent <= conditionRegenThreshold) return 0f;

            if (stats.EnergyPercent <= 25f) return 0f;
            if (stats.CaloriesPercent <= 25f) return 0f;
            if (stats.HydrationPercent <= 25f) return 0f;
            if (stats.WarmthPercent <= 25f) return 0f;

            float regen = stats.Sleeping ? 1f : 0.10f;
            float multiplier = 1f;

            if (stats.EnergyPercent > 75f) multiplier += 0.025f;
            if (stats.CaloriesPercent > 75f) multiplier += 0.025f;
            if (stats.HydrationPercent > 75f) multiplier += 0.025f;
            if (stats.WarmthPercent > 75f) multiplier += 0.025f;

            if (stats.ConditionPercent > 75f) multiplier += 0.05f;

            if (HomeComfort.HomeComfortBuff.IsActive) multiplier *= 1.05f;

            return regen * multiplier;
        }

        private static float GetBiologicalThreatDrainPerHour(out bool biologicalThreat, out string biologicalSources)
        {
            float total = 0f;
            List<string> sources = [];

            void Add(bool active, float drain, string source)
            {
                if (!active) return;

                total += drain;
                sources.Add(source);
            }

            try
            {
                Condition condition = GameManager.GetConditionComponent();

                if (condition != null)
                {
                    Add(condition.HasSpecificAffliction(AfflictionType.InfectionRisk), 0.25f, "InfectionRisk");
                    Add(condition.HasSpecificAffliction(AfflictionType.Infection), 0.75f, "Infection");
                    Add(condition.HasSpecificAffliction(AfflictionType.IntestinalParasitesRisk), 0.20f, "ParasitesRisk");
                    Add(condition.HasSpecificAffliction(AfflictionType.IntestinalParasites), 0.50f, "Parasites");
                    Add(condition.HasSpecificAffliction(AfflictionType.FoodPoisioning), 0.50f, "FoodPoisoning");
                    Add(condition.HasSpecificAffliction(AfflictionType.Dysentery), 0.75f, "Dysentery");
                }

                Add(HasCustomAfflictionByTypeName("SepsisRiskAffliction"), 1f, "SepsisRisk");
                Add(HasCustomAfflictionByTypeName("SepsisAffliction"), 2f, "Sepsis");
                Add(HasCustomAfflictionByTypeName("CorpseSicknessRiskAffliction"), 0.40f, "CorpseSicknessRisk");
                Add(HasCustomAfflictionByTypeName("CorpseSicknessAffliction"), 0.80f, "CorpseSickness");

                float necrosisDrain = NecrosisManager.GetBiologicalShieldDrainPerHour(out string necrosisSources);
                if (necrosisDrain > 0f)
                {
                    total += necrosisDrain;
                    sources.Add(necrosisSources);
                }
            }
            catch
            {
                biologicalThreat = false;
                biologicalSources = "error";
                return 0f;
            }

            biologicalThreat = sources.Count > 0;
            biologicalSources = biologicalThreat ? string.Join(",", sources) : string.Empty;

            return total;
        }

        private static float GetBiologicalFeverCapC(out string feverSources)
        {
            float cap = 37f;
            List<string> sources = [];

            void Add(bool active, float feverCapC, string source)
            {
                if (!active) return;

                cap = Mathf.Max(cap, feverCapC);
                sources.Add($"{source}:{feverCapC:0.#}C");
            }

            try
            {
                Condition condition = GameManager.GetConditionComponent();

                if (condition != null)
                {
                    Add(condition.HasSpecificAffliction(AfflictionType.InfectionRisk), 39f, "InfectionRisk");
                    Add(condition.HasSpecificAffliction(AfflictionType.Infection), 41f, "Infection");
                    Add(condition.HasSpecificAffliction(AfflictionType.IntestinalParasites), 38f, "Parasites");
                    Add(condition.HasSpecificAffliction(AfflictionType.FoodPoisioning), 40f, "FoodPoisoning");
                    Add(condition.HasSpecificAffliction(AfflictionType.Dysentery), 41f, "Dysentery");
                }

                Add(HasCustomAfflictionByTypeName("SepsisAffliction"), 43f, "Sepsis");
                Add(HasCustomAfflictionByTypeName("CorpseSicknessAffliction"), 38f, "CorpseSickness");

                float necrosisCap = NecrosisManager.GetFeverCapC(out string necrosisSources);
                if (necrosisCap > 37f)
                {
                    cap = Mathf.Max(cap, necrosisCap);
                    sources.Add(necrosisSources);
                }
            }
            catch
            {
                feverSources = "error";
                return 37f;
            }

            feverSources = sources.Count > 0 ? string.Join(",", sources) : string.Empty;
            return cap;
        }

        private static string BuildDrainMode(int activeDrainStats, int zeroStats, float bodyDrainPerHour, float biologicalDrainPerHour, string biologicalSources)
        {
            if (bodyDrainPerHour > 0f && biologicalDrainPerHour > 0f)
                return $"draining (body:{bodyDrainPerHour:0.###}/h, bio:{biologicalDrainPerHour:0.###}/h {biologicalSources}, {activeDrainStats} low, {zeroStats} zero)";

            if (bodyDrainPerHour > 0f)
                return $"draining (body:{bodyDrainPerHour:0.###}/h, {activeDrainStats} low, {zeroStats} zero)";

            if (biologicalDrainPerHour > 0f)
                return $"draining (bio:{biologicalDrainPerHour:0.###}/h {biologicalSources})";

            return "draining";
        }

        private static void UpdateFeverState(float gameHoursPassed)
        {
            FeverMode oldMode = s_FeverMode;
            float oldOnset = Core.State.FeverOnsetHours;
            float oldLinger = Core.State.FeverLingerHoursRemaining;
            float oldTarget = s_FeverTargetBodyTempC;

            bool wasVisible = ShouldFeverAfflictionBeVisible();

            float feverCapC = GetBiologicalFeverCapC(out string feverSources);
            bool biologicalFeverThreat = feverCapC > 37f;

            if (!biologicalFeverThreat) s_DevFeverSuppressedUntilThreatClears = false;
            if (s_DevFeverSuppressedUntilThreatClears) biologicalFeverThreat = false;

            bool canHaveFever = biologicalFeverThreat && Core.State.ImmunityShield >= 50f;

            if (canHaveFever)
            {
                Core.State.FeverLingerHoursRemaining = 0f;
                s_LingeringFeverTargetBodyTempC = 0f;

                if (wasVisible && s_FeverMode != FeverMode.Building)
                {
                    Core.State.FeverOnsetHours = 1f;
                    s_FeverMode = GetFeverModeForTargetC(feverCapC);
                    s_FeverTargetBodyTempC = feverCapC;
                }
                else
                {
                    Core.State.FeverOnsetHours = Mathf.Min(Core.State.FeverOnsetHours + gameHoursPassed, 1f);

                    if (Core.State.FeverOnsetHours >= 1f)
                    {
                        s_FeverMode = GetFeverModeForTargetC(feverCapC);
                        s_FeverTargetBodyTempC = feverCapC;
                    }
                    else
                    {
                        s_FeverMode = FeverMode.Building;
                        s_FeverTargetBodyTempC = 0f;
                    }
                }
            }
            else
            {
                Core.State.FeverOnsetHours = 0f;

                if (wasVisible && Core.State.FeverLingerHoursRemaining <= 0f)
                {
                    Core.State.FeverLingerHoursRemaining = 3f;
                    s_LingeringFeverTargetBodyTempC = Mathf.Max(s_FeverTargetBodyTempC, 37f);
                }

                if (Core.State.FeverLingerHoursRemaining > 0f)
                {
                    Core.State.FeverLingerHoursRemaining = Mathf.Max(0f, Core.State.FeverLingerHoursRemaining - gameHoursPassed);

                    if (Core.State.FeverLingerHoursRemaining > 0f)
                    {
                        float linger01 = Mathf.Clamp01(Core.State.FeverLingerHoursRemaining / 3f);
                        s_FeverMode = FeverMode.Lingering;
                        s_FeverTargetBodyTempC = Mathf.Lerp(37f, s_LingeringFeverTargetBodyTempC, linger01);
                    }
                    else
                    {
                        s_FeverMode = FeverMode.None;
                        s_FeverTargetBodyTempC = 0f;
                        s_LingeringFeverTargetBodyTempC = 0f;
                    }
                }
                else
                {
                    s_FeverMode = FeverMode.None;
                    s_FeverTargetBodyTempC = 0f;
                    s_LingeringFeverTargetBodyTempC = 0f;
                }
            }

            bool stateChanged = oldMode != s_FeverMode;
            bool timerChanged = !Mathf.Approximately(oldOnset, Core.State.FeverOnsetHours) || !Mathf.Approximately(oldLinger, Core.State.FeverLingerHoursRemaining);
            bool targetChanged = !Mathf.Approximately(oldTarget, s_FeverTargetBodyTempC);

            LogFeverStateIfChanged(oldMode, feverSources);

            if (stateChanged || timerChanged || targetChanged) Core.Instance?.MarkDirty();
        }

        private static void LogFeverStateIfChanged(FeverMode oldMode, string feverSources)
        {
            if (Settings.options == null || !Settings.options.IsLogging) return;

            int targetBucket = Mathf.FloorToInt(s_FeverTargetBodyTempC * 2f);

            bool shouldLog =
                oldMode != s_FeverMode ||
                s_FeverMode != s_LastLoggedFeverModeState ||
                targetBucket != s_LastLoggedFeverTargetBucket ||
                !string.Equals(feverSources, s_LastLoggedFeverSources, StringComparison.Ordinal);

            if (!shouldLog) return;

            s_LastLoggedFeverModeState = s_FeverMode;
            s_LastLoggedFeverTargetBucket = targetBucket;
            s_LastLoggedFeverSources = feverSources ?? string.Empty;

            Core.Log($"Fever state -> {oldMode} => {s_FeverMode} | Shield:{Core.State.ImmunityShield:0.#}% | Sources:{feverSources} | Target:{s_FeverTargetBodyTempC:0.0}C | Fatigue x{GetFeverFatigueMultiplier():0.00} | Onset:{Core.State.FeverOnsetHours:0.##}h | Linger:{Core.State.FeverLingerHoursRemaining:0.##}h");
        }

        private static FeverMode GetFeverModeForTargetC(float targetC)
        {
            if (targetC <= 37f) return FeverMode.None;
            if (targetC <= 39f) return FeverMode.Moderate;
            if (targetC <= 41f) return FeverMode.Severe;

            return FeverMode.Critical;
        }

        private static void SyncFeverAffliction(bool shouldBeVisible)
        {
            FeverAffliction? fever = AfflictionLogic.GetAffliction<FeverAffliction>();

            if (shouldBeVisible)
            {
                if (fever == null)
                {
                    Core.Log($"Fever response visible -> Mode:{s_FeverMode} | Target:{s_FeverTargetBodyTempC:0.0}C | Shield:{Core.State.ImmunityShield:0.#}%");
                    new FeverAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                }

                return;
            }

            if (fever != null)
            {
                Core.Log($"Fever response ended -> Shield:{Core.State.ImmunityShield:0.#}%");
                fever.Cure();
            }
        }

        private static void UpdateFeverInfectionRiskRecovery(float gameHoursPassed)
        {
            if (!IsEnabled()) return;

            if (!IsEffectiveFeverActive())
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            if (Core.State.ImmunityShield < 90f)
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            InfectionRisk infectionRisk = GameManager.GetInfectionRiskComponent();
            if (infectionRisk == null || !infectionRisk.HasInfectionRisk())
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            s_FeverInfectionRiskRecoveryHours += gameHoursPassed;

            while (s_FeverInfectionRiskRecoveryHours >= 1f)
            {
                s_FeverInfectionRiskRecoveryHours -= 1f;
                RollFeverInfectionRiskRecovery(infectionRisk);
            }
        }

        private static void RollFeverInfectionRiskRecovery(InfectionRisk infectionRisk)
        {
            if (infectionRisk == null) return;

            for (int i = infectionRisk.m_ElapsedHoursList.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= infectionRisk.m_ElapsedHoursList.Count) continue;
                if (!infectionRisk.RequiresAntiseptic(i)) continue;

                float roll = UnityEngine.Random.Range(0f, 100f);

                if (roll > 10f)
                {
                    Core.Log($"Fever failed to contain InfectionRisk -> Index:{i} | Mode:{s_FeverMode} | Target:{s_FeverTargetBodyTempC:0.0}C | Shield:{Core.State.ImmunityShield:0.#}% | Chance:{5f:0.#}% | Roll:{roll:0.#}");
                    continue;
                }

                Core.Log($"Fever contained InfectionRisk -> Index:{i} | Mode:{s_FeverMode} | Target:{s_FeverTargetBodyTempC:0.0}C | Shield:{Core.State.ImmunityShield:0.#}% | Chance:{5f:0.#}% | Roll:{roll:0.#}");
                infectionRisk.InfectionRiskEnd(i, false);
            }
        }

        private static bool HasCustomAfflictionByTypeName(string afflictionTypeName)
        {
            if (string.IsNullOrWhiteSpace(afflictionTypeName)) return false;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                object affliction = mgr.m_Afflictions[i];
                if (affliction == null) continue;

                Type type = affliction.GetType();

                if (string.Equals(type.Name, afflictionTypeName, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(type.FullName) && string.Equals(type.FullName, afflictionTypeName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static void LogStateIfChanged(string mode, BodyStats stats, float drainPerHour, float bodyDrainPerHour, float biologicalDrainPerHour, int activeDrainStats, int zeroStats, string biologicalSources)
        {
            int shieldBucket = Mathf.FloorToInt(Core.State.ImmunityShield / 5f) * 5;
            string modeBucket = GetShieldLogModeBucket(mode);
            int drainBucket = Mathf.FloorToInt(drainPerHour * 4f);
            int bodyDrainBucket = Mathf.FloorToInt(bodyDrainPerHour * 4f);
            int biologicalDrainBucket = Mathf.FloorToInt(biologicalDrainPerHour * 4f);
            string sources = biologicalSources ?? string.Empty;
            string fever = s_FeverMode != FeverMode.None ? $" | Fever:{s_FeverMode} Target:{s_FeverTargetBodyTempC:0.0}C" : string.Empty;

            bool shouldLog =
                shieldBucket != s_LastLoggedShieldBucket ||
                modeBucket != s_LastLoggedMode ||
                drainBucket != s_LastLoggedDrainBucket ||
                bodyDrainBucket != s_LastLoggedBodyDrainBucket ||
                biologicalDrainBucket != s_LastLoggedBiologicalDrainBucket ||
                activeDrainStats != s_LastLoggedLowStatCount ||
                zeroStats != s_LastLoggedZeroStatCount ||
                s_FeverMode != s_LastLoggedShieldFeverMode ||
                !string.Equals(sources, s_LastLoggedBiologicalSources, StringComparison.Ordinal);

            if (!shouldLog) return;

            s_LastLoggedShieldBucket = shieldBucket;
            s_LastLoggedMode = modeBucket;
            s_LastLoggedDrainBucket = drainBucket;
            s_LastLoggedBodyDrainBucket = bodyDrainBucket;
            s_LastLoggedBiologicalDrainBucket = biologicalDrainBucket;
            s_LastLoggedLowStatCount = activeDrainStats;
            s_LastLoggedZeroStatCount = zeroStats;
            s_LastLoggedShieldFeverMode = s_FeverMode;
            s_LastLoggedBiologicalSources = sources;

            Core.Log($"ImmunityShield -> {Core.State.ImmunityShield:0.#}% | {mode} | Drain:{drainPerHour:0.###}/h | Body:{bodyDrainPerHour:0.###}/h | Bio:{biologicalDrainPerHour:0.###}/h{fever} | Cond:{stats.ConditionPercent:0}% Energy:{stats.EnergyPercent:0}% Cal:{stats.CaloriesPercent:0}% Hyd:{stats.HydrationPercent:0}% Warm:{stats.WarmthPercent:0}%");
        }

        private static string GetShieldLogModeBucket(string mode)
        {
            if (mode.StartsWith("draining", StringComparison.Ordinal)) return "draining";
            return mode;
        }

        private static GameObject? FindChild(GameObject parent, string childName)
        {
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                if (child.name == childName) return child;
            }

            return null;
        }

        private static GameObject GetOrCreateFirstAidWidget(GameObject statusBars, GameObject sourceSection, string widgetName, bool sourceBackgroundAlreadyReduced)
        {
            GameObject? widget = FindChild(statusBars, widgetName);
            if (widget != null) return widget;

            widget = UnityEngine.Object.Instantiate(sourceSection, sourceSection.transform.parent);
            widget.name = widgetName;

            ReduceClonedFirstAidWidgetBackground(widget, sourceBackgroundAlreadyReduced);

            return widget;
        }

        private static void ReduceClonedFirstAidWidgetBackground(GameObject widget, bool sourceBackgroundAlreadyReduced)
        {
            if (sourceBackgroundAlreadyReduced) return;
            if (widget == null || widget.transform.childCount <= 3) return;

            GameObject background = widget.transform.GetChild(3).gameObject;
            int backgroundId = background.GetInstanceID();

            if (s_FirstAidReducedCloneBackgrounds.Contains(backgroundId)) return;

            Vector3 scale = background.transform.localScale;
            scale.x -= FirstAidWidgetBackgroundWidthReduction;
            background.transform.localScale = scale;

            s_FirstAidReducedCloneBackgrounds.Add(backgroundId);
        }

        private static void RefreshFirstAidWidget(GameObject anchorSection, GameObject widget, string label, string text, float xOffset, float yOffset, bool enabled)
        {
            Vector3 pos = anchorSection.transform.position;
            pos.x += xOffset;
            pos.y += yOffset;
            widget.transform.position = pos;

            widget.SetActive(enabled);
            if (!enabled) return;

            widget.transform.GetChild(1).GetComponent<UILabel>().text = label;
            widget.transform.GetChild(2).GetComponent<UILabel>().text = text;
        }

        private static void RefreshFirstAidWidgets(Panel_FirstAid panel)
        {
            GameObject statusBars = panel.gameObject.transform.GetChild(2).gameObject;
            GameObject caloriesSection = statusBars.transform.GetChild(12).gameObject;

            bool bodyHeatEnabled = IsBodyHeatEnabled();
            bool immunityEnabled = IsEnabled();
            bool sourceBackgroundAlreadyReduced = FindChild(statusBars, "Blood Drug Level") != null;

            GameObject bodyHeatSection = GetOrCreateFirstAidWidget(statusBars, caloriesSection, "Body Heat", sourceBackgroundAlreadyReduced);
            GameObject immunitySection = GetOrCreateFirstAidWidget(statusBars, caloriesSection, "Immunity Shield", sourceBackgroundAlreadyReduced);

            RefreshFirstAidWidget(caloriesSection, bodyHeatSection, "BODY HEAT", GetBodyHeatFirstAidText(), -0.31f, 0.98f, bodyHeatEnabled);
            RefreshFirstAidWidget(bodyHeatSection, immunitySection, "IMMUNITY SHIELD", GetFirstAidText(), 0.33f, 0f, immunityEnabled);
        }

        private enum FeverMode
        {
            None,
            Building,
            Moderate,
            Severe,
            Critical,
            Lingering
        }

        private struct BodyStats
        {
            public float EnergyPercent;
            public float CaloriesPercent;
            public float HydrationPercent;
            public float WarmthPercent;
            public float ConditionPercent;
            public bool Sleeping;
        }

        private struct InfectionRiskPatchState
        {
            public bool Applied;
            public float OriginalIncreasePerHour;
        }

        [HarmonyPatch(typeof(InfectionRisk), nameof(InfectionRisk.UpdateInfectionRisk))]
        private static class InfectionRisk_UpdateInfectionRisk_Patch
        {
            private static void Prefix(InfectionRisk __instance, int index, ref InfectionRiskPatchState __state)
            {
                __state = default;

                if (!IsEnabled()) return;
                if (__instance == null || index < 0 || index >= __instance.m_ElapsedHoursList.Count) return;
                if (__instance.IsConstant(index)) return;

                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return;

                float vanillaDelta = tod.GetTODHours(Time.deltaTime);
                if (vanillaDelta <= 0f) return;

                float multiplier = GetRiskProgressMultiplier();
                if (Mathf.Approximately(multiplier, 1f)) return;

                __instance.m_ElapsedHoursList[index] += vanillaDelta * (multiplier - 1f);

                __state.Applied = true;
                __state.OriginalIncreasePerHour = __instance.m_InfectionChanceIncreasePerHour;
                __instance.m_InfectionChanceIncreasePerHour = __state.OriginalIncreasePerHour * multiplier;
            }

            private static void Postfix(InfectionRisk __instance, InfectionRiskPatchState __state)
            {
                if (!__state.Applied || __instance == null) return;

                __instance.m_InfectionChanceIncreasePerHour = __state.OriginalIncreasePerHour;
            }
        }

        [HarmonyPatch(typeof(IntestinalParasites), nameof(IntestinalParasites.AddRiskPercent))]
        private static class IntestinalParasites_AddRiskPercent_Patch
        {
            private static void Prefix(IntestinalParasites __instance, ref float __state)
            {
                __state = __instance != null ? __instance.m_CurrentInfectionChance : 0f;
            }

            private static void Postfix(IntestinalParasites __instance, float __state)
            {
                if (!IsEnabled()) return;
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;

                float current = __instance.m_CurrentInfectionChance;
                float added = current - __state;
                if (added <= 0f) return;

                float adjustedAdded = added * GetRiskProgressMultiplier();
                __instance.m_CurrentInfectionChance = Mathf.Clamp(__state + adjustedAdded, 1f, 100f);
            }
        }

        [HarmonyPatch(typeof(IntestinalParasites), nameof(IntestinalParasites.CheckForInfection))]
        private static class IntestinalParasites_CheckForInfection_Patch
        {
            private static void Prefix(IntestinalParasites __instance)
            {
                if (!IsEnabled()) return;
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;

                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return;

                float vanillaDelta = tod.GetTODHours(Time.deltaTime);
                if (vanillaDelta <= 0f) return;

                float multiplier = GetRiskProgressMultiplier();
                if (Mathf.Approximately(multiplier, 1f)) return;

                __instance.m_RiskElapsedHours += vanillaDelta * (multiplier - 1f);
            }

            private static void Postfix(IntestinalParasites __instance)
            {
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;
                if (__instance.m_RiskElapsedHours < 0f) __instance.m_RiskElapsedHours = 0f;
            }
        }

        [HarmonyPatch(typeof(Panel_FirstAid), nameof(Panel_FirstAid.Initialize))]
        private static class PanelFirstAid_Initialize_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Panel_FirstAid __instance)
            {
                RefreshFirstAidWidgets(__instance);
            }
        }

        [HarmonyPatch(typeof(Panel_FirstAid), nameof(Panel_FirstAid.RefreshStatusLabels))]
        private static class PanelFirstAid_RefreshStatusLabels_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Panel_FirstAid __instance)
            {
                RefreshFirstAidWidgets(__instance);
            }
        }
    }
}