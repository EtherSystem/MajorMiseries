using Il2CppTLD.AI;

namespace MajorMiseries.Patches
{
    internal static class WildlifePatches
    {
        static WildlifePatches()
        {
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<PredatorThreatController>();
        }

        private sealed class PendingKill
        {
            public AiSubType SubType;
            public DamageSource LastDamageSource;
        }

        public sealed class PredatorThreatController : MonoBehaviour
        {
            public PredatorThreatController(IntPtr ptr) : base(ptr) { }

            private BaseAi? _ai;
            private bool _initialized;
            private AiMode _lastLoggedMode = AiMode.None;

            internal float _curiousFollowDistance;
            internal float _curiousEnterStalkingChance;

            internal float _stalkingFollowDistance;
            internal float _stalkingBeginChasingDistance;
            internal float _stalkingBeginChasingWeakTargetDistance;
            internal float _stalkingLoseInterestChance;
            internal float _stalkingChanceWhenTargetDetected;
            internal float _forceStalkPlayerDistance;

            internal float _passingAttackRange;

            internal float _smellRange;
            internal float _hearFootstepsRange;
            internal float _detectionRange;
            internal float _rangeMeleeAttack;
            internal float _breakStalkingRange;
            internal float _breakStalkingTimeSeconds;

            internal void Initialize(BaseAi ai)
            {
                _ai = ai;

                if (_initialized) return;

                CaptureBaseline();
                _initialized = true;

                ApplyPredatorThreatValues();
            }

            private void CaptureBaseline()
            {
                if (_ai == null) return;

                _curiousFollowDistance = _ai.m_CuriousFollowDistance;
                _curiousEnterStalkingChance = _ai.m_CuriousEnterStalkingChance;

                _stalkingFollowDistance = _ai.m_StalkingFollowDistance;
                _stalkingBeginChasingDistance = _ai.m_StalkingBeginChasingDistance;
                _stalkingBeginChasingWeakTargetDistance = _ai.m_StalkingBeginChasingWeakTargetDistance;
                _stalkingLoseInterestChance = _ai.m_StalkingLoseInterestChance;
                _stalkingChanceWhenTargetDetected = _ai.m_StalkingChanceWhenTargetDetected;
                _forceStalkPlayerDistance = _ai.m_ForceStalkPlayerDistance;

                _passingAttackRange = _ai.m_PassingAttackRange;

                _smellRange = _ai.m_SmellRange;
                _hearFootstepsRange = _ai.m_HearFootstepsRange;
                _detectionRange = _ai.m_DetectionRange;
                _rangeMeleeAttack = _ai.m_RangeMeleeAttack;
                _breakStalkingRange = _ai.m_BreakSlalkingRange;
                _breakStalkingTimeSeconds = _ai.m_BreakStalkingTimeSeconds;
            }

            private void LateUpdate()
            {
                if (_ai == null) return;

                if (!IsPredatorThreatTracked(_ai)) return;

                if (GameManager.m_IsPaused || GameManager.s_IsGameplaySuspended || GameManager.s_IsAISuspended) return;

                if (_ai.m_CurrentHP <= 0f || _ai.GetAiMode() == AiMode.Dead) return;

                ApplyPredatorThreatValues();
                MaybeLogPredatorThreatThreshold(_ai, this);
                MaybeLogPredatorModeTransition();
            }

            private void ApplyPredatorThreatValues()
            {
                if (_ai == null) return;

                switch (_ai.m_AiSubType)
                {
                    case AiSubType.Wolf:
                        ApplyWolfThreatValues();
                        break;

                    case AiSubType.Bear:
                        ApplyBearThreatValues();
                        break;

                    case AiSubType.Moose:
                        ApplyMooseThreatValues();
                        break;

                    case AiSubType.Cougar:
                        ApplyCougarThreatValues();
                        break;
                }
            }

