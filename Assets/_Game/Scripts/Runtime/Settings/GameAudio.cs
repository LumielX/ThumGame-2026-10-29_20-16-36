using SpiritMovers.Core.Save;
using SpiritMovers.Save;

namespace SpiritMovers.Settings
{
    /// <summary>
    /// Volume per group (TECHNICAL_DESIGN 13). No Audio Mixer yet (M4B plan): voice (M4C) and real sounds (M5) scale
    /// their sources by <see cref="Gain"/>; the master volume is the AudioListener's.
    /// </summary>
    public static class GameAudio
    {
        public static float Gain(AudioGroup group)
        {
            var settings = GameData.Settings;
            return group == AudioGroup.Master ? settings.master : settings.master * settings.Volume(group);
        }
    }
}
