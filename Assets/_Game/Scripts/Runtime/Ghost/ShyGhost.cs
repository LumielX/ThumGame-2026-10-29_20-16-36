using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Job;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// The shy ghost (GDD 6.4 คุณยาย, placeholder body): thinks on the host only and moves only while no player
    /// looks at it (TECHNICAL_DESIGN 5). Its anger stage picks what it does — drift and watch, prank (swap two items),
    /// disturb (lights out, push an item), hunt (chase and catch); stage 4 ends the job in <see cref="JobDirector"/>.
    /// Every machine sees its position (NetworkTransform) and colours it amber → red by the synced anger stage.
    /// </summary>
    public sealed class ShyGhost : NetworkBehaviour
    {
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color s_Calm = new(1f, 0.70f, 0.28f); // memory amber #ffb347
        private static readonly Color s_Angry = new(0.88f, 0.19f, 0.19f);
        private const float RepathSeconds = 0.5f;

        [SerializeField] private Renderer m_Body;

        private readonly NetworkVariable<GhostMode> m_Mode = new();
        private readonly NetworkVariable<bool> m_Watched = new();
        private readonly GhostConfig m_Config = new();
        private readonly List<Vector3> m_Corners = new();
        private readonly System.Random m_Random = new();
        private NavMeshPath m_Path;
        private MaterialPropertyBlock m_Block;
        private JobDirector m_Director;
        private bool m_NavMeshHeld;
        private Vector3 m_Ground;
        private Vector3 m_Goal;
        private float m_Speed;
        private float m_KeepAway;
        private float m_NextThinkAt;
        private float m_NextRepathAt;

        public GhostMode Mode => m_Mode.Value;
        public bool IsWatched => m_Watched.Value;
        public GhostConfig Config => m_Config;
        /// <summary>The floor point under the floating body.</summary>
        public Vector3 Ground => transform.position - Vector3.up * m_Config.FloatHeight;
        public Color CurrentColour { get; private set; } = Color.clear;
        /// <summary>Host: the room the ghost is in ("" between rooms).</summary>
        public string CurrentRoom => m_Director != null ? m_Director.House.RoomAt(Ground + Vector3.up * 0.3f) : "";

        /// <summary>Host: the ghost caught this player (Task 5 drags them to a memory cabinet).</summary>
        public event Action<ulong> Caught;

        internal float NextPrankAt { get; set; }
        internal float NextDisturbAt { get; set; }

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Path = new NavMeshPath();
            m_Director = FindAnyObjectByType<JobDirector>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            GhostNavMesh.Acquire();
            m_NavMeshHeld = true;
            PlaceAt(transform.position);
            NextPrankAt = Time.time + m_Config.PrankCooldown;
            NextDisturbAt = Time.time + m_Config.DisturbCooldown;
        }

        public override void OnNetworkDespawn()
        {
            if (m_NavMeshHeld)
                GhostNavMesh.Release();
            m_NavMeshHeld = false;
        }

        private void Update()
        {
            UpdateColour();
            if (!IsSpawned || !IsServer)
                return;
            if (Time.time >= m_NextThinkAt)
                ThinkNow();
            Move(Time.deltaTime);
        }

        /// <summary>Host: look around and decide (normally every <see cref="GhostConfig.ThinkInterval"/>).</summary>
        internal void ThinkNow()
        {
            m_NextThinkAt = Time.time + m_Config.ThinkInterval;
            var exploring = m_Director != null && m_Director.Phase == JobPhase.Exploring;
            var mode = exploring ? GhostRules.ModeFor(m_Director.AngerStage) : GhostMode.Idle;
            if (m_Mode.Value != mode)
                m_Mode.Value = mode;
            m_Speed = 0f;
            if (!exploring || mode == GhostMode.Evict)
                return;

            var motors = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
            var watched = GhostRules.IsWatched(transform.position.ToNumerics(),
                motors.Select(m => new Viewer(m.OwnerClientId, m.Head.position.ToNumerics(), m.Look.ToNumerics())), m_Config, ClearLine);
            if (m_Watched.Value != watched)
                m_Watched.Value = watched;
            if (watched)
                return; // shy: frozen while anyone looks

            var targets = motors.Select(m => new Target(m.OwnerClientId, m.Feet.ToNumerics(), false, 0f)).ToList();
            var target = GhostRules.PickTarget(Ground.ToNumerics(), targets, Time.time);
            if (target is { } t)
            {
                m_Goal = t.Feet.ToUnity();
                m_Speed = mode == GhostMode.Hunt ? m_Config.HuntSpeed : m_Config.IdleSpeed;
                m_KeepAway = mode == GhostMode.Hunt ? 0f : m_Config.IdleKeepAway;
                if (mode == GhostMode.Hunt && Vector3.Distance(Ground, m_Goal) <= m_Config.CatchDistance)
                    Caught?.Invoke(t.ClientId);
            }

            if (mode == GhostMode.Prank && Time.time >= NextPrankAt)
                Prank();
            if (mode == GhostMode.Disturb && Time.time >= NextDisturbAt)
                Disturb();
        }

        /// <summary>Test hook: put the ghost on the floor at a point.</summary>
        internal void SetPositionForTest(Vector3 ground)
        {
            PlaceAt(ground);
            m_NextRepathAt = 0f;
        }

        private static bool ClearLine(System.Numerics.Vector3 eye, System.Numerics.Vector3 ghost) =>
            !Physics.Linecast(eye.ToUnity(), ghost.ToUnity(), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private void PlaceAt(Vector3 point)
        {
            m_Ground = NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas) ? hit.position : point;
            m_Corners.Clear();
            transform.position = m_Ground + Vector3.up * m_Config.FloatHeight;
        }

        /// <summary>Host: drift along the walkable map toward the goal while unwatched.</summary>
        private void Move(float dt)
        {
            if (m_Speed <= 0f || m_Watched.Value)
                return;
            if (Time.time >= m_NextRepathAt)
            {
                m_NextRepathAt = Time.time + RepathSeconds;
                m_Corners.Clear();
                var goal = NavMesh.SamplePosition(m_Goal, out var hit, 2f, NavMesh.AllAreas) ? hit.position : m_Goal;
                if (NavMesh.CalculatePath(m_Ground, goal, NavMesh.AllAreas, m_Path) && m_Path.status != NavMeshPathStatus.PathInvalid)
                    m_Corners.AddRange(m_Path.corners.Skip(1));
            }
            if (m_KeepAway > 0f && Vector3.Distance(m_Ground, m_Goal) <= m_KeepAway)
                return;

            var step = m_Speed * dt;
            while (step > 0f && m_Corners.Count > 0)
            {
                var toCorner = m_Corners[0] - m_Ground;
                var distance = toCorner.magnitude;
                if (distance <= step)
                {
                    m_Ground = m_Corners[0];
                    m_Corners.RemoveAt(0);
                    step -= distance;
                    continue;
                }
                m_Ground += toCorner / distance * step;
                var flat = new Vector3(toCorner.x, 0f, toCorner.z);
                if (flat.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.LookRotation(flat);
                step = 0f;
            }
            transform.position = m_Ground + Vector3.up * m_Config.FloatHeight;
        }

        /// <summary>Stage 1: swap two items in this room while nobody looks (GDD 6.4).</summary>
        private void Prank()
        {
            NextPrankAt = Time.time + m_Config.PrankCooldown;
            if (GhostRules.PickPrank(m_Director.PrankCandidates(), CurrentRoom, m_Random) is { } pair &&
                m_Director.SwapItems(pair.A, pair.B))
                Debug.Log($"[Ghost] prank: swapped {pair.A} and {pair.B} in {CurrentRoom}");
        }

        /// <summary>Stage 2: lights out for a moment and an item flies lightly (GDD 6.2).</summary>
        private void Disturb()
        {
            NextDisturbAt = Time.time + m_Config.DisturbCooldown;
            LightsOutRpc(m_Config.LightsOutSeconds, m_Config.LightsOutFactor);
            var slot = GhostRules.PickPush(m_Director.PrankCandidates(), CurrentRoom, m_Random);
            if (slot == null)
                return;
            var angle = (float)m_Random.NextDouble() * Mathf.PI * 2f;
            var velocity = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)) * m_Config.PushSpeed;
            if (m_Director.PushItem(slot, velocity))
                Debug.Log($"[Ghost] disturb: lights out, pushed {slot}");
        }

        [Rpc(SendTo.Everyone)]
        private void LightsOutRpc(float seconds, float factor)
        {
            var lights = FindAnyObjectByType<GhostLights>();
            if (lights != null)
                lights.Dim(seconds, factor);
        }

        /// <summary>Every machine: amber when calm, red when hunting (the anger signal, D-023).</summary>
        private void UpdateColour()
        {
            var stage = m_Director != null ? m_Director.AngerStage : 0;
            var colour = Color.Lerp(s_Calm, s_Angry, Mathf.Clamp01(stage / 3f));
            if (colour == CurrentColour || m_Body == null)
                return;
            CurrentColour = colour;
            m_Body.GetPropertyBlock(m_Block);
            m_Block.SetColor(s_BaseColor, colour);
            m_Body.SetPropertyBlock(m_Block);
        }
    }
}
