using System.Collections.Generic;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Voice
{
    /// <summary>
    /// A walkie-talkie (GDD 8.3, 10, D-010): a real tool from the van's toolbox — put down, thrown and passed on like
    /// any carryable. Holding it and keeping Use pressed talks on the team radio; only players holding a walkie hear
    /// the radio (RadioListener). Letting go of it in any way stops the talking in that frame.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public sealed class Walkie : NetworkBehaviour, IUsable
    {
        private static readonly List<Walkie> s_All = new();

        private Carryable m_Item;
        private bool m_Talking;

        public string UsePromptKey => "radio.talk";

        /// <summary>This machine's player has a walkie-talkie in hand.</summary>
        public static bool LocalHolds
        {
            get
            {
                var local = PlayerMotor.Local;
                if (local == null)
                    return false;
                foreach (var walkie in s_All)
                    if (walkie.HeldBy(local.OwnerClientId))
                        return true;
                return false;
            }
        }

        private void Awake() => m_Item = GetComponent<Carryable>();

        private void OnEnable() => s_All.Add(this);

        private void OnDisable()
        {
            s_All.Remove(this);
            StopTalking();
        }

        /// <summary>Talking is held, not pressed: Update follows the button every frame.</summary>
        public void Use(PlayerCarrier by)
        {
        }

        private void Update()
        {
            var local = PlayerMotor.Local;
            var talking = local != null && HeldBy(local.OwnerClientId) && local.CurrentInput.UseHeld;
            if (talking == m_Talking)
                return;
            if (talking)
            {
                m_Talking = true;
                if (VoiceDirector.Instance != null)
                    VoiceDirector.Instance.RadioHeld = true;
            }
            else
            {
                StopTalking();
            }
        }

        private bool HeldBy(ulong clientId) => IsSpawned && m_Item.IsCarriedBy(clientId);

        private void StopTalking()
        {
            if (!m_Talking)
                return;
            m_Talking = false;
            if (VoiceDirector.Instance != null)
                VoiceDirector.Instance.RadioHeld = false;
        }
    }
}
