namespace MajorMiseries.Persistence
{
    internal class MMState
    {
        public float PredatorHostility = 0f;
        public float HoursSinceLastPredatorKill = 0f;
        public float BlackLungExposure = 0f;
        public int ScarredFleshHistoryCount = 0;
        public float CorpseExposure = 0f;

        public string LastKnownLogicalRegion = "";
        public string CurrentLogicalRegion = "";
        public string ConfiguredHomeRegion = "";
        public string ConfiguredRegionalDistressRegion = "";
        public float HomeSicknessHoursAway = 0f;
        public float RegionalDistressHoursInRegion = 0f;

        public int LeftWristSprainCount = 0;
        public float LeftWristSprainWindowHours = 0f;
        public float LeftWristSevereSprainRisk = 0f;

        public int RightWristSprainCount = 0;
        public float RightWristSprainWindowHours = 0f;
        public float RightWristSevereSprainRisk = 0f;

        public int LeftAnkleSprainCount = 0;
        public float LeftAnkleSprainWindowHours = 0f;
        public float LeftAnkleSevereSprainRisk = 0f;

        public int RightAnkleSprainCount = 0;
        public float RightAnkleSprainWindowHours = 0f;
        public float RightAnkleSevereSprainRisk = 0f;
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