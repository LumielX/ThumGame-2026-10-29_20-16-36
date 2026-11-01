using System;

namespace SpiritMovers.Core.Save
{
    /// <summary>The six volume groups (TECHNICAL_DESIGN 13).</summary>
    public enum AudioGroup { Master, Music, Sfx, Ambience, Voice, Ui }

    /// <summary>
    /// The player's settings in settings.json (TECHNICAL_DESIGN 9, GDD 10/18/19): language, mouse, field of view,
    /// volumes, voice options and the Input System binding overrides (JSON). ⚙ defaults from the M4B plan.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentSchema = 1;
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;
        public const float MinFov = 60f;
        public const float MaxFov = 100f;

        public int schema = CurrentSchema;
        public string language = "th";
        public float mouseSensitivity = 1f;
        public bool invertY;
        public float fov = 75f;
        public float master = 1f;
        public float music = 1f;
        public float sfx = 1f;
        public float ambience = 1f;
        public float voice = 1f;
        public float ui = 1f;
        public bool pushToTalk;
        public bool ghostHearsMic = true;
        public string bindingOverrides = "";

        public float Volume(AudioGroup group) => group switch
        {
            AudioGroup.Master => master,
            AudioGroup.Music => music,
            AudioGroup.Sfx => sfx,
            AudioGroup.Ambience => ambience,
            AudioGroup.Voice => voice,
            _ => ui,
        };

        public void SetVolume(AudioGroup group, float value)
        {
            value = Clamp(value, 0f, 1f, 1f);
            switch (group)
            {
                case AudioGroup.Master: master = value; break;
                case AudioGroup.Music: music = value; break;
                case AudioGroup.Sfx: sfx = value; break;
                case AudioGroup.Ambience: ambience = value; break;
                case AudioGroup.Voice: voice = value; break;
                default: ui = value; break;
            }
        }

        /// <summary>A copy with every value in range; unknown languages fall back to Thai (GDD 19).</summary>
        public SettingsData Sanitized()
        {
            var defaults = new SettingsData();
            return new SettingsData
            {
                schema = schema,
                language = language == "en" ? "en" : "th",
                mouseSensitivity = Clamp(mouseSensitivity, MinSensitivity, MaxSensitivity, defaults.mouseSensitivity),
                invertY = invertY,
                fov = Clamp(fov, MinFov, MaxFov, defaults.fov),
                master = Clamp(master, 0f, 1f, defaults.master),
                music = Clamp(music, 0f, 1f, defaults.music),
                sfx = Clamp(sfx, 0f, 1f, defaults.sfx),
                ambience = Clamp(ambience, 0f, 1f, defaults.ambience),
                voice = Clamp(voice, 0f, 1f, defaults.voice),
                ui = Clamp(ui, 0f, 1f, defaults.ui),
                pushToTalk = pushToTalk,
                ghostHearsMic = ghostHearsMic,
                bindingOverrides = bindingOverrides ?? "",
            };
        }

        private static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(max, Math.Max(min, value));
    }
}
