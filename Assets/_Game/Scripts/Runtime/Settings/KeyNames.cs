using SpiritMovers.Job;
using UnityEngine.InputSystem;

namespace SpiritMovers.Settings
{
    /// <summary>
    /// The key an action is bound to right now, as the player should read it in a hint (D-024, TECHNICAL_DESIGN 11):
    /// the keyboard's own name ("T", "Enter") or a localized mouse button ("คลิกขวา").
    /// </summary>
    public static class KeyNames
    {
        /// <summary>"Map/Action" (e.g. "Gameplay/Ready"); the first binding is the one the settings page changes.</summary>
        public static string For(string actionPath)
        {
            var action = SettingsApplier.Actions != null ? SettingsApplier.Actions.FindAction(actionPath) : null;
            if (action == null || action.bindings.Count == 0)
                return "?";
            switch (action.bindings[0].effectivePath)
            {
                case "<Mouse>/leftButton": return JobUi.Text("key.mouse_left");
                case "<Mouse>/rightButton": return JobUi.Text("key.mouse_right");
                case "<Mouse>/middleButton": return JobUi.Text("key.mouse_middle");
                default: return action.GetBindingDisplayString(0);
            }
        }

        /// <summary>The key names an interaction prompt shows in its "{0}" (none for prompts without one).</summary>
        public static object[] ArgsFor(string promptKey) => promptKey switch
        {
            "camera.use" => new object[] { For("Gameplay/Use") },
            _ => null,
        };
    }
}
