using AfflictionComponent.Components;
using static MajorMiseries.Afflictions.Dirge;
using static MajorMiseries.Afflictions.Knell;
using static MajorMiseries.Afflictions.Omen;
using static MajorMiseries.Afflictions.Requiem;
using static MajorMiseries.Afflictions.ScarredFlesh;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.BrokenArm;
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
            }));

            uConsole.RegisterCommand("dirge", new Action(() =>
            {
                new DirgeAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("knell", new Action(() =>
            {
                new KnellAffliction(AfflictionBodyArea.Head).Start();
            }));

            uConsole.RegisterCommand("requiem", new Action(() =>
            {
                new RequiemAffliction(AfflictionBodyArea.Head).Start();
            }));
            // -----------------------------------------------------------------

            uConsole.RegisterCommand("scarredflesh", new Action(() =>
            {
                new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
            }));

            uConsole.RegisterCommand("brokenleg", new Action(() =>
            {
                new BrokenLegAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight, Settings.options.BrokenLimbDurationMode == 1 ? 201.6f : 2016f).Start();
            }));

            uConsole.RegisterCommand("brokenarm", new Action(() =>
            {
                new BrokenArmAffliction(Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight, Settings.options.BrokenLimbDurationMode == 1 ? 134.4f : 1344f).Start();
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
                        || a is BrokenArmAffliction)
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
        }
    }
}