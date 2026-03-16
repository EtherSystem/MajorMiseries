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
        private static readonly Color LOCK_COLOR = new(0.64f, 0.20f, 0.23f, 1f);

        internal static void ResetRuntime()
        {
            s_LockHud.Clear();
        }

        private sealed class LockHudVisual
        {
            public UISprite NormalArc = null!;
            public UISprite LockedArc = null!;
        }

        private static readonly Dictionary<int, LockHudVisual> s_LockHud = new();

        private static bool IsTrackedBar(StatusBar.StatusBarType type)
        {
            return type == StatusBar.StatusBarType.Hunger
                || type == StatusBar.StatusBarType.Thirst
                || type == StatusBar.StatusBarType.Fatigue
                || type == StatusBar.StatusBarType.Cold;
        }

        private static LockHudVisual? GetOrCreateLockHud(StatusBar statusBar)
        {
            if (statusBar == null || statusBar.m_FillSprite == null)
                return null;

            int id = statusBar.GetInstanceID();
            if (s_LockHud.TryGetValue(id, out LockHudVisual existing))
                return existing;

            UISprite baseFill = statusBar.m_FillSprite;
            if (baseFill == null || baseFill.gameObject == null)
                return null;

            Transform parent = baseFill.transform.parent;
            if (parent == null)
                return null;

            GameObject normalGo = UnityEngine.Object.Instantiate(baseFill.gameObject, parent);
            normalGo.name = $"{baseFill.gameObject.name}_MM_NormalArc";

            GameObject lockedGo = UnityEngine.Object.Instantiate(baseFill.gameObject, parent);
            lockedGo.name = $"{baseFill.gameObject.name}_MM_LockedArc";

            UISprite normalArc = normalGo.GetComponent<UISprite>();
            UISprite lockedArc = lockedGo.GetComponent<UISprite>();

            if (normalArc == null || lockedArc == null)
                return null;

            LockHudVisual visual = new()
            {
                NormalArc = normalArc,
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

            visual.NormalArc.gameObject.SetActive(false);
            visual.LockedArc.gameObject.SetActive(false);
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
            if (statusBar == null || !IsTrackedBar(statusBar.m_StatusBarType) || statusBar.m_FillSprite == null)
                return;

            LockHudVisual? visual = GetOrCreateLockHud(statusBar);
            if (visual == null)
                return;

            if (!RequiemStagesEffects.IsGaugeLocked(statusBar.m_StatusBarType))
            {
                HideHudVisual(statusBar, visual);
                return;
            }

            UISprite baseFill = statusBar.m_FillSprite;

            baseFill.gameObject.SetActive(false);

            CopyBaseSpriteLayout(baseFill, visual.NormalArc, 1, Color.white, 1f);

            visual.NormalArc.fillAmount = Mathf.Clamp01(baseFill.fillAmount);

            CopyBaseSpriteLayout(baseFill, visual.LockedArc, 0, LOCK_COLOR, 0.95f);

            visual.LockedArc.fillAmount = RequiemStagesEffects.GaugeLockPercent;

            Vector3 rot = baseFill.transform.localEulerAngles;
            rot.z += 270f;
            visual.LockedArc.transform.localEulerAngles = rot;
        }

        [HarmonyPatch(typeof(GenericStatusBarSpawner), nameof(GenericStatusBarSpawner.Awake))]
        internal static class GenericStatusBarSpawner_Awake_LockHudPatch
        {
            private static void Postfix(GenericStatusBarSpawner __instance)
            {
                if (__instance == null || __instance.m_SpawnedObject == null)
                    return;

                StatusBar statusBar = __instance.m_SpawnedObject.GetComponent<StatusBar>();
                if (statusBar == null || !IsTrackedBar(statusBar.m_StatusBarType))
                    return;

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