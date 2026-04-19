namespace MajorMiseries.Persistence
{
    internal class MMState
    {
        public float PredatorHostility = 0f;
        public float HoursSinceLastPredatorKill = 0f;
        public float BlackLungExposure = 0f;
        public int ScarredFleshHistoryCount = 0;
        public float CorpseExposure = 0f;
    }

    internal enum RequiemStage
    {
        None = 0,
        Omen = 1,
        Dirge = 2,
        Knell = 3,
        Requiem = 4
    }
}