            private void ApplyWolfThreatValues()
            {
                if (_ai == null) return;

                float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
                float threatMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();

                _ai.m_SmellRange = _smellRange * smellMultiplier;
                _ai.m_HearFootstepsRange = _hearFootstepsRange * threatMultiplier;
                _ai.m_RangeMeleeAttack = _rangeMeleeAttack * threatMultiplier;

                _ai.m_StalkingFollowDistance = _stalkingFollowDistance * threatMultiplier;
                _ai.m_CurrentStalkingFollowDistance = _ai.m_StalkingFollowDistance;

                _ai.m_StalkingBeginChasingDistance = _stalkingBeginChasingDistance * threatMultiplier;
                _ai.m_StalkingBeginChasingWeakTargetDistance = _stalkingBeginChasingWeakTargetDistance * threatMultiplier;

                _ai.m_ForceStalkPlayerDistance = _forceStalkPlayerDistance * threatMultiplier;
                _ai.m_PassingAttackRange = _passingAttackRange * threatMultiplier;
            }

            private void ApplyBearThreatValues()
            {
                if (_ai == null) return;

                float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
                float threatMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();

                float scaledDetectionRange = _detectionRange * threatMultiplier;
                float scaledBeginChase = _stalkingBeginChasingDistance * threatMultiplier;
                float scaledWeakBeginChase = _stalkingBeginChasingWeakTargetDistance * threatMultiplier;

                _ai.m_SmellRange = _smellRange * smellMultiplier;
                _ai.m_HearFootstepsRange = _hearFootstepsRange * threatMultiplier;
                _ai.m_DetectionRange = scaledDetectionRange;

                _ai.m_StalkingBeginChasingDistance = scaledBeginChase;
                _ai.m_StalkingBeginChasingWeakTargetDistance = scaledWeakBeginChase;

                _ai.m_RangeMeleeAttack = _rangeMeleeAttack * threatMultiplier;

                _ai.m_StalkingFollowDistance = Mathf.Max(_stalkingFollowDistance * threatMultiplier, scaledDetectionRange);

                _ai.m_CurrentStalkingFollowDistance = _ai.m_StalkingFollowDistance;

                _ai.m_BreakSlalkingRange = Mathf.Max(_breakStalkingRange * threatMultiplier, scaledDetectionRange);
            }

            private void ApplyMooseThreatValues()
            {
                if (_ai == null) return;

                float threatMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();

                _ai.m_RangeMeleeAttack = _rangeMeleeAttack * threatMultiplier;
                _ai.m_DetectionRange = _detectionRange * threatMultiplier;
            }

            private void ApplyCougarThreatValues()
            {
                if (_ai == null) return;

                float stalkMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();
                float chanceMultiplier = 1f + ((stalkMultiplier - 1f) * 0.5f);

                _ai.m_CuriousFollowDistance = _curiousFollowDistance * stalkMultiplier;
                _ai.m_CuriousEnterStalkingChance = Mathf.Clamp(_curiousEnterStalkingChance * chanceMultiplier, 0f, 100f);

                _ai.m_StalkingFollowDistance = _stalkingFollowDistance * stalkMultiplier;
                _ai.m_CurrentStalkingFollowDistance = _ai.m_StalkingFollowDistance;

                _ai.m_StalkingBeginChasingDistance = _stalkingBeginChasingDistance * stalkMultiplier;
                _ai.m_StalkingBeginChasingWeakTargetDistance = _stalkingBeginChasingWeakTargetDistance * stalkMultiplier;

                _ai.m_StalkingChanceWhenTargetDetected = Mathf.Clamp(Mathf.RoundToInt(_stalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);

                _ai.m_StalkingLoseInterestChance = Mathf.Clamp(_stalkingLoseInterestChance / Mathf.Max(1f, stalkMultiplier), 0f, 100f);

                _ai.m_ForceStalkPlayerDistance = _forceStalkPlayerDistance * stalkMultiplier;
                _ai.m_PassingAttackRange = _passingAttackRange * stalkMultiplier;
            }

            private void MaybeLogPredatorModeTransition()
            {
                if (_ai == null) return;

                int key = _ai.GetInstanceID();
                AiMode currentMode = _ai.GetAiMode();

                if (_lastLoggedMode == AiMode.None)
                {
                    _lastLoggedMode = currentMode;
                    return;
                }

                if (currentMode == _lastLoggedMode) return;

                AiMode lastMode = _lastLoggedMode;
                _lastLoggedMode = currentMode;

                if (!IsInterestingPredatorMode(lastMode) && !IsInterestingPredatorMode(currentMode)) return;

                float distance = GetDistanceToPlayer(_ai);
                int threat = AfflictionLogic.GetTotalPredatorThreatLevel();

                if (distance >= 0f) Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} at {distance:0.0} m | Threat={threat}");
                else Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} | Threat={threat}");
            }
        }

