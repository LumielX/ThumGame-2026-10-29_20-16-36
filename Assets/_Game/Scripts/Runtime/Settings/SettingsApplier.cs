using System;
using SpiritMovers.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

namespace SpiritMovers.Settings
{
    /// <summary>
    /// Puts the saved settings into effect (GDD 18/19, TECHNICAL_DESIGN 11/13): the language, the master volume and
    /// the player's key bindings. The mouse and field of view are read by PlayerMotor every frame.
    /// </summary>
    public static class SettingsApplier
    {
        /// <summary>The game's input actions (the asset every player reads), set by SettingsHost on GameNetwork.</summary>
        public static InputActionAsset Actions { get; internal set; }

        /// <summary>The language changed: screens that built their text once build it again.</summary>
        public static event Action LanguageChanged;

        public static void ApplyAll()
        {
            ApplyLanguage();
            ApplyAudio();
            if (Actions != null)
                ApplyBindings(Actions);
        }

        public static void ApplyLanguage()
        {
            var init = LocalizationSettings.InitializationOperation;
            if (!init.IsDone)
                init.WaitForCompletion();
            var locale = LocalizationSettings.AvailableLocales.GetLocale(GameData.Settings.language);
            if (locale != null && LocalizationSettings.SelectedLocale != locale)
                LocalizationSettings.SelectedLocale = locale;
            LanguageChanged?.Invoke();
        }

        /// <summary>Master volume for everything heard; the other groups are read through GameAudio.Gain.</summary>
        public static void ApplyAudio() => AudioListener.volume = GameAudio.Gain(Core.Save.AudioGroup.Master);

        /// <summary>The saved overrides on top of the default bindings; a damaged override file means the defaults.</summary>
        public static void ApplyBindings(InputActionAsset actions)
        {
            actions.RemoveAllBindingOverrides();
            var json = GameData.Settings.bindingOverrides;
            if (string.IsNullOrEmpty(json))
                return;
            try
            {
                actions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] key bindings could not be read, using the defaults: {e.Message}");
                actions.RemoveAllBindingOverrides();
            }
        }

        public static string CaptureBindings(InputActionAsset actions) => actions.SaveBindingOverridesAsJson();
    }
}
