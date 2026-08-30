namespace ParcelSort
{
    public static class UiStrings
    {
        public const string PackagesLeft = "Left";
        public const string Spawned = "Spawned";
        public const string Correct = "Correct";
        public const string Wrong = "Wrong";
        public const string Jams = "Jams";
        public const string Gate = "Gates";
        public const string Devices = "Devices";
        public const string Coins = "Coins";
        public const string PhasePrep = "PREP";
        public const string PhaseRunning = "RUNNING";
        public const string PhaseResult = "RESULT";
        public const string Start = "START";

        // Bottom left run controls. English per the project's language split (README P1).
        public const string Pause = "PAUSE";
        public const string Resume = "RESUME";
        public const string Paused = "PAUSED";
        public const string ExitLevel = "EXIT LEVEL";

        /// <summary>Debug label text for a phase. Every phase is listed explicitly.</summary>
        public static string PhaseName(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Prep:
                    return PhasePrep;
                case GamePhase.Running:
                    return PhaseRunning;
                case GamePhase.Result:
                    return PhaseResult;
            }

            return phase.ToString();
        }
    }
}
