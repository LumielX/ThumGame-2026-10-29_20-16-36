using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Settings
{
    /// <summary>On the persistent GameNetwork object: hands the game's input actions to the settings code.</summary>
    public sealed class SettingsHost : MonoBehaviour
    {
        [SerializeField] private InputActionAsset m_Actions;

        private void Awake() => SettingsApplier.Actions = m_Actions;
    }
}
