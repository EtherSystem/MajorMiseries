using Il2CppTLD.Gameplay;

namespace MajorMiseries.Patches
{
    internal static class DisplayStagePopup
    {
        internal static void ShowStagePopup(int stage, string stageNameLocId)
        {
            MiseryHUD miseryHud = UnityEngine.Object.FindObjectOfType<MiseryHUD>();

            if (miseryHud == null)
            {
                MiseryHUD[] allHud = UnityEngine.Resources.FindObjectsOfTypeAll<MiseryHUD>();

                if (allHud != null && allHud.Length > 0)
                {
                    miseryHud = allHud[0];
                }
            }

            if (miseryHud == null)
            {
                Core.Log("MiseryHUD not found.");
                return;
            }

            Core.Log($"Sending popup: stage={stage}, locId={stageNameLocId}");
            miseryHud.EnqueueStage(stage, stageNameLocId);
        }
    }

    internal static class StageGaugeLockVisuals
    {
        private static readonly Color32 LOCK_COLOR = new(173, 55, 62, 255);
        private const float LOCK_ROTATION_TWEAK = -3.0f;
        private const float LOCK_FILL_EPSILON = 0.0025f;

        private const float SOUR_STOMACH_AVAILABLE_PERCENT = 0.60f;
        private const float BASE_LOCK_PERCENT = RequiemStagesEffects.GaugeLockPercent;

        private sealed class LockHudVisual
        {
            public UISprite LockedArc = null!;
        }

        private static readonly Dictionary<int, LockHudVisual> s_LockHud = new();

        internal static void ResetRuntime()
        {
            foreach (LockHudVisual visual in s_LockHud.Values)
            {
                if (visual?.LockedArc != null && visual.LockedArc.gameObject != null)
                {
                    UnityEngine.Object.Destroy(visual.LockedArc.gameObject);
                }
            }

            s_LockHud.Clear();
        }

        private static bool IsTrackedBar(StatusBar.StatusBarType type)
        {
            return type == StatusBar.StatusBarType.Hunger || type == StatusBar.StatusBarType.Thirst || type == StatusBar.StatusBarType.Fatigue || type == StatusBar.StatusBarType.Cold;
        }

        private static bool HasSourStomach()
        {
            Condition condition = GameManager.GetConditionComponent();
            return condition != null && condition.HasSpecificAffliction(AfflictionType.SourStomach);
        }

        private static float GetDisplayedLockPercent(StatusBar.StatusBarType type)
        {
            float availablePercent = RequiemStagesEffects.GaugeAvailablePercent;

            if (type == StatusBar.StatusBarType.Hunger && HasSourStomach())
            {
                availablePercent *= SOUR_STOMACH_AVAILABLE_PERCENT;
            }

            return Mathf.Clamp01(1f - availablePercent);
        }

        private static float GetDisplayedRotationOffset(StatusBar.StatusBarType type)
        {
            float rotationOffset = 270f;
            //float rotationOffset = type switch
            //{
            //    StatusBar.StatusBarType.Hunger => 270f + Settings.options.HungerLockArcRotation,
            //    StatusBar.StatusBarType.Thirst => 270f + Settings.options.ThirstLockArcRotation,
            //    StatusBar.StatusBarType.Fatigue => 270f + Settings.options.FatigueLockArcRotation,
            //    StatusBar.StatusBarType.Cold => 270f + Settings.options.ColdLockArcRotation,
            //    _ => 270f
            //};

            if (type == StatusBar.StatusBarType.Hunger && HasSourStomach())
            {
                float displayedLock = GetDisplayedLockPercent(type);
                float extraLock = displayedLock - RequiemStagesEffects.GaugeLockPercent;
                rotationOffset -= extraLock * 360f;
                rotationOffset += -3f;
                //rotationOffset += Settings.options.SourStomachExtraHungerRotation;
            }

            return rotationOffset;
        }

