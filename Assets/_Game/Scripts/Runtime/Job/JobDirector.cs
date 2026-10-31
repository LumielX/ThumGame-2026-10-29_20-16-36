using System;
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

        private JobProgress m_Progress;
        private AngerModel m_Anger;
        private JobDifficulty m_Difficulty;

        public HouseAuthoring House => m_House;
        public JobPhase Phase => m_Phase.Value;
        public JobPlan Plan { get; private set; }
        /// <summary>False when this peer's house data gives a different plan than the host's.</summary>
        public bool PlanMatchesHost { get; private set; } = true;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                return;
            m_Spec.OnValueChanged += (_, spec) => ApplyHostPlan(spec);
            if (m_Spec.Value.IsSet)
                ApplyHostPlan(m_Spec.Value);
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
