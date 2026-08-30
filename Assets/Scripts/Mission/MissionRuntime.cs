using UnityEngine;

namespace ParcelSort
{
    /// <summary>The settled result of one round.</summary>
    public struct MissionOutcome
    {
        public bool won;
        public MissionFailReason reason;

        /// <summary>Coins this settlement hands out. Zero on repeat settlements of the same round.</summary>
        public int coinsAwarded;

        /// <summary>True only for the very first clear of this mission on this save.</summary>
        public bool firstClear;

        public int correct;
        public int wrong;
        public int jams;
        public float seconds;
    }

    /// <summary>
    /// Live scoring for one round: the clock, the tallies, and the verdict. A plain class so a
    /// whole round can be replayed at a fixed timestep without an engine.
    ///
    /// Invariant: <c>Delivered == Correct + Wrong</c> at all times.
    /// </summary>
    public class MissionRuntime
    {
        readonly MissionDef def;
        readonly int planned;

        bool resolved;
        MissionOutcome settled;

        public MissionRuntime(MissionDef def, int planned)
        {
            this.def = def ?? new MissionDef();
            this.planned = Mathf.Max(0, planned);
        }

        public MissionDef Def => def;

        public MissionState State { get; private set; } = MissionState.Idle;

        public MissionFailReason FailReason { get; private set; } = MissionFailReason.None;

        public float Elapsed { get; private set; }

        public float Remaining => Mathf.Max(0f, def.objective.timeLimitSeconds - Elapsed);

        /// <summary>Parcels that reached a bay, right or wrong.</summary>
        public int Delivered => Correct + Wrong;

        public int Correct { get; private set; }

        public int Wrong { get; private set; }

        public int Jams { get; private set; }

        /// <summary>Total the schedule will release. Known during prep, before the round starts.</summary>
        public int Planned => planned;

        public int LeftToDeliver => Mathf.Max(0, planned - Delivered);

        /// <summary>True once <see cref="Resolve"/> has paid out; further calls award nothing.</summary>
        public bool IsResolved => resolved;

        /// <summary>The outcome as first settled, for the result panel to keep displaying.</summary>
        public MissionOutcome SettledOutcome => settled;

        public bool IsOver => State == MissionState.Won || State == MissionState.Lost;

        public void Begin()
        {
            State = MissionState.Running;
            FailReason = MissionFailReason.None;
            Elapsed = 0f;
            Correct = 0;
            Wrong = 0;
            Jams = 0;
            resolved = false;
            settled = default;
        }

        /// <summary>Advances the clock and re-checks the verdict. No-op once the round is over.</summary>
        public void Tick(float dt)
        {
            if (State != MissionState.Running || dt <= 0f)
            {
                return;
            }

            Elapsed += dt;
            Evaluate();
        }

        public void ReportCorrect()
        {
            if (State != MissionState.Running)
            {
                return;
            }

            Correct++;
            Evaluate();
        }

        public void ReportWrong()
        {
            if (State != MissionState.Running)
            {
                return;
            }

            Wrong++;
            Evaluate();
        }

        public void ReportJam()
        {
            if (State != MissionState.Running)
            {
                return;
            }

            Jams++;
            Evaluate();
        }

        /// <summary>
        /// Re-judges the round. Losses are checked before the win so a run that blows the error
        /// budget on the same parcel that would have hit the target still counts as a loss; this
        /// is what keeps "won implies within every limit" true.
        /// </summary>
        public MissionState Evaluate()
        {
            if (State != MissionState.Running)
            {
                return State;
            }

            MissionObjective objective = def.objective;

            if (Wrong > objective.maxWrong)
            {
                State = MissionState.Lost;
                FailReason = MissionFailReason.TooManyWrong;
                return State;
            }

            if (objective.maxJams >= 0 && Jams > objective.maxJams)
            {
                State = MissionState.Lost;
                FailReason = MissionFailReason.TooManyJams;
                return State;
            }

            if (Correct >= objective.targetDelivered && objective.targetDelivered > 0)
            {
                State = MissionState.Won;
                FailReason = MissionFailReason.None;
                return State;
            }

            if (objective.timeLimitSeconds > 0f && Remaining <= 0f)
            {
                State = MissionState.Lost;
                FailReason = MissionFailReason.Timeout;
                return State;
            }

            return State;
        }

        /// <summary>Forces the round to end as a timeout, used when the player abandons it.</summary>
        public void ForceTimeout()
        {
            if (State != MissionState.Running)
            {
                return;
            }

            State = MissionState.Lost;
            FailReason = MissionFailReason.Timeout;
        }

        /// <summary>
        /// Settles the round against the persistent record.
        ///
        /// Idempotent by design: the first call computes the payout and latches the first-clear
        /// flag, and every later call returns the same verdict with <c>coinsAwarded == 0</c>. So
        /// a caller that adds up <c>coinsAwarded</c> over repeated calls still pays exactly the
        /// first call's total, and the first-clear bonus can never be collected twice.
        /// </summary>
        public MissionOutcome Resolve(MissionRecord record)
        {
            if (resolved)
            {
                MissionOutcome repeat = settled;
                repeat.coinsAwarded = 0;
                repeat.firstClear = false;
                return repeat;
            }

            Evaluate();

            bool won = State == MissionState.Won;
            var outcome = new MissionOutcome
            {
                won = won,
                reason = won ? MissionFailReason.None : FailReason,
                correct = Correct,
                wrong = Wrong,
                jams = Jams,
                seconds = Elapsed
            };

            if (won)
            {
                outcome.coinsAwarded = def.rewards.baseCoins;
                if (record != null && !record.FirstClearPaid)
                {
                    outcome.coinsAwarded += def.rewards.firstClearCoins;
                    outcome.firstClear = true;
                    record.MarkFirstClearPaid();
                }

                record?.MarkCleared();
                record?.RecordWinSeconds(Elapsed);
            }

            if (record != null)
            {
                record.RecordAttempt();
                record.RecordDelivered(Delivered);
            }

            resolved = true;
            settled = outcome;
            return outcome;
        }
    }
}
