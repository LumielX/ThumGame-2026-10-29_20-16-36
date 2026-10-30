using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Prototypes.NetSync
{
    /// <summary>
    /// Throwaway M2 prototype player: owner-authoritative movement (WASD + mouse), E to carry/drop.
    /// In bot mode (headless tests) it walks a circle in sync with server time and carries the nearest prop.
    /// </summary>
    public sealed class ProtoPlayer : NetworkBehaviour
    {
        public const float BotRadius = 4f;
        public const float BotAngularSpeed = 0.6f; // rad/s → ~2.4 m/s along the circle

        private static readonly Dictionary<ulong, ProtoPlayer> s_ByClient = new();

        private const float MoveSpeed = 4f;
        private const float LookSensitivity = 0.12f;
        private const float GrabRange = 3.5f;

        private Camera m_Camera;
        private float m_Pitch;
        private ProtoProp m_Carried;

        public static bool BotMode { get; set; }

        /// <summary>Shifts this instance's bot circle along X (voice distance tests).</summary>
        public static float BotOffsetX { get; set; }
        public static CarryMode PreferredCarryMode { get; set; } = CarryMode.OwnerTransfer;
        public static ProtoPlayer Local { get; private set; }

        /// <summary>True while a UI field (chat) owns the keyboard.</summary>
        public static bool InputBlocked { get; set; }

        /// <summary>Ears/mouth for positional voice: the camera when there is one.</summary>
        public Transform Head => m_Camera != null ? m_Camera.transform : transform;

        public Vector3 HoldPoint => HoldPointFor(transform.position, transform.forward);
        public ProtoProp CarriedProp => m_Carried;

        public static Vector3 HoldPointFor(Vector3 position, Vector3 forward) =>
            position + Vector3.up * 1.2f + forward * 1.6f;

        /// <summary>Where a bot stands at a given server time; every peer can compute it.</summary>
        public static void BotPose(double serverTime, out Vector3 position, out Vector3 forward)
        {
            var angle = (float)(serverTime * BotAngularSpeed);
            position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * BotRadius;
            forward = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        public static bool TryGet(ulong clientId, out ProtoPlayer player) => s_ByClient.TryGetValue(clientId, out player);

        public override void OnNetworkSpawn()
        {
            s_ByClient[OwnerClientId] = this;
            if (!IsOwner)
                return;

            Local = this;
            if (!Application.isBatchMode)
            {
                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.6f;
                m_Camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (other != m_Camera)
                        other.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (s_ByClient.TryGetValue(OwnerClientId, out var registered) && registered == this)
                s_ByClient.Remove(OwnerClientId);
            if (Local == this)
                Local = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            if (BotMode)
                UpdateBot();
            else
                UpdateHuman();

            if (m_Carried != null && (!m_Carried.IsSpawned || (m_Carried.IsCarried && m_Carried.Carrier != OwnerClientId)))
                m_Carried = null;
        }

        private void UpdateBot()
        {
            BotPose(NetworkManager.ServerTime.Time, out var position, out var forward);
            transform.SetPositionAndRotation(position + Vector3.right * BotOffsetX, Quaternion.LookRotation(forward));
        }

        private void UpdateHuman()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || InputBlocked)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = mouse.delta.ReadValue() * LookSensitivity;
                transform.Rotate(0f, look.x, 0f);
                m_Pitch = Mathf.Clamp(m_Pitch - look.y, -80f, 80f);
                if (m_Camera != null)
                    m_Camera.transform.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
            }

            var input = new Vector3(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0f,
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            transform.position += transform.TransformDirection(Vector3.ClampMagnitude(input, 1f)) * (MoveSpeed * Time.deltaTime);

            if (keyboard.eKey.wasPressedThisFrame)
                ToggleCarry(m_Camera != null ? m_Camera.transform : transform);
        }

        public void ToggleCarry(Transform aim)
        {
            if (m_Carried != null)
            {
                m_Carried.RequestDropRpc();
                m_Carried = null;
                return;
            }

            if (Physics.Raycast(aim.position, aim.forward, out var hit, GrabRange) &&
                hit.rigidbody != null && hit.rigidbody.TryGetComponent<ProtoProp>(out var prop) && !prop.IsCarried)
            {
                prop.RequestCarryRpc(PreferredCarryMode);
                m_Carried = prop;
            }
        }

        /// <summary>Bot helper: grab the closest free prop regardless of aim.</summary>
        public void CarryNearest()
        {
            ProtoProp best = null;
            var bestDistance = float.MaxValue;
            foreach (var prop in FindObjectsByType<ProtoProp>(FindObjectsSortMode.None))
            {
                if (prop.IsCarried)
                    continue;
                var distance = (prop.transform.position - HoldPoint).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = prop;
                    bestDistance = distance;
                }
            }

            if (best == null)
                return;
            best.RequestCarryRpc(PreferredCarryMode);
            m_Carried = best;
        }
    }
}
