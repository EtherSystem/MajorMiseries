using Il2CppTLD.AI;

namespace MajorMiseries.Patches
{
    internal static class WildlifePatches
    {
        private sealed class PendingKill
        {
            public AiSubType SubType;
            public DamageSource LastDamageSource;
        }

        private sealed class PredatorBaseline
        {
            public float CuriousFollowDistance;
            public float CuriousEnterStalkingChance;

            public float StalkingFollowDistance;
            public float StalkingBeginChasingDistance;
            public float StalkingBeginChasingWeakTargetDistance;
            public float StalkingLoseInterestChance;
            public float StalkingChanceWhenTargetDetected;
            public float ForceStalkPlayerDistance;

            public float PassingAttackRange;
        }

        private static readonly Dictionary<int, PendingKill> _pendingPredatorKills = new();
        private static readonly Dictionary<int, PredatorBaseline> _predatorBaselines = new();
        private static readonly Dictionary<int, AiMode> _lastLoggedPredatorModes = new();

        private static int _lastLoggedPredatorThreatLevel = -1;

        internal static void ResetRuntime()
        {
            _pendingPredatorKills.Clear();
            _predatorBaselines.Clear();
            _lastLoggedPredatorModes.Clear();
            _lastLoggedPredatorThreatLevel = -1;
            Core.Log("predator hostility tracker reset");
        }

        private static bool TryGetTrackedPredatorSubtype(BaseAi ai, out AiSubType subType)
        {
            subType = AiSubType.None;

            if (ai == null)
                return false;

            subType = ai.m_AiSubType;

            return subType == AiSubType.Wolf
                || subType == AiSubType.Bear
                || subType == AiSubType.Moose
                || subType == AiSubType.Cougar;
        }

        private static string GetPredatorName(AiSubType subType)
        {
            return subType switch
            {
                AiSubType.Wolf => "Wolf",
                AiSubType.Bear => "Bear",
                AiSubType.Moose => "Moose",
                AiSubType.Cougar => "Cougar",
                _ => subType.ToString()
            };
        }

        private static float GetPredatorHostilityGain(AiSubType subType)
        {
            return subType switch
            {
                AiSubType.Wolf => 1f,
                AiSubType.Moose => 2f,
                AiSubType.Bear => 3f,
                AiSubType.Cougar => 4f,
                _ => 0f
            };
        }

        private static void TrackPredatorPlayerDamage(BaseAi ai, DamageSource damageSource, string sourceHook)
        {
            if (!TryGetTrackedPredatorSubtype(ai, out AiSubType subType))
                return;

            if (damageSource != DamageSource.Player)
                return;

            if (ai.m_CurrentHP <= 0f)
                return;

            int key = ai.GetInstanceID();

            _pendingPredatorKills[key] = new PendingKill
            {
                SubType = subType,
                LastDamageSource = damageSource
            };

            Core.Log($"predator hostility track -> {GetPredatorName(subType)} marked as player-damaged ({sourceHook})");
        }

        private static void TryRegisterPredatorKill(BaseAi ai, string hookName)
        {
            if (!TryGetTrackedPredatorSubtype(ai, out _))
                return;

            int key = ai.GetInstanceID();

            if (!_pendingPredatorKills.TryGetValue(key, out PendingKill pending))
                return;

            _pendingPredatorKills.Remove(key);

            float hostilityAdded = GetPredatorHostilityGain(pending.SubType);
            if (hostilityAdded <= 0f)
                return;

            Core.Log($"predator hostility death -> {GetPredatorName(pending.SubType)} confirmed from {hookName}");
            AfflictionLogic.RegisterPredatorKill(hostilityAdded);
        }

        private static bool IsPredatorThreatTracked(BaseAi ai)
        {
            return TryGetTrackedPredatorSubtype(ai, out _);
        }

        private static PredatorBaseline GetOrCreatePredatorBaseline(BaseAi ai)
        {
            int key = ai.GetInstanceID();

            if (_predatorBaselines.TryGetValue(key, out PredatorBaseline existing))
                return existing;

            PredatorBaseline created = new()
            {
                CuriousFollowDistance = ai.m_CuriousFollowDistance,
                CuriousEnterStalkingChance = ai.m_CuriousEnterStalkingChance,

                StalkingFollowDistance = ai.m_StalkingFollowDistance,
                StalkingBeginChasingDistance = ai.m_StalkingBeginChasingDistance,
                StalkingBeginChasingWeakTargetDistance = ai.m_StalkingBeginChasingWeakTargetDistance,
                StalkingLoseInterestChance = ai.m_StalkingLoseInterestChance,
                StalkingChanceWhenTargetDetected = ai.m_StalkingChanceWhenTargetDetected,
                ForceStalkPlayerDistance = ai.m_ForceStalkPlayerDistance,

                PassingAttackRange = ai.m_PassingAttackRange
            };

            _predatorBaselines[key] = created;
            return created;
        }

