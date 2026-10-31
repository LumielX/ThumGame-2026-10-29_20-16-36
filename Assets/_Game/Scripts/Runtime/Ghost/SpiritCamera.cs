using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// The spirit camera (GDD 5.2, 8.3): hold it and right-click to see, for a few seconds, amber copies of the items
    /// that are out of place where the ghost remembers them — only on the holder's screen. Limited film per job, each
    /// look annoys the ghost, and aiming it at an occupied memory cabinet lets a friend open it. It is an ordinary
    /// carryable, so it is passed around by putting it down or throwing it.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public sealed class SpiritCamera : NetworkBehaviour, IUsable
    {
        private readonly NetworkVariable<int> m_Film = new();
        private readonly ToolConfig m_Config = new();
        private Carryable m_Item;
        private JobDirector m_Director;
        private float m_ReadyAt;

        public int Film => m_Film.Value;
        /// <summary>Looks this machine has seen through this camera (tests, bots).</summary>
        public int PeeksSeen { get; private set; }
        public string UsePromptKey => Film > 0 ? "camera.use" : "camera.empty";

        private void Awake()
        {
            m_Item = GetComponent<Carryable>();
            m_Director = FindAnyObjectByType<JobDirector>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                m_Film.Value = m_Config.FilmPerCamera;
        }

        public void Use(PlayerCarrier by)
        {
            if (by.IsOwner)
                RequestPeekRpc(by.Motor.Head.position, by.Motor.Look);
        }

        [Rpc(SendTo.Server)]
        private void RequestPeekRpc(Vector3 eye, Vector3 look, RpcParams rpcParams = default) =>
            TryPeek(rpcParams.Receive.SenderClientId, eye, look);

        /// <summary>
        /// Host: <paramref name="clientId"/> looks through the camera from <paramref name="eye"/>. Only the one player
        /// holding it, not from inside a cabinet, while exploring, with film left and off cooldown.
        /// </summary>
        internal bool TryPeek(ulong clientId, Vector3 eye, Vector3 look)
        {
            if (!IsServer || m_Director == null || m_Director.Phase != JobPhase.Exploring)
                return false;
            if (m_Item.CarrierCount != 1 || !m_Item.IsCarriedBy(clientId))
                return false;
            if (PlayerCarrier.TryGet(clientId, out var holder) && holder.TryGetComponent<PlayerCapture>(out var capture) && capture.IsCaptured)
                return false;
            if (Film <= 0 || Time.time < m_ReadyAt)
                return false;

            m_ReadyAt = Time.time + m_Config.PeekCooldown;
            m_Film.Value--;
            m_Director.CameraUsed();
            UnlockCabinetsInView(eye, look);
            PeekRpc(eye, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            Debug.Log($"[Ghost] camera peek by client {clientId}, film left {Film}");
            return true;
        }

        /// <summary>Host: occupied cabinets the camera points at (close and near the view centre) can be opened now.</summary>
        internal void UnlockCabinetsInView(Vector3 eye, Vector3 look)
        {
            foreach (var cabinet in FindObjectsByType<MemoryCabinet>(FindObjectsSortMode.None))
            {
                var toCabinet = cabinet.transform.position + Vector3.up - eye;
                if (cabinet.IsOccupied && toCabinet.magnitude <= m_Config.CabinetAimRange &&
                    Vector3.Angle(look, toCabinet) <= m_Config.CabinetAimAngle)
                    cabinet.UnlockByCamera();
            }
        }

        /// <summary>The holder's machine only: show the memory overlay.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void PeekRpc(Vector3 eye, RpcParams rpcParams = default)
        {
            PeeksSeen++;
            if (MemoryOverlay.Current != null)
                MemoryOverlay.Current.Show(eye);
        }

        /// <summary>Test hook.</summary>
        internal void SetFilmForTest(int film) => m_Film.Value = film;
    }
}
