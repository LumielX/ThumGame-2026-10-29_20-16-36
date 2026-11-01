using System.Collections;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// A memory cabinet (GDD 6.3): the ghost shuts a caught player in. A friend can open it once one more item in this
    /// room is right since the capture, or after the spirit camera was aimed at it; otherwise the player gets out alone
    /// after 45 s (20 s solo, and the ghost calms a stage) and that costs pay. The host decides everything.
    /// </summary>
    public sealed class MemoryCabinet : NetworkBehaviour, IInteractable
    {
        private const float CheckInterval = 0.25f;
        private const float KnockFlashSeconds = 0.25f;
        private const float FrontDistance = 1.3f;

        [SerializeField] private string m_RoomId;
        [SerializeField] private Light m_Light;

        private readonly NetworkVariable<bool> m_Occupied = new();
        private readonly NetworkVariable<bool> m_Unlocked = new();
        private readonly GhostConfig m_Config = new();
        private JobDirector m_Director;
        private PlayerCapture m_Captive;
        private int m_CorrectAtCapture;
        private float m_NextCheckAt;

        public string RoomId => m_RoomId;
        public bool IsOccupied => m_Occupied.Value;
        public bool IsUnlocked => m_Unlocked.Value;
        /// <summary>Where a released player stands, facing away from the cabinet.</summary>
        public Vector3 FrontPoint => transform.position + transform.forward * FrontDistance;
        public string PromptKey => IsUnlocked ? "cabinet.open" : "cabinet.locked";

        private void Awake()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            if (m_Light != null)
                m_Light.enabled = false;
        }

        public bool CanInteract(PlayerCarrier by) =>
            IsOccupied && by.Held == null && !(by.TryGetComponent<PlayerCapture>(out var capture) && capture.IsCaptured);

        public void Interact(PlayerCarrier by)
        {
            if (by.IsOwner)
                RequestOpenRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestOpenRpc(RpcParams rpcParams = default) => Open(rpcParams.Receive.SenderClientId);

        /// <summary>Host: shut <paramref name="captive"/> in; false if this cabinet is taken or they are already caught.</summary>
        public bool Capture(PlayerCapture captive)
        {
            if (!IsServer || IsOccupied || captive == null || captive.IsCaptured)
                return false;
            if (captive.TryGetComponent<PlayerCarrier>(out var hands) && hands.Held != null)
                hands.Held.ReleaseCarrier(captive.OwnerClientId);

            var solo = NetworkManager.ConnectedClientsIds.Count == 1;
            if (solo && m_Director != null)
                m_Director.CalmOneStage();
            m_Captive = captive;
            m_CorrectAtCapture = m_Director != null ? m_Director.CorrectCount(m_RoomId) : 0;
            m_Unlocked.Value = false;
            m_Occupied.Value = true;
            captive.SetCaptured(this, Time.time + CabinetRules.ReleaseSeconds(solo, m_Config), transform.position, transform.eulerAngles.y);
            Debug.Log($"[Ghost] caught client {captive.OwnerClientId} into cabinet {m_RoomId} (solo={solo})");
            return true;
        }

        /// <summary>Host: the spirit camera was aimed at this cabinet — a friend can open it now.</summary>
        public void UnlockByCamera()
        {
            if (IsServer && IsOccupied)
                m_Unlocked.Value = true;
        }

        /// <summary>Host: <paramref name="opener"/> opens the unlocked cabinet (not the captive, not another captive).</summary>
        public bool Open(ulong opener)
        {
            if (!IsServer || !IsOccupied || !IsUnlocked || m_Captive == null || opener == m_Captive.OwnerClientId)
                return false;
            if (PlayerCarrier.TryGet(opener, out var friend) && friend.TryGetComponent<PlayerCapture>(out var capture) && capture.IsCaptured)
                return false;
            Release(alone: false);
            return true;
        }

        /// <summary>Test hook: the captive's time is up now.</summary>
        internal void ExpireNow()
        {
            if (IsOccupied)
                Release(alone: true);
        }

        /// <summary>Host: the captive's player object went away (left the game).</summary>
        internal void OnCaptiveGone(PlayerCapture captive)
        {
            if (captive == m_Captive)
                Empty();
        }

        /// <summary>Host: the captive knocks; everyone sees the lamp blink.</summary>
        internal void Knock() => KnockRpc();

        [Rpc(SendTo.Everyone)]
        private void KnockRpc()
        {
            Debug.Log($"[Ghost] knock from cabinet {m_RoomId}");
            if (m_Light != null && isActiveAndEnabled)
                StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            m_Light.enabled = true;
            yield return new WaitForSeconds(KnockFlashSeconds);
            m_Light.enabled = false;
        }

        public override void OnNetworkDespawn()
        {
            // The job scene can unload before the next check lets the captive out (M4A final review).
            if (IsServer && m_Captive != null)
                m_Captive.SetFreed();
            m_Captive = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !IsOccupied || Time.time < m_NextCheckAt)
                return;
            m_NextCheckAt = Time.time + CheckInterval;
            if (m_Captive == null)
            {
                Empty();
                return;
            }
            if (m_Director != null && m_Director.Phase == JobPhase.Ended)
            {
                Release(alone: false);
                return;
            }
            if (!IsUnlocked && m_Director != null &&
                CabinetRules.CanFriendOpen(m_CorrectAtCapture, m_Director.CorrectCount(m_RoomId), cameraAimed: false))
                m_Unlocked.Value = true;
            if (Time.time >= m_Captive.ReleaseAt)
                Release(alone: true);
        }

        private void Release(bool alone)
        {
            var captive = m_Captive;
            Empty();
            if (captive == null)
                return;
            if (alone && m_Director != null)
                m_Director.CountSelfRelease();
            captive.SetReleased(FrontPoint, transform.eulerAngles.y, Time.time + m_Config.ImmuneSeconds);
            Debug.Log($"[Ghost] client {captive.OwnerClientId} out of cabinet {m_RoomId} ({(alone ? "alone" : "helped")})");
        }

        private void Empty()
        {
            m_Captive = null;
            m_Occupied.Value = false;
            m_Unlocked.Value = false;
        }

        /// <summary>Editor setup.</summary>
        public void Configure(string roomId, Light light)
        {
            m_RoomId = roomId;
            m_Light = light;
        }
    }
}
