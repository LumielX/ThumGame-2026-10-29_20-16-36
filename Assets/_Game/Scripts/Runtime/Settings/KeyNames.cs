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
            return action == null || action.bindings.Count == 0 ? "?" : Display(action, 0);
        }

        /// <summary>The name of one binding of an action as it is bound now.</summary>
        public static string Display(InputAction action, int bindingIndex) => action.bindings[bindingIndex].effectivePath switch
        {
            "<Mouse>/leftButton" => JobUi.Text("key.mouse_left"),
            "<Mouse>/rightButton" => JobUi.Text("key.mouse_right"),
            "<Mouse>/middleButton" => JobUi.Text("key.mouse_middle"),
            // Not GetBindingDisplayString: that asks the keyboard, and a Thai layout names T "ะ" (M4B final review).
            var path => InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice),
        };

        /// <summary>The key names an interaction prompt shows in its "{0}" (none for prompts without one).</summary>
        public static object[] ArgsFor(string promptKey) => promptKey switch
        {
            "camera.use" => new object[] { For("Gameplay/Use") },
            _ => null,
        };
    }
}
