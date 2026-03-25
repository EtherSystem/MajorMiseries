using AfflictionComponent.Components;

namespace MajorMiseries.Resources.Localization
{
    internal static class LocalizationRefresh
    {
        private static bool _pendingRefresh = false;

        internal static void RequestRefresh()
        {
            _pendingRefresh = true;
        }

        internal static void FlushPendingRefresh()
        {
            if (!_pendingRefresh) return;

            string scene = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(scene) || scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            _pendingRefresh = false;
            RefreshActiveCustomAfflictions();
        }

        internal static void RefreshActiveCustomAfflictions()
        {
            string scene = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(scene) || scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                var aff = mgr.m_Afflictions[i];
                if (aff is ILocalizableAffliction localizable)
                {
                    localizable.RefreshLocalization();
                }
            }

            AfflictionLogic.ForceRefreshEffects();

            var firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
            firstAid?.UpdateDueToAfflictionHealed();

            Core.Log("Localization refreshed for active custom afflictions");
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.Localization), nameof(Il2Cpp.Localization.LoadStringTableForLanguage))]
    internal static class LocalizationRefreshPatch
    {
        private static void Postfix()
        {
            string scene = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(scene) || scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            LocalizationRefresh.RequestRefresh();

            Core.Log("Localization reload detected -> queued affliction refresh");
        }
    }
}