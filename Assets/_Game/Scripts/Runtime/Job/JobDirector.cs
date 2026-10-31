using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SpiritMovers.Job
{
    /// <summary>Everything a peer needs to rebuild the host's job plan; synced as one value so it arrives whole.</summary>
    public struct JobSpec : INetworkSerializable, IEquatable<JobSpec>
    {
        public int Seed;
        public FixedString32Bytes DifficultyId;
        public int PlayerCount;
        public FixedString32Bytes LayoutHash;

        public bool IsSet => DifficultyId.Length > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref DifficultyId);
            serializer.SerializeValue(ref PlayerCount);
            serializer.SerializeValue(ref LayoutHash);
        }

        public bool Equals(JobSpec other) =>
            Seed == other.Seed && DifficultyId.Equals(other.DifficultyId) && PlayerCount == other.PlayerCount && LayoutHash.Equals(other.LayoutHash);
    }

    /// <summary>How a job ended and what it paid; synced to every peer for the results screen.</summary>
    public struct JobResult : INetworkSerializable, IEquatable<JobResult>
    {
        public bool IsSet;
        public JobEnding Ending;
        public int RoomsAccepted;
        public int RoomsRequired;
        public int TeamTotal;
        public int PerPlayer;
        public int Stars;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsSet);
            serializer.SerializeValue(ref Ending);
            serializer.SerializeValue(ref RoomsAccepted);
            serializer.SerializeValue(ref RoomsRequired);
            serializer.SerializeValue(ref TeamTotal);
            serializer.SerializeValue(ref PerPlayer);
            serializer.SerializeValue(ref Stars);
        }

        public bool Equals(JobResult other) =>
            IsSet == other.IsSet && Ending == other.Ending && RoomsAccepted == other.RoomsAccepted &&
            RoomsRequired == other.RoomsRequired && TeamTotal == other.TeamTotal && PerPlayer == other.PerPlayer && Stars == other.Stars;

        public override string ToString() =>
            $"ending={Ending} rooms={RoomsAccepted}/{RoomsRequired} teamTotal={TeamTotal} perPlayer={PerPlayer} stars={Stars}";
    }

    /// <summary>
    /// Runs one job in the house (TECHNICAL_DESIGN 3–4). The host owns the rules (<see cref="JobProgress"/>,
    /// <see cref="AngerModel"/>) and unpacks the house; every peer rebuilds the same <see cref="JobPlan"/>
    /// from the synced <see cref="JobSpec"/> and checks its layout hash against the host's.
    /// </summary>
    public sealed class JobDirector : NetworkBehaviour
    {
        [SerializeField] private HouseAuthoring m_House;

        private readonly NetworkVariable<JobSpec> m_Spec = new();
        private readonly NetworkVariable<JobPhase> m_Phase = new(JobPhase.Briefing);
        private readonly NetworkVariable<int> m_AngerStage = new();
        private readonly NetworkVariable<JobResult> m_Result = new();
        private NetworkList<FixedString32Bytes> m_Accepted;

        private const float EvaluateInterval = 0.5f;
        private const float SettledSpeed = 0.05f;
        private const float FarewellSeconds = 5f;

        private readonly List<string> m_AcceptedCache = new();
        private readonly List<SlotState> m_SlotStates = new();
        private JobProgress m_Progress;
        private AngerModel m_Anger;
        private JobDifficulty m_Difficulty;
        private float m_NextEvaluate;
        private float m_FarewellEndsAt = -1f;

        public HouseAuthoring House => m_House;
        public JobPhase Phase => m_Phase.Value;
        public JobPlan Plan { get; private set; }
        /// <summary>False when this peer's house data gives a different plan than the host's.</summary>
        public bool PlanMatchesHost { get; private set; } = true;
        /// <summary>Rooms the ghost has accepted, in order; every peer.</summary>
        public IReadOnlyList<string> AcceptedRooms => m_AcceptedCache;
        /// <summary>Anger stage 0–4 as the host sees it; every peer.</summary>
        public int AngerStage => m_AngerStage.Value;
        /// <summary>Host only: the anger value (0 elsewhere).</summary>
        public float AngerValue => m_Anger?.Value ?? 0f;
        /// <summary>Set on every peer once the job has ended.</summary>
        public JobResult Result => m_Result.Value;

        /// <summary>Every peer: a room was accepted (its colour starts coming back).</summary>
        public event Action<string> RoomAccepted;

        private void Awake() => m_Accepted = new NetworkList<FixedString32Bytes>();

        public override void OnNetworkSpawn()
        {
            m_Accepted.OnListChanged += OnAcceptedChanged;
            // Late joiners see already-accepted rooms in colour straight away.
            foreach (var room in m_Accepted)
                OnRoomAccepted(room.ToString(), instant: true);

            if (IsServer)
                return;
            m_Spec.OnValueChanged += (_, spec) => ApplyHostPlan(spec);
            if (m_Spec.Value.IsSet)
                ApplyHostPlan(m_Spec.Value);
        }

        private void OnAcceptedChanged(NetworkListEvent<FixedString32Bytes> change)
        {
            if (change.Type == NetworkListEvent<FixedString32Bytes>.EventType.Add)
                OnRoomAccepted(change.Value.ToString(), instant: false);
        }

        private void OnRoomAccepted(string roomId, bool instant)
        {
            if (m_AcceptedCache.Contains(roomId))
                return;
            m_AcceptedCache.Add(roomId);
            var room = m_House.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room != null && room.TryGetComponent<RoomRestoration>(out var restoration))
                restoration.Restore(instant);
            RoomAccepted?.Invoke(roomId);
        }

        private void Update()
        {
            if (!IsServer || m_Progress == null)
                return;

            if (m_Progress.Phase == JobPhase.Farewell)
            {
                // Placeholder for the farewell ceremony (the ghost thanks the team and fades out).
                if (m_FarewellEndsAt < 0f)
                    m_FarewellEndsAt = Time.time + FarewellSeconds;
                else if (Time.time >= m_FarewellEndsAt)
                {
                    m_Progress.CompleteFarewell();
                    Finish();
                }
                return;
            }
            if (m_Progress.Phase != JobPhase.Exploring)
                return;

            m_Anger.Tick(Time.deltaTime, everyoneSilent: false);
            m_AngerStage.Value = m_Anger.Stage;
            if (m_Anger.IsEvicted)
            {
                m_Progress.Evict();
                Finish();
                return;
            }

            if (Time.time < m_NextEvaluate)
                return;
            m_NextEvaluate = Time.time + EvaluateInterval;
            foreach (var room in Plan.Rooms)
                if (!m_Progress.AcceptedRooms.Contains(room.RoomId))
                    EvaluateNow(room.RoomId);
        }

        /// <summary>
        /// Server: score one room of the plan now and accept it if the ghost would (TECHNICAL_DESIGN 4.3) —
        /// only once its items are put down and still. Returns how many items are in place (the ghost's nod),
        /// or −1 for a room that is not part of this job.
        /// </summary>
        public int EvaluateNow(string roomId)
        {
            var room = Plan?.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (!IsServer || room == null)
                return -1;

            m_SlotStates.Clear();
            var settled = true;
            foreach (var task in room.Slots)
            {
                var item = m_House.Slot(task.Slot.Id).Item;
                var body = item.GetComponent<Rigidbody>();
                settled &= !item.IsCarried && body.linearVelocity.magnitude < SettledSpeed;
                var actual = new Placement(m_House.RoomAt(body.position), body.position.ToNumerics(), body.rotation.ToNumerics());
                m_SlotStates.Add(new SlotState(task.Slot.Id, task.Slot.Target, task.Slot.Tolerance, actual, ItemCondition.Intact));
            }

            var result = RoomEvaluator.Evaluate(m_SlotStates, RoomRules.Default);
            if (result.Accepted && settled && m_Progress.AcceptRoom(roomId))
            {
                m_Anger.OnRoomAccepted();
                m_AngerStage.Value = m_Anger.Stage;
                m_Accepted.Add(roomId);
                m_Phase.Value = m_Progress.Phase;
                Debug.Log($"[Job] room accepted {roomId} ({m_Progress.AcceptedRooms.Count}/{m_Progress.RoomsRequired})");
            }
            return result.CorrectCount;
        }

        /// <summary>
        /// Server: the team closes the job at the van (GDD 4.3) — from the briefing too, which pays nothing.
        /// Does nothing once the job has ended.
        /// </summary>
        public void LeaveEarly()
        {
            if (!IsServer || m_Result.Value.IsSet)
                return;
            if (m_Progress == null)
            {
                m_Result.Value = new JobResult { IsSet = true, Ending = JobEnding.LeftEarly };
                m_Phase.Value = JobPhase.Ended;
                Debug.Log($"[Job] end before start {m_Result.Value}");
                return;
            }
            m_Progress.LeaveEarly();
            Finish();
        }

        /// <summary>Server: pay out and publish the result (GDD 4.3, 12).</summary>
        private void Finish()
        {
            if (m_Result.Value.IsSet || m_Progress.Ending is not { } ending)
                return;
            var stars = StarRating.Calculate(m_Anger.HighestStage, itemsBroken: 0);
            var payout = PayoutCalculator.Calculate(ending, m_Difficulty.BasePay, m_Progress.AcceptedRooms.Count,
                m_Progress.RoomsRequired, stars, hintsUsed: 0, NetworkManager.ConnectedClientsIds.Count, PayoutRules.Default);
            m_Result.Value = new JobResult
            {
                IsSet = true,
                Ending = ending,
                RoomsAccepted = m_Progress.AcceptedRooms.Count,
                RoomsRequired = m_Progress.RoomsRequired,
                TeamTotal = payout.TeamTotal,
                PerPlayer = payout.PerPlayer,
                Stars = payout.Stars,
            };
            m_Phase.Value = JobPhase.Ended;
            Debug.Log($"[Job] end {m_Result.Value}");
        }

        /// <summary>Server: the ghost answers a bell — score the room, count the ring against its patience.</summary>
        internal int RingBell(string roomId)
        {
            var count = EvaluateNow(roomId);
            if (count < 0)
                return count;
            m_Anger.Apply(AngerEvent.BellRung);
            m_AngerStage.Value = m_Anger.Stage;
            return count;
        }

        /// <summary>Test hook: raise anger (by breaking-item steps) until it reaches at least <paramref name="value"/>.</summary>
        internal void ApplyAngerForTest(float value)
        {
            while (m_Anger.Value < value && !m_Anger.IsEvicted)
                m_Anger.Apply(AngerEvent.ItemBroken);
            m_AngerStage.Value = m_Anger.Stage;
        }

        /// <summary>Server: make the plan for this seed, unpack the house and start exploring.</summary>
        public void BeginJob(int seed, JobDifficulty difficulty)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host starts a job.");
            var problems = difficulty.Validate();
            if (problems.Count > 0)
                throw new ArgumentException($"Difficulty '{difficulty.Id}': {string.Join("; ", problems)}");

            var players = NetworkManager.ConnectedClientsIds.Count;
            Plan = JobGenerator.Generate(seed, m_House.BuildLayout(), difficulty, players);
            m_Difficulty = difficulty;
            m_Progress = new JobProgress(Plan);
            m_Anger = JobSetup.CreateAnger(AngerConfig.Default, difficulty, players);

            foreach (var task in Plan.Rooms.SelectMany(r => r.Slots))
                Teleport(m_House.Slot(task.Slot.Id).Item, task.StartPlacement.Position.ToUnity(), task.StartPlacement.Rotation.ToUnity());

            m_Progress.Begin();
            m_Spec.Value = new JobSpec
            {
                Seed = seed,
                DifficultyId = difficulty.Id,
                PlayerCount = players,
                LayoutHash = Plan.LayoutHash,
            };
            m_Phase.Value = m_Progress.Phase;
            Debug.Log($"[Job] begin seed={seed} difficulty={difficulty.Id} players={players} rooms={string.Join(",", Plan.Rooms.Select(r => r.RoomId))} hash={Plan.LayoutHash}");
        }

        /// <summary>Test hook: rebuild the plan as a client would for a host seed and layout hash.</summary>
        internal void ApplyHostPlan(int seed, string hostLayoutHash) =>
            ApplyHostPlan(new JobSpec { Seed = seed, DifficultyId = "normal", PlayerCount = 1, LayoutHash = hostLayoutHash });

        private void ApplyHostPlan(JobSpec spec)
        {
            if (!spec.IsSet)
                return;
            Plan = JobGenerator.Generate(spec.Seed, m_House.BuildLayout(), DifficultyById(spec.DifficultyId.ToString()), spec.PlayerCount);
            PlanMatchesHost = Plan.LayoutHash == spec.LayoutHash.ToString();
            if (!PlanMatchesHost)
                Debug.LogError($"[Job] plan mismatch: host layout {spec.LayoutHash} but this build has {Plan.LayoutHash} — the house data differs");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>Dev builds only: a bot asks the host to put a slot's item back (Tools/m3c-job-test.ps1).</summary>
        [Rpc(SendTo.Server)]
        public void DevPutBackRpc(FixedString32Bytes slotId) => PlaceAtTarget(slotId.ToString());
#endif

        /// <summary>Server (tests, dev bots): put a slot's item exactly where the ghost remembers it.</summary>
        internal void PlaceAtTarget(string slotId)
        {
            var marker = m_House.Slot(slotId);
            Teleport(marker.Item, marker.transform.position, marker.transform.rotation);
        }

        private static JobDifficulty DifficultyById(string id) => id == JobDifficulty.Hard.Id ? JobDifficulty.Hard : JobDifficulty.Normal;

        private static void Teleport(Carryable item, Vector3 position, Quaternion rotation)
        {
            var body = item.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            // Teleport (not a normal update) so other peers do not see the item slide across the house.
            item.GetComponent<NetworkTransform>().Teleport(position, rotation, item.transform.localScale);
        }
    }
}
