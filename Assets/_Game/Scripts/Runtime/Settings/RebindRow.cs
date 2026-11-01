using System;
using SpiritMovers.Core.Text;
using SpiritMovers.Session;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpiritMovers.Settings
{
    /// <summary>
    /// One line of the keys tab: the action, its key now, a button to press a new key (Esc cancels, moving the mouse
    /// does not count) and a warning when another action uses the same key (GDD 18).
    /// </summary>
    public sealed class RebindRow : MonoBehaviour
    {
        private InputAction m_Action;
        private int m_BindingIndex;
        private TMP_FontAsset m_Font;
        private TextMeshProUGUI m_Key;
        private TextMeshProUGUI m_Conflict;
        private InputActionRebindingExtensions.RebindingOperation m_Operation;
        private Action<RebindRow, bool> m_Finished;

        /// <summary>"Map/Action", plus the composite part for Move ("up").</summary>
        public string ActionPath { get; private set; }
        public string Part { get; private set; }
        /// <summary>Name used to find clashes: the action, or "Move/up" for a part.</summary>
        public string ConflictName => Part == null ? m_Action.name : $"{m_Action.name}/{Part}";
        public string LabelKey { get; private set; }
        public InputBinding Binding => m_Action.bindings[m_BindingIndex];
        public bool Waiting => m_Operation != null;
        public bool Conflicted { get; private set; }
        public string KeyText => m_Key != null ? m_Key.text : "";

        internal void Init(InputAction action, string actionPath, string part, string labelKey, TMP_FontAsset font,
            Action<RebindRow, bool> finished)
        {
            m_Action = action;
            ActionPath = actionPath;
            Part = part;
            LabelKey = labelKey;
            m_Font = font;
            m_Finished = finished;
            m_BindingIndex = part == null ? 0 : action.bindings.IndexOf(b => b.isPartOfComposite && b.name == part);

            var row = gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12;
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var label = SessionUi.CreateLabel(transform, font, 40, 24);
            label.text = SessionUi.Text(labelKey);
            label.GetComponent<LayoutElement>().preferredWidth = 260;
            var button = SessionUi.CreateButton(transform, font, "settings.change_key", StartRebind);
            button.GetComponent<LayoutElement>().preferredWidth = 220;
            m_Key = button.GetComponentInChildren<TextMeshProUGUI>();
            m_Conflict = SessionUi.CreateLabel(transform, font, 40, 22);
            m_Conflict.GetComponent<LayoutElement>().preferredWidth = 300;
            Refresh();
        }

        /// <summary>Wait for the next key or mouse button; Esc cancels.</summary>
        public void StartRebind()
        {
            if (Waiting)
                return;
            var wasEnabled = m_Action.enabled;
            m_Action.Disable();
            m_Operation = m_Action.PerformInteractiveRebinding(m_BindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => Finish(true, wasEnabled))
                .OnCancel(_ => Finish(false, wasEnabled))
                .Start();
            m_Key.text = SessionUi.Text("settings.press_key");
        }

        private void Finish(bool changed, bool wasEnabled)
        {
            m_Operation.Dispose();
            m_Operation = null;
            if (wasEnabled)
                m_Action.Enable();
            Refresh();
            m_Finished?.Invoke(this, changed);
        }

        /// <summary>Show the key now bound and whether another action uses it (<paramref name="clashWith"/>, or null).</summary>
        internal void Refresh(string clashWith = null)
        {
            if (Waiting)
                return;
            Conflicted = clashWith != null;
            m_Key.text = ThaiShaper.Shape(KeyNames.Display(m_Action, m_BindingIndex));
            m_Conflict.text = Conflicted ? SessionUi.Text("settings.conflict", clashWith) : "";
        }

        private void OnDestroy()
        {
            // Closing the page mid-wait: stop listening without telling the page (it is going away too).
            m_Finished = null;
            m_Operation?.Cancel();
        }
    }
}
