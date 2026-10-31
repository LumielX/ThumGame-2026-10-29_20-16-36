namespace SpiritMovers.Core.Ghost
{
    /// <summary>Tuning for the spirit camera (GDD 5.2, 8.3; ⚙ defaults in the M3D plan).</summary>
    public sealed class ToolConfig
    {
        public int FilmPerCamera { get; set; } = 6;
        public float OverlaySeconds { get; set; } = 3f;
        /// <summary>Only memory slots this close to the camera show up.</summary>
        public float OverlayRange { get; set; } = 12f;
        /// <summary>A peek opens a cabinet within this distance and angle of the view.</summary>
        public float CabinetAimRange { get; set; } = 6f;
        public float CabinetAimAngle { get; set; } = 20f;
        public float PeekCooldown { get; set; } = 1f;

        /// <summary>Spirit cameras in the team toolbox: 1 for 1–3 players, 2 for 4–6 (GDD 8.3).</summary>
        public static int SpiritCameras(int players) => players >= 4 ? 2 : 1;
    }
}