        private static LockHudVisual? GetOrCreateLockHud(StatusBar statusBar)
        {
            if (statusBar == null || statusBar.m_FillSprite == null) return null;

            int id = statusBar.GetInstanceID();
            if (s_LockHud.TryGetValue(id, out LockHudVisual existing)) return existing;

            UISprite baseFill = statusBar.m_FillSprite;
            if (baseFill == null || baseFill.gameObject == null) return null;

            Transform parent = baseFill.transform.parent;
            if (parent == null) return null;

            GameObject lockedGo = UnityEngine.Object.Instantiate(baseFill.gameObject, parent);
            lockedGo.name = $"{baseFill.gameObject.name}_MM_LockedArc";

            UISprite lockedArc = lockedGo.GetComponent<UISprite>();
            if (lockedArc == null) return null;

            lockedArc.gameObject.SetActive(false);

            LockHudVisual visual = new()
            {
                LockedArc = lockedArc
            };

            s_LockHud[id] = visual;
            return visual;
        }

        private static void HideHudVisual(StatusBar statusBar, LockHudVisual visual)
        {
            if (statusBar != null && statusBar.m_FillSprite != null)
            {
                statusBar.m_FillSprite.gameObject.SetActive(true);
            }

            if (visual?.LockedArc != null && visual.LockedArc.gameObject != null)
            {
                visual.LockedArc.gameObject.SetActive(false);
            }
        }

        private static void CopyBaseSpriteLayout(UISprite source, UISprite target, int depthOffset, Color color, float alpha)
        {
            target.gameObject.SetActive(true);

            target.atlas = source.atlas;
            target.spriteName = source.spriteName;
            target.type = source.type;
            target.fillDirection = source.fillDirection;
            target.invert = source.invert;
            target.flip = source.flip;

            target.width = source.width;
            target.height = source.height;
            target.depth = source.depth + depthOffset;

            target.color = color;
            target.alpha = alpha;

            target.transform.localPosition = source.transform.localPosition;
            target.transform.localScale = source.transform.localScale;
            target.transform.localEulerAngles = source.transform.localEulerAngles;
        }

        private static void SyncHudVisual(StatusBar statusBar)
        {
            if (statusBar == null || !IsTrackedBar(statusBar.m_StatusBarType) || statusBar.m_FillSprite == null) return;

            LockHudVisual? visual = GetOrCreateLockHud(statusBar);
            if (visual == null) return;

            UISprite baseFill = statusBar.m_FillSprite;

            if (!RequiemStagesEffects.IsGaugeLocked(statusBar.m_StatusBarType))
            {
                HideHudVisual(statusBar, visual);
                return;
            }

            baseFill.gameObject.SetActive(true);

            CopyBaseSpriteLayout(baseFill, visual.LockedArc, 1, LOCK_COLOR, 1f);
            visual.LockedArc.fillAmount = Mathf.Clamp01(GetDisplayedLockPercent(statusBar.m_StatusBarType) + LOCK_FILL_EPSILON);

            Vector3 rot = baseFill.transform.localEulerAngles;
            rot.z += GetDisplayedRotationOffset(statusBar.m_StatusBarType);
            visual.LockedArc.transform.localEulerAngles = rot;
        }

        [HarmonyPatch(typeof(GenericStatusBarSpawner), nameof(GenericStatusBarSpawner.Awake))]
        internal static class GenericStatusBarSpawner_Awake_LockHudPatch
        {
            private static void Postfix(GenericStatusBarSpawner __instance)
            {
                if (__instance == null || __instance.m_SpawnedObject == null) return;

                StatusBar statusBar = __instance.m_SpawnedObject.GetComponent<StatusBar>();
                if (statusBar == null || !IsTrackedBar(statusBar.m_StatusBarType)) return;

                GetOrCreateLockHud(statusBar);
            }
        }

        [HarmonyPatch(typeof(StatusBar), nameof(StatusBar.Update))]
        internal static class StatusBar_Update_LockHudPatch
        {
            private static void Postfix(StatusBar __instance)
            {
                SyncHudVisual(__instance);
            }
        }
    }
}