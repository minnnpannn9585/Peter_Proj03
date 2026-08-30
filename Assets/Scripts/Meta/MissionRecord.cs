namespace ParcelSort
{
    /// <summary>
    /// Persistent progress for one mission. The fields that must never regress are behind
    /// methods rather than public setters, so "cleared" cannot be un-cleared by a later loss
    /// and a worse run cannot overwrite a better one.
    /// </summary>
    public class MissionRecord
    {
        /// <summary>True once the mission has been won at least once. Never returns to false.</summary>
        public bool Cleared { get; private set; }

        /// <summary>
        /// True once the one-off first-clear bonus has been handed out. This flag alone is what
        /// makes the bonus pay exactly once across the whole lifetime of a save.
        /// </summary>
        public bool FirstClearPaid { get; private set; }

        /// <summary>Highest delivered count ever reached. Monotonically non-decreasing.</summary>
        public int BestDelivered { get; private set; }

        /// <summary>How many rounds of this mission have reached the result screen.</summary>
        public int Attempts { get; private set; }

        /// <summary>Fastest winning time in seconds. 0 means "never won".</summary>
        public float BestSeconds { get; private set; }

        public static MissionRecord Restore(
            bool cleared, bool firstClearPaid, int bestDelivered, int attempts, float bestSeconds)
        {
            return new MissionRecord
            {
                Cleared = cleared,
                FirstClearPaid = firstClearPaid,
                BestDelivered = bestDelivered < 0 ? 0 : bestDelivered,
                Attempts = attempts < 0 ? 0 : attempts,
                BestSeconds = bestSeconds < 0f ? 0f : bestSeconds
            };
        }

        /// <summary>One-way latch: a clear is permanent.</summary>
        public void MarkCleared()
        {
            Cleared = true;
        }

        /// <summary>One-way latch: the first-clear bonus is never owed twice.</summary>
        public void MarkFirstClearPaid()
        {
            FirstClearPaid = true;
        }

        public void RecordAttempt()
        {
            Attempts++;
        }

        /// <summary>Keeps the higher of the two, so this can be called with any run's result.</summary>
        public void RecordDelivered(int delivered)
        {
            if (delivered > BestDelivered)
            {
                BestDelivered = delivered;
            }
        }

        /// <summary>Keeps the fastest win. 0 and negatives are ignored as "no time".</summary>
        public void RecordWinSeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            if (BestSeconds <= 0f || seconds < BestSeconds)
            {
                BestSeconds = seconds;
            }
        }

        public MissionRecord Clone()
        {
            return Restore(Cleared, FirstClearPaid, BestDelivered, Attempts, BestSeconds);
        }
    }
}
