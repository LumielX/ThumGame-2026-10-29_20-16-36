using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritMovers.Player
{
    /// <summary>Reads the "Gameplay" action map of SpiritMovers.inputactions (TECHNICAL_DESIGN 11).</summary>
    public sealed class KeyboardMouseInput : IPlayerInputSource
    {
        private readonly InputAction m_Move;
        private readonly InputAction m_Look;
        private readonly InputAction m_Sprint;
        private readonly InputAction m_Crouch;
        private readonly InputAction m_Jump;
        private readonly InputAction m_Interact;
        private readonly InputAction m_Throw;
        private readonly InputAction m_Rotate;
        private readonly InputAction m_JobSheet;
        private readonly InputAction m_Wai;
        private readonly InputAction m_Use;
        private readonly InputAction m_Ready;
        private readonly InputAction m_Confirm;
        private readonly InputAction m_Pause;
        private readonly InputAction m_Talk;

        public KeyboardMouseInput(InputActionAsset actions)
        {
            var map = actions.FindActionMap("Gameplay", true);
            m_Move = map.FindAction("Move", true);
            m_Look = map.FindAction("Look", true);
            m_Sprint = map.FindAction("Sprint", true);
            m_Crouch = map.FindAction("Crouch", true);
            m_Jump = map.FindAction("Jump", true);
            m_Interact = map.FindAction("Interact", true);
            m_Throw = map.FindAction("Throw", true);
            m_Rotate = map.FindAction("RotateItem", true);
            m_JobSheet = map.FindAction("JobSheet", true);
            m_Wai = map.FindAction("Wai", true);
            m_Use = map.FindAction("Use", true);
            m_Ready = map.FindAction("Ready", true);
            m_Confirm = map.FindAction("Confirm", true);
            m_Pause = map.FindAction("Pause", true);
            map.Enable();
            var voice = actions.FindActionMap("Voice", true);
            m_Talk = voice.FindAction("PushToTalk", true);
            voice.Enable();
        }

        public PlayerInputState Read()
        {
            var scroll = m_Rotate.ReadValue<float>();
            return new PlayerInputState
            {
                Move = m_Move.ReadValue<Vector2>(),
                Look = m_Look.ReadValue<Vector2>(),
                Sprint = m_Sprint.IsPressed(),
                Crouch = m_Crouch.IsPressed(),
                JumpPressed = m_Jump.WasPressedThisFrame(),
                InteractPressed = m_Interact.WasPressedThisFrame(),
                ThrowPressed = m_Throw.WasPressedThisFrame(),
                // Scroll units differ between devices; one notch = one step.
                RotateSteps = Mathf.Abs(scroll) > 0.01f ? Mathf.Sign(scroll) : 0f,
                JobSheetHeld = m_JobSheet.IsPressed(),
                WaiPressed = m_Wai.WasPressedThisFrame(),
                UsePressed = m_Use.WasPressedThisFrame(),
                ReadyPressed = m_Ready.WasPressedThisFrame(),
                ConfirmPressed = m_Confirm.WasPressedThisFrame(),
                PausePressed = m_Pause.WasPressedThisFrame(),
                TalkHeld = m_Talk.IsPressed(),
            };
        }
    }
}