        private static void ApplyPredatorStalkingThreat(BaseAi ai, PredatorBaseline baseline)
        {
            if (!IsPredatorThreatTracked(ai))
                return;

            float stalkMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();
            float chanceMultiplier = 1f + ((stalkMultiplier - 1f) * 0.5f);

            ai.m_CuriousFollowDistance = baseline.CuriousFollowDistance * stalkMultiplier;
            ai.m_CuriousEnterStalkingChance = Mathf.Clamp(baseline.CuriousEnterStalkingChance * chanceMultiplier, 0f, 100f);

            ai.m_StalkingFollowDistance = baseline.StalkingFollowDistance * stalkMultiplier;
            ai.m_CurrentStalkingFollowDistance = ai.m_StalkingFollowDistance;

            ai.m_StalkingBeginChasingDistance = baseline.StalkingBeginChasingDistance * stalkMultiplier;
            ai.m_StalkingBeginChasingWeakTargetDistance = baseline.StalkingBeginChasingWeakTargetDistance * stalkMultiplier;

            ai.m_StalkingChanceWhenTargetDetected = Mathf.Clamp(Mathf.RoundToInt(baseline.StalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);
            ai.m_StalkingLoseInterestChance = Mathf.Clamp(baseline.StalkingLoseInterestChance / Mathf.Max(1f, stalkMultiplier), 0f, 100f);

            ai.m_ForceStalkPlayerDistance = baseline.ForceStalkPlayerDistance * stalkMultiplier;
            ai.m_PassingAttackRange = baseline.PassingAttackRange * stalkMultiplier;
        }

        private static void MaybeLogPredatorThreatThreshold(BaseAi ai, PredatorBaseline baseline)
        {
            if (!IsPredatorThreatTracked(ai))
                return;

            int baseThreat = AfflictionLogic.GetBasePredatorThreatLevel();
            int dynamicThreat = AfflictionLogic.GetDynamicPredatorThreatLevel();
            int totalThreat = AfflictionLogic.GetTotalPredatorThreatLevel();

            if (totalThreat == _lastLoggedPredatorThreatLevel)
                return;

            _lastLoggedPredatorThreatLevel = totalThreat;

            float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
            float stalkMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();
            float chanceMultiplier = 1f + ((stalkMultiplier - 1f) * 0.5f);

            float newCuriousFollow = baseline.CuriousFollowDistance * stalkMultiplier;
            float newCuriousEnter = Mathf.Clamp(baseline.CuriousEnterStalkingChance * chanceMultiplier, 0f, 100f);

            float newStalkingFollow = baseline.StalkingFollowDistance * stalkMultiplier;
            float newBeginChase = baseline.StalkingBeginChasingDistance * stalkMultiplier;
            float newWeakBeginChase = baseline.StalkingBeginChasingWeakTargetDistance * stalkMultiplier;
            int newTargetDetected = Mathf.Clamp(Mathf.RoundToInt(baseline.StalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);
            float newLoseInterest = Mathf.Clamp(baseline.StalkingLoseInterestChance / Mathf.Max(1f, stalkMultiplier), 0f, 100f);
            float newForceStalk = baseline.ForceStalkPlayerDistance * stalkMultiplier;
            float newPassingAttack = baseline.PassingAttackRange * stalkMultiplier;

            Core.Log($"predator threat threshold -> Base:{baseThreat} | Dynamic:{dynamicThreat} | Total:{totalThreat} | Smell x{smellMultiplier:0.00} | Stalk x{stalkMultiplier:0.00}");

            Core.Log(
                $"predator stalking ranges -> {GetPredatorName(ai.m_AiSubType)} | " +
                $"CuriousFollow {baseline.CuriousFollowDistance:0.0}->{newCuriousFollow:0.0} | " +
                $"StalkFollow {baseline.StalkingFollowDistance:0.0}->{newStalkingFollow:0.0} | " +
                $"BeginChase {baseline.StalkingBeginChasingDistance:0.0}->{newBeginChase:0.0} | " +
                $"WeakBeginChase {baseline.StalkingBeginChasingWeakTargetDistance:0.0}->{newWeakBeginChase:0.0} | " +
                $"ForceStalk {baseline.ForceStalkPlayerDistance:0.0}->{newForceStalk:0.0} | " +
                $"PassingAttack {baseline.PassingAttackRange:0.0}->{newPassingAttack:0.0}"
            );

            Core.Log(
                $"predator stalking chances -> {GetPredatorName(ai.m_AiSubType)} | " +
                $"CuriousEnterStalk {baseline.CuriousEnterStalkingChance:0.00}->{newCuriousEnter:0.00} | " +
                $"TargetDetectedStalk {baseline.StalkingChanceWhenTargetDetected:0.00}->{newTargetDetected:0.00} | " +
                $"LoseInterest {baseline.StalkingLoseInterestChance:0.00}->{newLoseInterest:0.00}"
            );
        }

        private static bool IsInterestingPredatorMode(AiMode mode)
        {
            return mode == AiMode.Investigate
                || mode == AiMode.InvestigateSmell
                || mode == AiMode.Stalking
                || mode == AiMode.Attack
                || mode == AiMode.PassingAttack;
        }

        private static float GetDistanceToPlayer(BaseAi ai)
        {
            if (ai == null)
                return -1f;

            GameObject playerObject = GameManager.GetPlayerObject();
            if (playerObject == null)
                return -1f;

            return Vector3.Distance(ai.transform.position, playerObject.transform.position);
        }

        private static void MaybeLogPredatorModeTransition(BaseAi ai)
        {
            if (!IsPredatorThreatTracked(ai))
                return;

            int key = ai.GetInstanceID();
            AiMode currentMode = ai.GetAiMode();

            if (!_lastLoggedPredatorModes.TryGetValue(key, out AiMode lastMode))
            {
                _lastLoggedPredatorModes[key] = currentMode;
                return;
            }

            if (currentMode == lastMode)
                return;

            _lastLoggedPredatorModes[key] = currentMode;

            if (!IsInterestingPredatorMode(lastMode) && !IsInterestingPredatorMode(currentMode))
                return;

            float distance = GetDistanceToPlayer(ai);
            int threat = AfflictionLogic.GetTotalPredatorThreatLevel();

            if (distance >= 0f)
            {
                Core.Log($"{GetPredatorName(ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} at {distance:0.0} m | Threat={threat}");
            }
            else
            {
                Core.Log($"{GetPredatorName(ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} | Threat={threat}");
            }
        }

        // ------------------------------------------------------------------------
        //                                PATCHES
        // ------------------------------------------------------------------------

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.IsDamageFatal))]
        internal static class BaseAi_IsDamageFatal_Patch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.IsDamageFatal");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(DamageSource), typeof(string))]
        internal static class BaseAi_ApplyDamage_ShortPatch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.ApplyDamage(short)");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(float), typeof(DamageSource), typeof(string))]
        internal static class BaseAi_ApplyDamage_FullPatch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.ApplyDamage(full)");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.EnterDead))]
        internal static class BaseAi_EnterDead_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                TryRegisterPredatorKill(__instance, "BaseAi.EnterDead");
            }
        }

        [HarmonyPatch(typeof(AiCougar), nameof(AiCougar.EnterDead))]
        internal static class AiCougar_EnterDead_Patch
        {
            private static void Prefix(AiCougar __instance)
            {
                TryRegisterPredatorKill(__instance, "AiCougar.EnterDead");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Update))]
        internal static class BaseAi_UpdatePredatorThreat_Patch
        {
            private static void Postfix(BaseAi __instance)
            {
                if (!IsPredatorThreatTracked(__instance))
                    return;

                PredatorBaseline baseline = GetOrCreatePredatorBaseline(__instance);

                ApplyPredatorStalkingThreat(__instance, baseline);
                MaybeLogPredatorThreatThreshold(__instance, baseline);
                MaybeLogPredatorModeTransition(__instance);
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.GetRangeFromScentIntensity))]
        internal static class BaseAi_GetRangeFromScentIntensity_Patch
        {
            private static void Postfix(BaseAi __instance, ref float __result)
            {
                if (!IsPredatorThreatTracked(__instance))
                    return;

                __result *= AfflictionLogic.GetPredatorSmellDistanceMultiplier();
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.OnDestroy))]
        internal static class BaseAi_OnDestroy_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                if (__instance == null)
                    return;

                int key = __instance.GetInstanceID();
                _pendingPredatorKills.Remove(key);
                _predatorBaselines.Remove(key);
                _lastLoggedPredatorModes.Remove(key);
            }
        }
    }
}