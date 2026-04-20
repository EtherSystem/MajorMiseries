using AfflictionComponent.Components;
using MajorMiseries.Patches;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
using static MajorMiseries.Afflictions.Dirge;
using static MajorMiseries.Afflictions.Knell;
using static MajorMiseries.Afflictions.Omen;
using static MajorMiseries.Afflictions.Requiem;
using static MajorMiseries.Afflictions.ScarredFlesh;
using static MajorMiseries.Afflictions.Sepsis;
using static MajorMiseries.Afflictions.SepsisRisk;
using static MajorMiseries.Afflictions.CorpseSicknessRisk;
using static MajorMiseries.Afflictions.CorpseSickness;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal class DevConsoleCommands
    {
        internal static void Register()
        {
            // -----------------requiem stages afflictions---------------------
            uConsole.RegisterCommand("omen", new Action(() =>
            {
                new OmenAffliction(AfflictionBodyArea.Head).Start();
                DisplayStagePopup.ShowStagePopup(1, "GAMEPLAY_Stage1Name");
            }));

            uConsole.RegisterCommand("dirge", new Action(() =>
            {
                new DirgeAffliction(AfflictionBodyArea.Head).Start();
                DisplayStagePopup.ShowStagePopup(2, "GAMEPLAY_Stage2Name");
            }));

            uConsole.RegisterCommand("knell", new Action(() =>
            {
                new KnellAffliction(AfflictionBodyArea.Head).Start();
                DisplayStagePopup.ShowStagePopup(3, "GAMEPLAY_Stage3Name");
            }));

            uConsole.RegisterCommand("requiem", new Action(() =>
            {
                new RequiemAffliction(AfflictionBodyArea.Head).Start();
                DisplayStagePopup.ShowStagePopup(4, "GAMEPLAY_Stage4Name");
            }));
            // -----------------------------------------------------------------

            uConsole.RegisterCommand("scarredflesh", new Action(() =>
            {
                new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("sepsisrisk", new Action(() =>
            {
                new SepsisRiskAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("sepsis", new Action(() =>
            {
                new SepsisAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("brokenleg", new Action(() =>
            {
                new BrokenLegAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight, Settings.options.BrokenLimbDurationMode == 1 ? 201.6f : 2016f).Start();
            }));

            uConsole.RegisterCommand("brokenarm", new Action(() =>
            {
                new BrokenArmAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight, Settings.options.BrokenLimbDurationMode == 1 ? 134.4f : 1344f).Start();
            }));

            uConsole.RegisterCommand("blacklungrisk", new Action(() =>
            {
                new BlackLungRiskAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("blacklung", new Action(() =>
            {
                new BlackLungAffliction(AfflictionBodyArea.Chest, Settings.options.BlackLungDurationMode == 1 ? 360f : 3600f).Start();
            }));

            uConsole.RegisterCommand("coexposure", new Action(() =>
            {
                new COExposureAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("copoisoning", new Action(() =>
            {
                new COPoisoningAffliction(AfflictionBodyArea.Chest, Random.Range(6f, 24f)).Start();
            }));

            uConsole.RegisterCommand("corpsesicknessrisk", new Action(() =>
            {
                new CorpseSicknessRiskAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("corpsesickness", new Action(() =>
            {
                new CorpseSicknessAffliction(AfflictionBodyArea.Head, Random.Range(48f, 96f)).Start();
            }));

            uConsole.RegisterCommand("maj_afflictions_cure", new Action(() =>
            {
                var mgr = AfflictionManager.GetAfflictionManagerInstance();
                if (mgr?.m_Afflictions == null) return;

                for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
                {
                    var a = mgr.m_Afflictions[i];
                    if (a == null) continue;

                    if (a is OmenAffliction
                        || a is DirgeAffliction
                        || a is KnellAffliction
                        || a is RequiemAffliction
                        || a is ScarredFleshAffliction
                        || a is BrokenLegAffliction
                        || a is BrokenArmAffliction
                        || a is SepsisRiskAffliction
                        || a is SepsisAffliction
                        || a is BlackLungRiskAffliction
                        || a is BlackLungAffliction
                        || a is COExposureAffliction
                        || a is COPoisoningAffliction
                        || a is CorpseSicknessRiskAffliction
                        || a is CorpseSicknessAffliction)
                    {
                        a.Cure();
                    }
                }
            }));

            uConsole.RegisterCommand("severeL", new Action(() =>
            {
                GameManager.GetSevereLacerations().ApplySevereLacerations("debug");
            }));

            uConsole.RegisterCommand("severeL_cure", new Action(() =>
            {
                GameManager.GetSevereLacerations().Cure();
            }));

            uConsole.RegisterCommand("infection", new Action(() =>
            {
                GameManager.GetInfectionComponent().InfectionStart("debug", (int)AfflictionBodyArea.Chest, true, false);
            }));

            uConsole.RegisterCommand("infection_cure", new Action(() =>
            {
                GameManager.GetInfectionComponent().Cure();
            }));

            uConsole.RegisterCommand("mm_testpopup", new Action(() =>
            {
                Patches.DisplayStagePopup.ShowStagePopup(1, "GAMEPLAY_OmenName");
            }));

            uConsole.RegisterCommand("reset_SFhistory", new Action(() =>
            {
                Core.State.ScarredFleshHistoryCount = 0;
                Core.Instance?.MarkDirty();

                uConsole.Log("ScarredFleshHistoryCount reset to 0");

                if (Settings.options.IsLogging)
                    Core.Log("ScarredFleshHistoryCount reset to 0", false);
            }));

            uConsole.RegisterCommand("set_SFhistory", new Action(() =>
            {
                var @params = uConsole.GetAllParameters();
                if (@params == null || @params.Count < 1)
                {
                    uConsole.Log("[value]");
                    return;
                }

                if (!int.TryParse(@params[0], out int v))
                {
                    uConsole.Log("value must be a number");
                    return;
                }

                Core.State.ScarredFleshHistoryCount = Mathf.Max(0, v);
                Core.Instance?.MarkDirty();

                uConsole.Log($"ScarredFleshHistoryCount set to {Core.State.ScarredFleshHistoryCount}");

                if (Settings.options.IsLogging)
                    Core.Log($"ScarredFleshHistoryCount set to {Core.State.ScarredFleshHistoryCount}", false);
            }));

            uConsole.RegisterCommand("reset_PH", new Action(() =>
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                Core.Instance?.MarkDirty();

                uConsole.Log("PredatorHostility reset to 0");

                if (Settings.options.IsLogging)
                    Core.Log("PredatorHostility reset to 0", false);
            }));

            uConsole.RegisterCommand("set_PH", new Action(() =>
            {
                var @params = uConsole.GetAllParameters();
                if (@params == null || @params.Count < 1)
                {
                    uConsole.Log("[value]");
                    return;
                }

                if (!int.TryParse(@params[0], out int v))
                {
                    uConsole.Log("value must be a number");
                    return;
                }

                Core.State.PredatorHostility = Mathf.Max(0, v);
                Core.State.HoursSinceLastPredatorKill = 0f;
                Core.Instance?.MarkDirty();

                uConsole.Log($"PredatorHostility set to {Core.State.PredatorHostility:0}");

                if (Settings.options.IsLogging)
                    Core.Log($"PredatorHostility set to {Core.State.PredatorHostility:0}", false);
            }));

            uConsole.RegisterCommand("reset_CE", new Action(() =>
            {
                Core.State.CorpseExposure = 0f;
                Core.Instance?.MarkDirty();

                uConsole.Log("CorpseExposure reset to 0");

                if (Settings.options.IsLogging)
                    Core.Log("CorpseExposure reset to 0", false);
            }));

            uConsole.RegisterCommand("set_CE", new Action(() =>
            {
                var @params = uConsole.GetAllParameters();
                if (@params == null || @params.Count < 1)
                {
                    uConsole.Log("[value]");
                    return;
                }

                if (!float.TryParse(@params[0], out float v))
                {
                    uConsole.Log("value must be a number");
                    return;
                }

                Core.State.CorpseExposure = Mathf.Clamp(v, 0f, AfflictionLogic.GetCorpseExposureMax());
                Core.Instance?.MarkDirty();

                uConsole.Log($"CorpseExposure set to {Core.State.CorpseExposure:0.##}");

                if (Settings.options.IsLogging)
                    Core.Log($"CorpseExposure set to {Core.State.CorpseExposure:0.##}", false);
            }));
        }
    }
}