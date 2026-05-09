using AfflictionComponent.Components;
using MajorMiseries.Patches;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.Buffs.HomeComfort;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
using static MajorMiseries.Afflictions.CorpseSickness;
using static MajorMiseries.Afflictions.CorpseSicknessRisk;
using static MajorMiseries.Afflictions.HomeSickness;
using static MajorMiseries.Afflictions.RegionalDistress;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Dirge;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Knell;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Omen;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Requiem;
using static MajorMiseries.Afflictions.ScarredFlesh;
using static MajorMiseries.Afflictions.Sepsis;
using static MajorMiseries.Afflictions.SepsisRisk;
using static MajorMiseries.Afflictions.SevereAnkleSprain;
using static MajorMiseries.Afflictions.SevereAnkleSprainRisk;
using static MajorMiseries.Afflictions.SevereWristSprain;
using static MajorMiseries.Afflictions.SevereWristSprainRisk;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal class DevConsoleCommands
    {
        internal static void Register()
        {
            // -------------------- Requiem Stages --------------------

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


            // -------------------- Scarred Flesh --------------------

            uConsole.RegisterCommand("scarredflesh", new Action(() =>
            {
                AfflictionLogic.AddScarredFleshStack("Debug command scarredflesh");

                uConsole.Log($"ScarredFleshHistoryCount increased to {Core.State.ScarredFleshHistoryCount}");
            }));


            // -------------------- Sepsis --------------------

            uConsole.RegisterCommand("sepsisrisk", new Action(() =>
            {
                new SepsisRiskAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("sepsis", new Action(() =>
            {
                new SepsisAffliction(AfflictionBodyArea.Chest).Start();
            }));


            // -------------------- Broken Leg & Broken Arm --------------------

            uConsole.RegisterCommand("brokenleg", new Action(() =>
            {
                new BrokenLegAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight, Settings.options.BrokenLimbDurationMode == 1 ? 201.6f : 2016f).Start();
            }));

            uConsole.RegisterCommand("brokenarm", new Action(() =>
            {
                new BrokenArmAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight, Settings.options.BrokenLimbDurationMode == 1 ? 134.4f : 1344f).Start();
            }));


            // -------------------- Black Lung --------------------

            uConsole.RegisterCommand("blacklungrisk", new Action(() =>
            {
                new BlackLungRiskAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("blacklung", new Action(() =>
            {
                new BlackLungAffliction(AfflictionBodyArea.Chest, Settings.options.BlackLungDurationMode == 1 ? 360f : 3600f).Start();
            }));


            // -------------------- Carbon Monoxide ---------------------
            uConsole.RegisterCommand("coexposure", new Action(() =>
            {
                new COExposureAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("copoisoning", new Action(() =>
            {
                new COPoisoningAffliction(AfflictionBodyArea.Chest, Random.Range(6f, 24f)).Start();
            }));


            // -------------------- Corpse Sickness --------------------

            uConsole.RegisterCommand("corpsesicknessrisk", new Action(() =>
            {
                new CorpseSicknessRiskAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("corpsesickness", new Action(() =>
            {
                new CorpseSicknessAffliction(AfflictionBodyArea.Head, Random.Range(48f, 96f)).Start();
            }));


            // -------------------- Severe Sprain Risks --------------------

            uConsole.RegisterCommand("mm_sprain_risk_wrist_left", new Action(() =>
            {
                SevereSprainLogic.DevApplyRisk(SevereSprainKind.Wrist, AfflictionBodyArea.HandLeft);
            }));

            uConsole.RegisterCommand("mm_sprain_risk_wrist_right", new Action(() =>
            {
                SevereSprainLogic.DevApplyRisk(SevereSprainKind.Wrist, AfflictionBodyArea.HandRight);
            }));

            uConsole.RegisterCommand("mm_sprain_risk_ankle_left", new Action(() =>
            {
                SevereSprainLogic.DevApplyRisk(SevereSprainKind.Ankle, AfflictionBodyArea.FootLeft);
            }));

            uConsole.RegisterCommand("mm_sprain_risk_ankle_right", new Action(() =>
            {
                SevereSprainLogic.DevApplyRisk(SevereSprainKind.Ankle, AfflictionBodyArea.FootRight);
            }));


            // -------------------- Severe Sprains --------------------

            uConsole.RegisterCommand("mm_severe_sprain_wrist_left", new Action(() =>
            {
                SevereSprainLogic.DevApplySevereSprain(SevereSprainKind.Wrist, AfflictionBodyArea.HandLeft);
            }));

            uConsole.RegisterCommand("mm_severe_sprain_wrist_right", new Action(() =>
            {
                SevereSprainLogic.DevApplySevereSprain(SevereSprainKind.Wrist, AfflictionBodyArea.HandRight);
            }));

            uConsole.RegisterCommand("mm_severe_sprain_ankle_left", new Action(() =>
            {
                SevereSprainLogic.DevApplySevereSprain(SevereSprainKind.Ankle, AfflictionBodyArea.FootLeft);
            }));

            uConsole.RegisterCommand("mm_severe_sprain_ankle_right", new Action(() =>
            {
                SevereSprainLogic.DevApplySevereSprain(SevereSprainKind.Ankle, AfflictionBodyArea.FootRight);
            }));


            // -------------------- Regional Afflictions & Home Comfort --------------------

            uConsole.RegisterCommand("homesickness", new Action(() =>
            {
                new HomeSicknessAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("regionaldistress", new Action(() =>
            {
                new RegionalDistressAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("homecomfort", new Action(() =>
            {
                new HomeComfortBuff(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("homecomfort_cure", new Action(() =>
            {
                var mgr = AfflictionManager.GetAfflictionManagerInstance();
                if (mgr?.m_Afflictions == null) return;

                for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
                {
                    var a = mgr.m_Afflictions[i];
                    if (a == null) continue;

                    if (a is HomeComfortBuff)
                    {
                        a.Cure();
                    }
                }
            }));

            uConsole.RegisterCommand("reset_HR_timer", new Action(() =>
            {
                RegionalAfflictionLogic.DevResetHomeRegionRelocationCooldown();
            }));


            // -------------------- Batch Risk Afflictions --------------------

            uConsole.RegisterCommand("maj_afflictionsrisk", new Action(() =>
            {
                new SepsisRiskAffliction(AfflictionBodyArea.Chest)
                {
                    DebugForced = true,
                    DebugRiskValue = 50f
                }.Start();

                new BlackLungRiskAffliction(AfflictionBodyArea.Chest)
                {
                    DebugForced = true,
                    DebugRiskValue = 50f
                }.Start();

                new COExposureAffliction(AfflictionBodyArea.Chest)
                {
                    DebugForced = true,
                    DebugRiskValue = 50f
                }.Start();

                new CorpseSicknessRiskAffliction(AfflictionBodyArea.Head)
                {
                    DebugForced = true,
                    DebugRiskValue = 50f
                }.Start();

                new SevereWristSprainRiskAffliction(AfflictionBodyArea.HandLeft)
                {
                    DebugForced = true,
                    DebugRiskValue = 99f
                }.Start();

                new SevereAnkleSprainRiskAffliction(AfflictionBodyArea.FootLeft)
                {
                    DebugForced = true,
                    DebugRiskValue = 99f
                }.Start();

                uConsole.Log("MajorMiseries risk afflictions applied in debug mode.");
            }));


            // -------------------- Batch Afflictions --------------------

            uConsole.RegisterCommand("maj_afflictions", new Action(() =>
            {
                AfflictionLogic.AddScarredFleshStack("Debug command maj_afflictions");

                new BrokenLegAffliction(
                    AfflictionBodyArea.LegLeft,
                    Settings.options.BrokenLimbDurationMode == 1 ? 201.6f : 2016f
                ).Start();

                new BrokenArmAffliction(
                    AfflictionBodyArea.ArmLeft,
                    Settings.options.BrokenLimbDurationMode == 1 ? 134.4f : 1344f
                ).Start();

                new SepsisAffliction(AfflictionBodyArea.Chest).Start();

                new BlackLungAffliction(
                    AfflictionBodyArea.Chest,
                    Settings.options.BlackLungDurationMode == 1 ? 360f : 3600f
                ).Start();

                new COPoisoningAffliction(AfflictionBodyArea.Chest, 12f).Start();

                new CorpseSicknessAffliction(AfflictionBodyArea.Head, 72f).Start();

                new SevereWristSprainAffliction(
                    AfflictionBodyArea.HandLeft,
                    SevereSprainLogic.SevereSprainDurationHours
                ).Start();

                new SevereAnkleSprainAffliction(
                    AfflictionBodyArea.FootLeft,
                    SevereSprainLogic.SevereSprainDurationHours
                ).Start();

                new HomeSicknessAffliction(AfflictionBodyArea.Head)
                {
                    DebugForced = true
                }.Start();

                new RegionalDistressAffliction(AfflictionBodyArea.Head)
                {
                    DebugForced = true
                }.Start();

                uConsole.Log("MajorMiseries afflictions applied in debug mode.");
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
                        || a is CorpseSicknessAffliction
                        || a is SevereWristSprainRiskAffliction
                        || a is SevereAnkleSprainRiskAffliction
                        || a is SevereWristSprainAffliction
                        || a is SevereAnkleSprainAffliction
                        || a is HomeSicknessAffliction
                        || a is RegionalDistressAffliction
                        || a is HomeComfortBuff)
                    {
                        a.Cure();
                    }
                }

                AfflictionLogic.SetScarredFleshStack(0);
            }));


            // -------------------- Vanilla debug Affliction --------------------

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


            // -------------------- Misery stage popup test --------------------

            uConsole.RegisterCommand("mm_testpopup", new Action(() =>
            {
                Patches.DisplayStagePopup.ShowStagePopup(1, "GAMEPLAY_OmenName");
            }));


            // -------------------- Scarred Flesh State --------------------

            uConsole.RegisterCommand("reset_SFhistory", new Action(() =>
            {
                AfflictionLogic.SetScarredFleshStack(0);

                uConsole.Log("ScarredFleshHistoryCount reset to 0");

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

                Core.State ??= new Persistence.MMState();

                AfflictionLogic.SetScarredFleshStack(v);

                uConsole.Log($"ScarredFleshHistoryCount set to {Core.State.ScarredFleshHistoryCount}");

                Core.Log($"ScarredFleshHistoryCount set to {Core.State.ScarredFleshHistoryCount}", false);
            }));


            // -------------------- Predator Hostility State --------------------

            uConsole.RegisterCommand("reset_PH", new Action(() =>
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                Core.Instance?.MarkDirty();

                uConsole.Log("PredatorHostility reset to 0");

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

                Core.Log($"PredatorHostility set to {Core.State.PredatorHostility:0}", false);
            }));


            // -------------------- Corpse Exposure State --------------------

            uConsole.RegisterCommand("reset_CE", new Action(() =>
            {
                Core.State.CorpseExposure = 0f;
                Core.Instance?.MarkDirty();

                uConsole.Log("CorpseExposure reset to 0");

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

                Core.Log($"CorpseExposure set to {Core.State.CorpseExposure:0.##}", false);
            }));


            // -------------------- Immunity Shield & Body Temp State --------------------

            uConsole.RegisterCommand("set_IS", new Action(() =>
            {
                var @params = uConsole.GetAllParameters();
                if (@params == null || @params.Count < 1)
                {
                    uConsole.Log("[value 0-100]");
                    return;
                }

                if (!float.TryParse(@params[0], out float v))
                {
                    uConsole.Log("value must be a number");
                    return;
                }

                Core.State ??= new Persistence.MMState();

                Core.State.ImmunityShield = Mathf.Clamp(v, 0f, 100f);
                Core.Instance?.MarkDirty();

                uConsole.Log($"ImmunityShield set to {Core.State.ImmunityShield:0.#}%");

                Core.Log($"ImmunityShield set to {Core.State.ImmunityShield:0.#}%", false);
            }));

            uConsole.RegisterCommand("set_BT", new Action(() =>
            {
                var @params = uConsole.GetAllParameters();
                if (@params == null || @params.Count < 1)
                {
                    uConsole.Log("[value 34-43]");
                    return;
                }

                if (!float.TryParse(@params[0], out float v))
                {
                    uConsole.Log("value must be a number");
                    return;
                }

                Core.State ??= new Persistence.MMState();

                Core.State.InternalBodyTemp = Mathf.Clamp(v, 34f, 43f);
                Core.Instance?.MarkDirty();

                uConsole.Log($"InternalBodyTemp set to {Core.State.InternalBodyTemp:0.0}C");

                Core.Log($"InternalBodyTemp set to {Core.State.InternalBodyTemp:0.0}C", false);
            }));
        }
    }
}