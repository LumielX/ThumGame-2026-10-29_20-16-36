using System;
using System.Collections.Generic;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
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
        /// <summary>Height above the feet the ghost needs a clear line to before it can grab a player.</summary>
        private const float ChestHeight = 1.0f;
        /// <summary>Besides its centre, the ghost counts as seen if this point above it is visible.</summary>
        private const float TopOffset = 0.5f;

        private static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        [SerializeField] private Renderer m_Body;

        private readonly NetworkVariable<GhostMode> m_Mode = new();
        private readonly NetworkVariable<bool> m_Watched = new();
        private readonly GhostConfig m_Config = new();
        private readonly List<Vector3> m_Corners = new();
        private readonly Dictionary<ulong, float> m_LeaveAlone = new();
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

        /// <summary>Host: until when the ghost ignores this player (it could not drag them anywhere).</summary>
        internal float LeaveAloneUntil(ulong clientId) => m_LeaveAlone.TryGetValue(clientId, out var until) ? until : 0f;
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
            // Players shut in a cabinet cannot hold the ghost still by staring at it.
            var watched = GhostRules.IsWatched(transform.position.ToNumerics(),
                motors.Where(m => !IsCaptured(m)).Select(m => new Viewer(m.OwnerClientId, m.Head.position.ToNumerics(), m.Look.ToNumerics())),
                m_Config, ClearLine);
            if (m_Watched.Value != watched)
                m_Watched.Value = watched;
            if (watched)
                return; // shy: frozen while anyone looks

            var targets = motors.Select(m => m.TryGetComponent<PlayerCapture>(out var capture)
                ? new Target(m.OwnerClientId, m.Feet.ToNumerics(), capture.IsCaptured,
                    Mathf.Max(capture.ImmuneUntil, LeaveAloneUntil(m.OwnerClientId)))
                : new Target(m.OwnerClientId, m.Feet.ToNumerics(), false, LeaveAloneUntil(m.OwnerClientId))).ToList();
            var target = GhostRules.PickTarget(Ground.ToNumerics(), targets, Time.time);
            if (target is { } t)
            {
                m_Goal = t.Feet.ToUnity();
                m_Speed = mode == GhostMode.Hunt ? m_Config.HuntSpeed : m_Config.IdleSpeed;
                m_KeepAway = mode == GhostMode.Hunt ? 0f : m_Config.IdleKeepAway;
                // Close is not enough: no grabbing through a wall or the loft floor.
                if (mode == GhostMode.Hunt && Vector3.Distance(Ground, m_Goal) <= m_Config.CatchDistance &&
                    Clear(transform.position, m_Goal + Vector3.up * ChestHeight))
                    Catch(t.ClientId);
            }

            if (mode == GhostMode.Prank && Time.time >= NextPrankAt)
                Prank();
            if (mode == GhostMode.Disturb && Time.time >= NextDisturbAt)
                Disturb();
        }

        private static bool IsCaptured(PlayerMotor motor) =>
            motor.TryGetComponent<PlayerCapture>(out var capture) && capture.IsCaptured;

        /// <summary>
        /// Host: drag the caught player into a free cabinet — this room's first, else the nearest. With every cabinet
        /// taken the ghost leaves that player alone for a while instead of hovering over them (final review).
        /// </summary>
        private void Catch(ulong clientId)
        {
            Caught?.Invoke(clientId);
            var room = CurrentRoom;
            var cabinet = FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None)
                .Where(c => !c.IsOccupied)
                .OrderBy(c => c.RoomId == room ? 0 : 1)
                .ThenBy(c => Vector3.Distance(c.transform.position, Ground))
                .FirstOrDefault();
            if (PlayerCarrier.TryGet(clientId, out var player) && player.TryGetComponent<PlayerCapture>(out var capture) &&
                cabinet != null && cabinet.Capture(capture))
                return;
            m_LeaveAlone[clientId] = Time.time + m_Config.ImmuneSeconds;
            Debug.Log($"[Ghost] caught client {clientId} but no cabinet is free; leaving them alone for {m_Config.ImmuneSeconds} s");
        }

        /// <summary>Test hook: put the ghost on the floor at a point.</summary>
        internal void SetPositionForTest(Vector3 ground)
        {
            PlaceAt(ground);
            m_NextRepathAt = 0f;
        }

        /// <summary>The ghost is seen if its centre or its top is in clear view.</summary>
        private static bool ClearLine(System.Numerics.Vector3 eye, System.Numerics.Vector3 ghost)
        {
            var from = eye.ToUnity();
            var centre = ghost.ToUnity();
            return Clear(from, centre) || Clear(from, centre + Vector3.up * TopOffset);
        }

        /// <summary>
        /// Nothing fixed in between. Items and players never hide anything: the box in your own hands must not hide the
        /// ghost you stare at (final review), and the ghost drifts through them anyway.
        /// </summary>
        private static bool Clear(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            var distance = delta.magnitude;
            if (distance < 1e-4f)
                return true;
            var count = Physics.RaycastNonAlloc(from, delta / distance, s_Hits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = s_Hits[i].collider;
                if (hit is CharacterController || hit.GetComponentInParent<Carryable>() != null)
                    continue;
                return false;
            }
            return true;
        }

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
