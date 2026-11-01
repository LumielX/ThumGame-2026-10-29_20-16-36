using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
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
        /// <summary><see cref="JobPlan.ContentHash"/> of the host's plan.</summary>
        public FixedString32Bytes PlanHash;

        public bool IsSet => DifficultyId.Length > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref DifficultyId);
            serializer.SerializeValue(ref PlayerCount);
            serializer.SerializeValue(ref LayoutHash);
            serializer.SerializeValue(ref PlanHash);
        }

        public bool Equals(JobSpec other) =>
            Seed == other.Seed && DifficultyId.Equals(other.DifficultyId) && PlayerCount == other.PlayerCount &&
            LayoutHash.Equals(other.LayoutHash) && PlanHash.Equals(other.PlanHash);
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
    /// from the synced <see cref="JobSpec"/> and checks its plan against the host's; a client whose plan differs is
    /// disconnected rather than left playing a different job.
    /// </summary>
    public sealed class JobDirector : NetworkBehaviour
    {
        [SerializeField] private HouseAuthoring m_House;
        [Tooltip("Spirit camera prefab; BeginJob puts the team's cameras at the tool spot (GDD 8.3).")]
        [SerializeField] private GameObject m_CameraPrefab;
        [SerializeField] private Transform m_ToolSpot;

        private readonly NetworkVariable<JobSpec> m_Spec = new();
        private readonly NetworkVariable<JobPhase> m_Phase = new(JobPhase.Briefing);
        private readonly NetworkVariable<int> m_AngerStage = new();
        private readonly NetworkVariable<JobResult> m_Result = new();
        private NetworkList<FixedString32Bytes> m_Accepted;

        private const float EvaluateInterval = 0.5f;
        private const float SettledSpeed = 0.05f;
        /// <summary>A room's items must stay still this long before the ghost accepts it (one still snapshot is not enough).</summary>
        private const float SettledSeconds = 0.4f;
        private const float FarewellSeconds = 5f;

        private readonly List<string> m_AcceptedCache = new();
        private readonly List<SlotState> m_SlotStates = new();
        private readonly Dictionary<string, float> m_SettledSince = new();
        private JobProgress m_Progress;
        private AngerModel m_Anger;
        private JobDifficulty m_Difficulty;
        private float m_NextEvaluate;
        private float m_FarewellEndsAt = -1f;
        private NoiseHub m_Noise;
        private LoudNoiseGate m_LoudGate;

        public HouseAuthoring House => m_House;
        public JobPhase Phase => m_Phase.Value;
        public JobPlan Plan { get; private set; }
        /// <summary>False when this peer cannot build the host's plan (different house data or rules); Plan is then null.</summary>
        public bool PlanMatchesHost { get; private set; } = true;
        /// <summary>Rooms the ghost has accepted, in order; every peer.</summary>
        public IReadOnlyList<string> AcceptedRooms => m_AcceptedCache;
        /// <summary>Anger stage 0–4 as the host sees it; every peer.</summary>
        public int AngerStage => m_AngerStage.Value;
        /// <summary>Host only: the anger value (0 elsewhere).</summary>
        public float AngerValue => m_Anger?.Value ?? 0f;
        /// <summary>Host: how many times a player got out of a memory cabinet alone (costs pay, GDD 6.3).</summary>
        public int SelfReleases { get; private set; }
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
            {
                m_Noise = GetComponent<NoiseHub>();
                if (m_Noise != null)
                {
                    m_LoudGate = new LoudNoiseGate(m_Noise.Config);
                    m_Noise.Heard += OnNoiseHeard;
                }
                return;
            }
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

            m_Anger.Tick(Time.deltaTime, EveryoneSilent());
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
        /// Silent = no noise above the silence level since just over one footstep interval (steps come in every
        /// interval while someone walks, so a walking player keeps the house "not silent").
        /// </summary>
        private bool EveryoneSilent() =>
            m_Noise == null || Time.time - m_Noise.LastNoiseTime > m_Noise.Config.FootstepInterval * 1.5f;

        /// <summary>Server: loud noises annoy the ghost, at most once per cooldown (GDD 6.2).</summary>
        private void OnNoiseHeard(NoiseEvent e)
        {
            if (m_Progress?.Phase != JobPhase.Exploring || !m_LoudGate.TryAccept(e, Time.time))
                return;
            m_Anger.Apply(AngerEvent.LoudNoise);
            m_AngerStage.Value = m_Anger.Stage;
            Debug.Log($"[Job] loud {e.Source} {e.Loudness:F2} anger={m_Anger.Value:F1}");
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

            var result = Score(room, out var settled);
            if (!settled)
                m_SettledSince.Remove(roomId);
            else if (!m_SettledSince.ContainsKey(roomId))
                m_SettledSince[roomId] = Time.time;
            var stayedStill = settled && Time.time - m_SettledSince[roomId] >= SettledSeconds;
            if (result.Accepted && stayedStill && m_Progress.AcceptRoom(roomId))
            {
                m_Anger.OnRoomAccepted();
                m_AngerStage.Value = m_Anger.Stage;
                m_Accepted.Add(roomId);
                m_Phase.Value = m_Progress.Phase;
                Debug.Log($"[Job] room accepted {roomId} ({m_Progress.AcceptedRooms.Count}/{m_Progress.RoomsRequired})");
            }
            return result.CorrectCount;
        }

        /// <summary>Host: how many of a planned room's items are in place right now (0 for a room not in the job).</summary>
        internal int CorrectCount(string roomId)
        {
            var room = Plan?.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            return IsServer && room != null ? Score(room, out _).CorrectCount : 0;
        }

        private RoomResult Score(RoomTask room, out bool settled)
        {
            m_SlotStates.Clear();
            settled = true;
            foreach (var task in room.Slots)
            {
                var item = m_House.Slot(task.Slot.Id).Item;
                var body = item.GetComponent<Rigidbody>();
                settled &= !item.IsCarried && body.linearVelocity.magnitude < SettledSpeed;
                var actual = new Placement(m_House.RoomAt(body.position), body.position.ToNumerics(), body.rotation.ToNumerics());
                m_SlotStates.Add(new SlotState(task.Slot.Id, task.Slot.Target, task.Slot.Tolerance, actual, ItemCondition.Intact));
            }
            return RoomEvaluator.Evaluate(m_SlotStates, RoomRules.Default);
        }

        /// <summary>Host: the team's spirit cameras go to the tool spot by the van (1 for 1–3 players, 2 for 4–6).</summary>
        private void SpawnTools(int players)
        {
            if (m_CameraPrefab == null || m_ToolSpot == null)
                return;
            for (var i = 0; i < ToolConfig.SpiritCameras(players); i++)
            {
                var camera = Instantiate(m_CameraPrefab, m_ToolSpot.position + m_ToolSpot.right * (0.4f * i), m_ToolSpot.rotation);
                // Tools belong to this house: they go when the job scene unloads (M4A, back to the van).
                camera.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            }
        }

        /// <summary>Host: someone looked through the spirit camera — it annoys the ghost (GDD 5.2).</summary>
        internal void CameraUsed()
        {
            if (m_Anger == null)
                return;
            m_Anger.Apply(AngerEvent.CameraUsed);
            m_AngerStage.Value = m_Anger.Stage;
        }

        /// <summary>Host: a player got out of a memory cabinet alone; it costs pay at the end (GDD 6.3).</summary>
        internal void CountSelfRelease() => SelfReleases++;

        /// <summary>Host: a solo player was caught — the ghost calms down one stage (D-011).</summary>
        internal void CalmOneStage()
        {
            if (m_Anger == null)
                return;
            m_Anger.CalmOneStage();
            m_AngerStage.Value = m_Anger.Stage;
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

        /// <summary>Server: a player bows to the ghost in a room — calms it once per room, only while exploring (GDD 6.2, 8.1).</summary>
        public bool Wai(string roomId)
        {
            if (!IsServer || m_Progress?.Phase != JobPhase.Exploring || string.IsNullOrEmpty(roomId) || !m_Anger.Wai(roomId))
                return false;
            m_AngerStage.Value = m_Anger.Stage;
            Debug.Log($"[Job] wai in {roomId} anger={m_Anger.Value:F1}");
            return true;
        }

        /// <summary>Server: pay out and publish the result (GDD 4.3, 12).</summary>
        private void Finish()
        {
            if (m_Result.Value.IsSet || m_Progress.Ending is not { } ending)
                return;
            var stars = StarRating.Calculate(m_Anger.HighestStage, itemsBroken: 0);
            var payout = PayoutCalculator.Calculate(ending, m_Difficulty.BasePay, m_Progress.AcceptedRooms.Count,
                m_Progress.RoomsRequired, stars, hintsUsed: 0, SelfReleases, NetworkManager.ConnectedClientsIds.Count, PayoutRules.Default);
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

        /// <summary>
        /// Host: the planned items as the ghost sees them — the room each is in now, whether someone carries it, and
        /// whether its own or its current room is already accepted (those are off limits to pranks, GDD 7).
        /// </summary>
        internal IReadOnlyList<PrankCandidate> PrankCandidates()
        {
            if (Plan == null)
                return Array.Empty<PrankCandidate>();
            return Plan.Rooms.SelectMany(room => room.Slots.Select(task =>
            {
                var item = m_House.Slot(task.Slot.Id).Item;
                var here = m_House.RoomAt(item.transform.position);
                var accepted = m_AcceptedCache.Contains(room.RoomId) || m_AcceptedCache.Contains(here);
                return new PrankCandidate(task.Slot.Id, here, item.IsCarried, accepted);
            })).ToList();
        }

        /// <summary>Host: the ghost swaps two planned items' places (refused for carried items or accepted rooms).</summary>
        internal bool SwapItems(string slotA, string slotB)
        {
            if (!IsServer || slotA == slotB || !Prankable(slotA) || !Prankable(slotB))
                return false;
            var a = m_House.Slot(slotA).Item;
            var b = m_House.Slot(slotB).Item;
            var (posA, rotA) = (a.transform.position, a.transform.rotation);
            Teleport(a, b.transform.position, b.transform.rotation);
            Teleport(b, posA, rotA);
            return true;
        }

        /// <summary>Host: the ghost flicks a planned item (same limits as <see cref="SwapItems"/>).</summary>
        internal bool PushItem(string slotId, Vector3 velocity)
        {
            if (!IsServer || !Prankable(slotId))
                return false;
            var body = m_House.Slot(slotId).Item.GetComponent<Rigidbody>();
            body.WakeUp();
            body.linearVelocity = velocity;
            return true;
        }

        private bool Prankable(string slotId) =>
            PrankCandidates().Any(c => c.SlotId == slotId && !c.Carried && !c.RoomAccepted);

        /// <summary>Test hook: raise anger (by breaking-item steps) until it reaches at least <paramref name="value"/>.</summary>
        internal void ApplyAngerForTest(float value)
        {
            while (m_Anger.Value < value && !m_Anger.IsEvicted)
                m_Anger.Apply(AngerEvent.ItemBroken);
            m_AngerStage.Value = m_Anger.Stage;
        }

        /// <summary>
        /// Server: make the plan for this seed, unpack the house and start exploring. One job per session: ignored
        /// once a job has started or ended (a new job needs a fresh house).
        /// </summary>
        public void BeginJob(int seed, JobDifficulty difficulty)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host starts a job.");
            if (m_Progress != null || m_Result.Value.IsSet)
            {
                Debug.LogWarning($"[Job] begin seed={seed} ignored: a job already ran in this session");
                return;
            }
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

            SpawnTools(players);
            m_Progress.Begin();
            m_Spec.Value = new JobSpec
            {
                Seed = seed,
                DifficultyId = difficulty.Id,
                PlayerCount = players,
                LayoutHash = Plan.LayoutHash,
                PlanHash = Plan.ContentHash(),
            };
            m_Phase.Value = m_Progress.Phase;
            Debug.Log($"[Job] begin seed={seed} difficulty={difficulty.Id} players={players} rooms={string.Join(",", Plan.Rooms.Select(r => r.RoomId))} hash={Plan.LayoutHash}");
        }

        /// <summary>Test hook: rebuild the plan as a client would for a host spec (one player).</summary>
        internal void ApplyHostPlan(int seed, string difficultyId, string layoutHash, string planHash) =>
            ApplyHostPlan(new JobSpec { Seed = seed, DifficultyId = difficultyId, PlayerCount = 1, LayoutHash = layoutHash, PlanHash = planHash });

        private void ApplyHostPlan(JobSpec spec)
        {
            if (!spec.IsSet)
                return;
            var problem = BuildHostPlan(spec, out var plan);
            PlanMatchesHost = problem == null;
            Plan = PlanMatchesHost ? plan : null;
            if (PlanMatchesHost)
                return;
            Debug.LogError($"[Job] plan mismatch: {problem}");
            if (IsSpawned && !IsServer)
                ReportPlanMismatchRpc(problem);
        }

        /// <summary>Rebuilds the host's plan; returns why it differs, or null when it matches.</summary>
        private string BuildHostPlan(JobSpec spec, out JobPlan plan)
        {
            plan = null;
            var difficulty = DifficultyById(spec.DifficultyId.ToString());
            if (difficulty == null)
                return $"host difficulty '{spec.DifficultyId}' is unknown to this build";
            try
            {
                plan = JobGenerator.Generate(spec.Seed, m_House.BuildLayout(), difficulty, spec.PlayerCount);
            }
            catch (Exception e)
            {
                return $"this build cannot make the host's job ({e.Message})";
            }
            if (plan.LayoutHash != spec.LayoutHash.ToString())
                return $"host layout {spec.LayoutHash} but this build has {plan.LayoutHash} — the house data differs";
            if (plan.ContentHash() != spec.PlanHash.ToString())
                return $"host plan {spec.PlanHash} but this build made {plan.ContentHash()} — the job rules differ";
            return null;
        }

        /// <summary>Server: a client could not rebuild the job; it would play a different house, so it is sent away.</summary>
        [Rpc(SendTo.Server)]
        private void ReportPlanMismatchRpc(FixedString512Bytes problem, RpcParams rpcParams = default)
        {
            var client = rpcParams.Receive.SenderClientId;
            Debug.LogError($"[Job] client {client} has a different plan ({problem}); disconnecting it");
            NetworkManager.DisconnectClient(client, "Job data differs from the host's - update the game.");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>Dev builds only: a bot asks the host to put a slot's item back (Tools/m3c-job-test.ps1).</summary>
        [Rpc(SendTo.Server)]
        public void DevPutBackRpc(FixedString32Bytes slotId) => PlaceAtTarget(slotId.ToString());
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>Dev builds only, host: move the ghost's anger to a stage (Tools/m3c-job-test.ps1 -Scenario ghost).</summary>
        public void DevSetAngerStage(int stage)
        {
            if (!IsServer || m_Anger == null)
                return;
            while (m_Anger.Stage > stage)
                m_Anger.CalmOneStage();
            while (m_Anger.Stage < stage && !m_Anger.IsEvicted)
                m_Anger.Apply(AngerEvent.ItemBroken);
            m_AngerStage.Value = m_Anger.Stage;
        }
#endif

        /// <summary>Server (tests, dev bots): put a slot's item exactly where the ghost remembers it.</summary>
        internal void PlaceAtTarget(string slotId)
        {
            var marker = m_House.Slot(slotId);
            Teleport(marker.Item, marker.transform.position, marker.transform.rotation);
        }

        private static JobDifficulty DifficultyById(string id) =>
            id == JobDifficulty.Normal.Id ? JobDifficulty.Normal : id == JobDifficulty.Hard.Id ? JobDifficulty.Hard : null;

        /// <summary>Server: move an item without it sliding across the house; anyone carrying it lets go first.</summary>
        private static void Teleport(Carryable item, Vector3 position, Quaternion rotation)
        {
            // A carried item belongs to its carrier, and only the owner may teleport it.
            item.ReleaseAll();
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
