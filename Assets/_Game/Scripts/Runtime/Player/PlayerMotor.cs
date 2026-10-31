using SpiritMovers.Core.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Player
{
    /// <summary>
    /// First-person movement (D-014, D-015 M1): walk / run with stamina / crouch / low jump, slowed by heavy
    /// items. Owner-authoritative: the owner moves its CharacterController and NetworkTransform syncs it.
    /// The pivot is at the feet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : NetworkBehaviour
    {
        private const float StandHeight = 1.8f;
        private const float CrouchHeight = 1.2f;
        private const float StandEyes = 1.6f;
        private const float CrouchEyes = 1.0f;
        private const float LookSensitivity = 0.12f;

        [SerializeField] private InputActionAsset m_Actions;

        private readonly MovementConfig m_Config = MovementConfig.Default;
        // Written by the owner so other machines (and the ghost on the host) see crouching and where the player looks.
        private readonly NetworkVariable<bool> m_NetCrouch = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> m_NetPitch = new(writePerm: NetworkVariableWritePermission.Owner);
        private float m_Pitch;
        private CharacterController m_Controller;
        private Transform m_Head;
        private float m_VerticalVelocity;
        private bool m_WasRunning;

        public static PlayerMotor Local { get; private set; }

        /// <summary>Owner only. Defaults to the keyboard; tests and bots swap in a <see cref="ScriptedInput"/>.</summary>
        public IPlayerInputSource Input { get; set; }
        /// <summary>False while a UI field (chat, menu) owns the keyboard: the player stands still.</summary>
        public bool InputEnabled { get; set; } = true;
        /// <summary>Set by the carrying code; slows the player for heavy items.</summary>
        public CarryLoad Load { get; set; }
        public Stamina Stamina { get; } = new();
        public MoveResult LastMove { get; private set; }
        /// <summary>The input read this frame (owner); other player components read it in LateUpdate.</summary>
        public PlayerInputState CurrentInput { get; private set; }
        /// <summary>Eyes: the camera on the owner, the interaction ray origin everywhere.</summary>
        public Transform Head => m_Head;
        public Vector3 Feet => transform.position;
        /// <summary>Heading (yaw only).</summary>
        public Vector3 Forward => transform.forward;
        /// <summary>Head pitch in degrees (down is positive); the synced value on other machines.</summary>
        public float Pitch => IsSpawned && !IsOwner ? m_NetPitch.Value : m_Pitch;
        public bool IsGrounded => m_Controller.isGrounded;
        /// <summary>Crouching, as every machine sees it.</summary>
        public bool Crouching => m_NetCrouch.Value;
        /// <summary>Where the eyes look (heading + pitch) — the ghost's "is anyone watching me" input.</summary>
        public Vector3 Look => Quaternion.Euler(Pitch, transform.eulerAngles.y, 0f) * Vector3.forward;

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            // At high frame rates a frame's step is under the 1 mm default and would be dropped (and isGrounded lost).
            m_Controller.minMoveDistance = 0f;
            m_Head = new GameObject("Head").transform;
            m_Head.SetParent(transform, false);
            m_Head.localPosition = Vector3.up * StandEyes;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                m_NetCrouch.OnValueChanged += (_, crouching) => ApplyCrouchShape(crouching);
                m_NetPitch.OnValueChanged += (_, pitch) => m_Head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                ApplyCrouchShape(m_NetCrouch.Value);
                m_Head.localRotation = Quaternion.Euler(m_NetPitch.Value, 0f, 0f);
                return;
            }

            Local = this;
            Input ??= Application.isBatchMode || m_Actions == null ? new ScriptedInput() : new KeyboardMouseInput(m_Actions);

            var spawn = GameObject.Find($"PlayerSpawn{OwnerClientId % 4}") ?? GameObject.Find("PlayerSpawn0");
            if (spawn != null)
                Teleport(spawn.transform.position, spawn.transform.eulerAngles.y);

            if (!Application.isBatchMode)
            {
                m_Head.gameObject.AddComponent<Camera>().nearClipPlane = 0.05f;
                m_Head.gameObject.AddComponent<AudioListener>();
                foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (other.transform != m_Head)
                        other.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this)
                Local = null;
        }

        /// <summary>Owner only: place the feet somewhere and face a heading (spawning, tests).</summary>
        public void Teleport(Vector3 feet, float yawDegrees)
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDegrees, 0f));
            m_Controller.enabled = true;
            m_VerticalVelocity = 0f;
        }

        /// <summary>Owner only: turn the body and head toward a point (tests, bots).</summary>
        public void LookAt(Vector3 point)
        {
            var toPoint = point - m_Head.position;
            var flat = new Vector3(toPoint.x, 0f, toPoint.z);
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flat);
            SetPitch(Mathf.Atan2(-toPoint.y, flat.magnitude) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            var input = InputEnabled && Input != null ? Input.Read() : default;
            CurrentInput = input;
            ApplyLook(input.Look);
            Move(input, Time.deltaTime);
        }

        private void ApplyLook(Vector2 delta)
        {
            if (delta == Vector2.zero)
                return;
            transform.Rotate(0f, delta.x * LookSensitivity, 0f);
            SetPitch(m_Pitch - delta.y * LookSensitivity);
        }

        private void SetPitch(float pitch)
        {
            m_Pitch = Mathf.Clamp(pitch, -85f, 85f);
            m_Head.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
            if (IsSpawned && IsOwner && Mathf.Abs(m_NetPitch.Value - m_Pitch) > 0.5f)
                m_NetPitch.Value = m_Pitch;
        }

        private void Move(PlayerInputState input, float dt)
        {
            var intent = new MoveIntent(input.Move.y, input.Move.x, input.Sprint, input.Crouch, input.JumpPressed);
            LastMove = MovementRules.Evaluate(intent, Stamina.AllowsRun(m_WasRunning), Load, m_Config);
            Stamina.Tick(dt, LastMove.Running);
            m_WasRunning = LastMove.Running;
            SetCrouch(LastMove.Crouching);

            var direction = transform.right * input.Move.x + transform.forward * input.Move.y;
            var planar = direction.sqrMagnitude > 0.0001f ? direction.normalized * LastMove.Speed : Vector3.zero;

            if (m_Controller.isGrounded)
            {
                if (m_VerticalVelocity < 0f)
                    m_VerticalVelocity = -2f; // keeps the controller pressed onto slopes and stairs
                if (input.JumpPressed && LastMove.CanJump)
                    m_VerticalVelocity = Mathf.Sqrt(2f * m_Config.JumpHeight * -m_Config.Gravity);
            }
            m_VerticalVelocity += m_Config.Gravity * dt;
            m_Controller.Move((planar + Vector3.up * m_VerticalVelocity) * dt);
        }

        private void SetCrouch(bool crouching)
        {
            if (m_NetCrouch.Value != crouching)
                m_NetCrouch.Value = crouching;
            ApplyCrouchShape(crouching);
        }

        private void ApplyCrouchShape(bool crouching)
        {
            var height = crouching ? CrouchHeight : StandHeight;
            if (Mathf.Approximately(m_Controller.height, height))
                return;
            m_Controller.height = height;
            m_Controller.center = Vector3.up * (height / 2f);
            m_Head.localPosition = Vector3.up * (crouching ? CrouchEyes : StandEyes);
        }
    }
}