        private static readonly Dictionary<int, PendingKill> _pendingPredatorKills = new();

        private static readonly Dictionary<AiSubType, int> _lastLoggedPredatorThreatLevelBySubtype = new();

        internal static void ResetRuntime()
        {
            _pendingPredatorKills.Clear();
            _lastLoggedPredatorThreatLevelBySubtype.Clear();
            Core.Log("predator hostility tracker reset");
        }

        private static bool TryGetTrackedPredatorSubtype(BaseAi ai, out AiSubType subType)
        {
            subType = AiSubType.None;

            if (ai == null) return false;

            subType = ai.m_AiSubType;

            return subType == AiSubType.Wolf || subType == AiSubType.Bear || subType == AiSubType.Moose || subType == AiSubType.Cougar;
        }

        private static bool IsPredatorThreatTracked(BaseAi ai)
        {
            return TryGetTrackedPredatorSubtype(ai, out _);
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

        private static PredatorThreatController? EnsureThreatController(BaseAi ai)
        {
            if (!IsPredatorThreatTracked(ai)) return null;

            PredatorThreatController? controller = ai.gameObject.GetComponent<PredatorThreatController>() ?? ai.gameObject.AddComponent<PredatorThreatController>();
            controller.Initialize(ai);
            return controller;
        }

        private static void TrackPredatorPlayerDamage(BaseAi ai, DamageSource damageSource, string sourceHook)
        {
            if (!TryGetTrackedPredatorSubtype(ai, out AiSubType subType)) return;

            if (damageSource != DamageSource.Player) return;

            if (ai.m_CurrentHP <= 0f) return;

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
            if (!TryGetTrackedPredatorSubtype(ai, out _)) return;

            int key = ai.GetInstanceID();

            if (!_pendingPredatorKills.TryGetValue(key, out PendingKill pending)) return;

            _pendingPredatorKills.Remove(key);

            float hostilityAdded = GetPredatorHostilityGain(pending.SubType);
            if (hostilityAdded <= 0f) return;

            Core.Log($"predator hostility death -> {GetPredatorName(pending.SubType)} confirmed from {hookName}");
            AfflictionLogic.RegisterPredatorKill(hostilityAdded);
        }

        private static bool IsInterestingPredatorMode(AiMode mode)
        {
            return mode == AiMode.Investigate || mode == AiMode.InvestigateSmell || mode == AiMode.Stalking || mode == AiMode.Attack || mode == AiMode.PassingAttack || mode == AiMode.HoldGround;
        }

        private static float GetDistanceToPlayer(BaseAi ai)
        {
            if (ai == null) return -1f;

            GameObject playerObject = GameManager.GetPlayerObject();
            if (playerObject == null) return -1f;

            return Vector3.Distance(ai.transform.position, playerObject.transform.position);
        }

        private static void MaybeLogPredatorThreatThreshold(BaseAi ai, PredatorThreatController controller)
        {
            if (!IsPredatorThreatTracked(ai)) return;

            int baseThreat = AfflictionLogic.GetBasePredatorThreatLevel();
            int dynamicThreat = AfflictionLogic.GetDynamicPredatorThreatLevel();
            int totalThreat = AfflictionLogic.GetTotalPredatorThreatLevel();

            if (_lastLoggedPredatorThreatLevelBySubtype.TryGetValue(ai.m_AiSubType, out int lastThreat) && lastThreat == totalThreat) return;

            _lastLoggedPredatorThreatLevelBySubtype[ai.m_AiSubType] = totalThreat;

            float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
            float threatMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();

            Core.Log(
                $"predator threat threshold -> {GetPredatorName(ai.m_AiSubType)} | " +
                $"Base:{baseThreat} | Dynamic:{dynamicThreat} | Total:{totalThreat} | " +
                $"Smell x{smellMultiplier:0.00} | Threat x{threatMultiplier:0.00}");

            switch (ai.m_AiSubType)
            {
                case AiSubType.Wolf:
                    {
                        Core.Log(
                            $"predator wolf values -> " +
                            $"SmellRange {controller._smellRange:0.0}->{controller._smellRange * smellMultiplier:0.0} | " +
                            $"HearFootsteps {controller._hearFootstepsRange:0.0}->{controller._hearFootstepsRange * threatMultiplier:0.0} | " +
                            $"MeleeRange {controller._rangeMeleeAttack:0.0}->{controller._rangeMeleeAttack * threatMultiplier:0.0} | " +
                            $"StalkFollow {controller._stalkingFollowDistance:0.0}->{controller._stalkingFollowDistance * threatMultiplier:0.0} | " +
                            $"BeginChase {controller._stalkingBeginChasingDistance:0.0}->{controller._stalkingBeginChasingDistance * threatMultiplier:0.0} | " +
                            $"WeakBeginChase {controller._stalkingBeginChasingWeakTargetDistance:0.0}->{controller._stalkingBeginChasingWeakTargetDistance * threatMultiplier:0.0} | " +
                            $"ForceStalk {controller._forceStalkPlayerDistance:0.0}->{controller._forceStalkPlayerDistance * threatMultiplier:0.0} | " +
                            $"PassingAttack {controller._passingAttackRange:0.0}->{controller._passingAttackRange * threatMultiplier:0.0}"
                        );

                        break;
                    }

                case AiSubType.Bear:
                    {
                        float scaledDetectionRange = controller._detectionRange * threatMultiplier;
                        float scaledStalkingFollowDistance = Mathf.Max(controller._stalkingFollowDistance * threatMultiplier, scaledDetectionRange);
                        float scaledBreakStalkingRange = Mathf.Max(controller._breakStalkingRange * threatMultiplier, scaledDetectionRange);

                        Core.Log(
                            $"predator bear values -> " +
                            $"SmellRange {controller._smellRange:0.0}->{controller._smellRange * smellMultiplier:0.0} | " +
                            $"HearFootsteps {controller._hearFootstepsRange:0.0}->{controller._hearFootstepsRange * threatMultiplier:0.0} | " +
                            $"DetectionRange {controller._detectionRange:0.0}->{scaledDetectionRange:0.0} | " +
                            $"BeginChase {controller._stalkingBeginChasingDistance:0.0}->{controller._stalkingBeginChasingDistance * threatMultiplier:0.0} | " +
                            $"WeakBeginChase {controller._stalkingBeginChasingWeakTargetDistance:0.0}->{controller._stalkingBeginChasingWeakTargetDistance * threatMultiplier:0.0} | " +
                            $"MeleeRange {controller._rangeMeleeAttack:0.0}->{controller._rangeMeleeAttack * threatMultiplier:0.0} | " +
                            $"StalkFollow {controller._stalkingFollowDistance:0.0}->{scaledStalkingFollowDistance:0.0} | " +
                            $"BreakStalkRange {controller._breakStalkingRange:0.0}->{scaledBreakStalkingRange:0.0}");

                        break;
                    }

                case AiSubType.Moose:
                    {
                        Core.Log(
                            $"predator moose values -> " +
                            $"MeleeRange {controller._rangeMeleeAttack:0.0}->{controller._rangeMeleeAttack * threatMultiplier:0.0} | " +
                            $"DetectionRange {controller._detectionRange:0.0}->{controller._detectionRange * threatMultiplier:0.0}");

                        break;
                    }

                case AiSubType.Cougar:
                    {
                        float chanceMultiplier = 1f + ((threatMultiplier - 1f) * 0.5f);

                        float newCuriousFollow = controller._curiousFollowDistance * threatMultiplier;
                        float newCuriousEnter = Mathf.Clamp(controller._curiousEnterStalkingChance * chanceMultiplier, 0f, 100f);

                        float newStalkingFollow = controller._stalkingFollowDistance * threatMultiplier;
                        float newBeginChase = controller._stalkingBeginChasingDistance * threatMultiplier;
                        float newWeakBeginChase = controller._stalkingBeginChasingWeakTargetDistance * threatMultiplier;
                        int newTargetDetected = Mathf.Clamp(Mathf.RoundToInt(controller._stalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);
                        float newLoseInterest = Mathf.Clamp(controller._stalkingLoseInterestChance / Mathf.Max(1f, threatMultiplier), 0f, 100f);
                        float newForceStalk = controller._forceStalkPlayerDistance * threatMultiplier;
                        float newPassingAttack = controller._passingAttackRange * threatMultiplier;

                        Core.Log(
                            $"predator cougar ranges -> " +
                            $"CuriousFollow {controller._curiousFollowDistance:0.0}->{newCuriousFollow:0.0} | " +
                            $"StalkFollow {controller._stalkingFollowDistance:0.0}->{newStalkingFollow:0.0} | " +
                            $"BeginChase {controller._stalkingBeginChasingDistance:0.0}->{newBeginChase:0.0} | " +
                            $"WeakBeginChase {controller._stalkingBeginChasingWeakTargetDistance:0.0}->{newWeakBeginChase:0.0} | " +
                            $"ForceStalk {controller._forceStalkPlayerDistance:0.0}->{newForceStalk:0.0} | " +
                            $"PassingAttack {controller._passingAttackRange:0.0}->{newPassingAttack:0.0}");

                        Core.Log(
                            $"predator cougar chances -> " +
                            $"CuriousEnterStalk {controller._curiousEnterStalkingChance:0.00}->{newCuriousEnter:0.00} | " +
                            $"TargetDetectedStalk {controller._stalkingChanceWhenTargetDetected:0.00}->{newTargetDetected:0.00} | " +
                            $"LoseInterest {controller._stalkingLoseInterestChance:0.00}->{newLoseInterest:0.00}");

                        break;
                    }
            }
        }

        // ------------------------------------------------------------------------
        //                                PATCHES
        // ------------------------------------------------------------------------

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Start))]
        internal static class BaseAi_Start_Patch
        {
            private static void Postfix(BaseAi __instance)
            {
                EnsureThreatController(__instance);
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Update))]
        internal static class BaseAi_Update_EnsureController_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                EnsureThreatController(__instance);
            }
        }

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
                AfflictionLogic.QueueAnimalCarcassReseed("BaseAi.EnterDead");
                TryRegisterPredatorKill(__instance, "BaseAi.EnterDead");
            }
        }

        [HarmonyPatch(typeof(AiCougar), nameof(AiCougar.EnterDead))]
        internal static class AiCougar_EnterDead_Patch
        {
            private static void Prefix(AiCougar __instance)
            {
                AfflictionLogic.QueueAnimalCarcassReseed("AiCougar.EnterDead");
                TryRegisterPredatorKill(__instance, "AiCougar.EnterDead");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.OnDestroy))]
        internal static class BaseAi_OnDestroy_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                if (__instance == null) return;

                int key = __instance.GetInstanceID();
                _pendingPredatorKills.Remove(key);
            }
        }
    }
}