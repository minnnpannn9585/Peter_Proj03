using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Central parcel stepper. Owning movement in one place is what makes queueing,
    /// gate back pressure and merge arbitration behave consistently.
    /// </summary>
    public class TrafficSystem : MonoBehaviour
    {
        YardGraph graph;
        RuleSettings rules;
        YardDirector director;
        Transform parcelRoot;
        readonly List<ParcelRuntime> live = new List<ParcelRuntime>();
        bool running;

        public int LiveParcelCount => live.Count;

        public Transform ParcelRoot
        {
            get
            {
                if (parcelRoot == null)
                {
                    GameObject go = new GameObject("ParcelRoot");
                    parcelRoot = go.transform;
                }

                return parcelRoot;
            }
        }

        public void Bind(YardGraph yardGraph, RuleSettings ruleSettings, YardDirector yardDirector)
        {
            graph = yardGraph;
            rules = ruleSettings;
            director = yardDirector;
            running = false;
        }

        public void SetRunning(bool value)
        {
            running = value;
        }

        public void ClearAll()
        {
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] != null)
                {
                    Destroy(live[i].gameObject);
                }
            }

            live.Clear();
            if (graph != null)
            {
                foreach (BeltPath belt in graph.AllBelts)
                {
                    belt.Parcels.Clear();
                }
            }
        }

        float MinGap => rules != null ? rules.minGap : 0.75f;

        float GateGap => MinGap * 0.5f;

        /// <summary>Distance a parcel may enter a belt at, or a negative value when blocked.</summary>
        public float ResolveEntryDistance(BeltPath belt, float desired)
        {
            if (belt == null)
            {
                return -1f;
            }

            float entry = Mathf.Clamp(desired, 0f, belt.Length);
            float rear = belt.RearParcelDistance();
            if (rear >= 0f)
            {
                entry = Mathf.Min(entry, rear - MinGap);
            }

            float gate = belt.NextClosedGateDistance(0f);
            if (gate >= 0f)
            {
                entry = Mathf.Min(entry, gate - GateGap);
            }

            return entry;
        }

        public bool CanEnter(BeltPath belt)
        {
            return ResolveEntryDistance(belt, 0f) >= 0f;
        }

        /// <summary>Puts a freshly spawned parcel on its first belt.</summary>
        public bool Spawn(ParcelRuntime parcel, BeltPath belt)
        {
            if (parcel == null || belt == null)
            {
                return false;
            }

            float entry = ResolveEntryDistance(belt, 0f);
            if (entry < 0f)
            {
                return false;
            }

            parcel.transform.SetParent(ParcelRoot, true);
            belt.AppendParcel(parcel, entry);
            parcel.ResetFlow();
            parcel.RefreshPose();
            live.Add(parcel);
            return true;
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances every parcel by dt. Public so it can be driven deterministically.</summary>
        public void Tick(float dt)
        {
            if (!running || graph == null)
            {
                return;
            }

            if (dt <= 0f)
            {
                return;
            }

            foreach (BeltPath belt in graph.AllBelts)
            {
                StepBelt(belt, dt);

                // Scanners are driven from here rather than from their own Update so a reveal
                // lands on the same tick as the movement that carried the parcel past them,
                // and so a whole round can be replayed headlessly at a fixed timestep.
                List<ScannerDevice> scanners = belt.Scanners;
                for (int s = 0; s < scanners.Count; s++)
                {
                    if (scanners[s] != null)
                    {
                        scanners[s].ScanPass();
                    }
                }
            }

            foreach (BeltPath belt in graph.AllBelts)
            {
                List<ParcelRuntime> parcels = belt.Parcels;
                for (int i = 0; i < parcels.Count; i++)
                {
                    parcels[i].RefreshPose();
                }
            }
        }

        void StepBelt(BeltPath belt, float dt)
        {
            List<ParcelRuntime> parcels = belt.Parcels;
            float speed = belt.EffectiveSpeed;
            float step = speed * dt;

            // Tracks whether the parcel ahead is (directly or transitively) gate-held, so the
            // exemption travels back down the queue instead of stopping at the barrier.
            bool aheadGateHeld = false;

            int i = 0;
            while (i < parcels.Count)
            {
                ParcelRuntime parcel = parcels[i];
                float before = parcel.Distance;
                float target = before + step;

                bool limitedByLeader = false;
                if (i > 0)
                {
                    float leaderLimit = parcels[i - 1].Distance - MinGap;
                    if (leaderLimit < target)
                    {
                        target = leaderLimit;
                        limitedByLeader = true;
                    }
                }

                float gate = belt.NextClosedGateDistance(before);
                bool limitedByGate = false;
                if (gate >= 0f)
                {
                    float gateLimit = gate - GateGap;
                    if (gateLimit < target)
                    {
                        target = gateLimit;
                        limitedByGate = true;
                    }
                }

                if (i == 0 && target > belt.Length)
                {
                    if (TryHandOff(parcel, belt, target - belt.Length))
                    {
                        // parcels[0] was removed, keep the same index.
                        continue;
                    }

                    target = belt.Length;
                }

                target = Mathf.Min(target, belt.Length);
                parcel.Distance = Mathf.Max(before, target);

                // This parcel is gate-held either because a closed gate capped it directly, or
                // because it is queueing behind a parcel that is.
                bool gateHeld = limitedByGate || (limitedByLeader && aheadGateHeld);
                parcel.UpstreamGateHeld = gateHeld && !limitedByGate;

                bool blocked = step <= 0.0001f || (parcel.Distance - before) < step * 0.5f;

                if (gateHeld)
                {
                    // A closed gate is the player choosing to queue parcels. Counting that as
                    // congestion would make the Gate trigger the very failure it exists to avoid,
                    // since a gate holds for 8s and the jam threshold is 3s. The gate always
                    // reopens on its own, so the queue is guaranteed to drain and this exemption
                    // cannot hide a real deadlock.
                    parcel.GateWaitTime += dt;
                    parcel.StallTime = 0f;
                    parcel.JamCounted = false;
                }
                else if (blocked)
                {
                    parcel.StallTime += dt;
                    float limit = rules != null ? rules.jamStallSeconds : 3f;
                    if (!parcel.JamCounted && parcel.StallTime >= limit)
                    {
                        parcel.JamCounted = true;
                        if (director != null)
                        {
                            director.NotifyJam();
                        }
                    }
                }
                else
                {
                    parcel.StallTime = 0f;
                    parcel.JamCounted = false;
                    parcel.GateWaitTime = 0f;
                }

                aheadGateHeld = gateHeld;
                i++;
            }
        }

        /// <summary>Moves the front parcel across a node. Returns false when it must wait.</summary>
        bool TryHandOff(ParcelRuntime parcel, BeltPath belt, float overflow)
        {
            YardNode node = belt.To;
            if (node == null)
            {
                Debug.LogWarning("Belt '" + belt.BeltId + "' has no destination node.");
                Retire(parcel, belt);
                return true;
            }

            if (node.Type == NodeType.Bay)
            {
                var bay = node.GetComponent<TruckBay>();
                belt.RemoveParcel(parcel);
                live.Remove(parcel);
                if (bay != null)
                {
                    bay.Accept(parcel.Parcel);
                }
                else
                {
                    Destroy(parcel.gameObject);
                }

                return true;
            }

            if (node.OutBelts.Count == 0)
            {
                Debug.LogWarning("Node '" + node.NodeId + "' is a dead end; parcel discarded.");
                Retire(parcel, belt);
                return true;
            }

            if (node.InBelts.Count > 1 && !ClaimMerge(node, belt))
            {
                return false;
            }

            BeltPath next = node.SelectedBelt;
            float entry = ResolveEntryDistance(next, overflow);
            if (entry < 0f)
            {
                return false;
            }

            belt.RemoveParcel(parcel);
            next.AppendParcel(parcel, entry);
            parcel.ResetFlow();
            if (node.InBelts.Count > 1)
            {
                node.LastServedInput = node.InBelts.IndexOf(belt);
            }

            return true;
        }

        /// <summary>Round robin so one input branch cannot starve another at a merge.</summary>
        static bool ClaimMerge(YardNode node, BeltPath incoming)
        {
            int count = node.InBelts.Count;
            for (int k = 1; k <= count; k++)
            {
                int index = (node.LastServedInput + k) % count;
                BeltPath candidate = node.InBelts[index];
                if (candidate == incoming)
                {
                    return true;
                }

                if (HasParcelWaiting(candidate))
                {
                    return false;
                }
            }

            return true;
        }

        static bool HasParcelWaiting(BeltPath belt)
        {
            return belt != null && belt.Parcels.Count > 0 &&
                   belt.Parcels[0].Distance >= belt.Length - 0.06f;
        }

        void Retire(ParcelRuntime parcel, BeltPath belt)
        {
            belt.RemoveParcel(parcel);
            live.Remove(parcel);
            Destroy(parcel.gameObject);
        }
    }
}
