using System;
using System.Collections.Generic;

namespace SpiritMovers.Core.Job
{
    /// <summary>Things players do that annoy the ghost (GDD 6.2).</summary>
    public enum AngerEvent
    {
        ItemBroken,
        /// <summary>The bell was rung and the ghost found items out of place.</summary>
        WrongNod,
        LoudNoise,
        CameraUsed,
        BellRung,
    }

    /// <summary>Tuning for <see cref="AngerModel"/> (⚙ values in GDD 6.2; defaults in the M3A plan).</summary>
    public sealed class AngerConfig
    {
        /// <summary>Value at which stages 1–4 start; stage 4 evicts the team.</summary>
        public float[] StageThresholds { get; set; } = { 25f, 50f, 75f, 100f };
        public float Max { get; set; } = 100f;

        public Dictionary<AngerEvent, float> EventAmounts { get; set; } = new()
        {
            [AngerEvent.ItemBroken] = 8f,
            [AngerEvent.WrongNod] = 4f,
            [AngerEvent.LoudNoise] = 3f,
            [AngerEvent.CameraUsed] = 2f,
            [AngerEvent.BellRung] = 3f,
        };

        public float PerSecond { get; set; } = 0.05f;
        public float RoomAccepted { get; set; } = 20f;
        public float Wai { get; set; } = 10f;
        public float SilenceGraceSeconds { get; set; } = 5f;
        public float SilencePerSecond { get; set; } = 0.5f;

        /// <summary>A fresh copy, so tuning one job never changes another.</summary>
        public static AngerConfig Default => new();

        /// <summary>Problems in hand-edited tuning data; empty when usable (M3A carry-over 4).</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (StageThresholds == null || StageThresholds.Length != 4)
            {
                problems.Add("need four stage thresholds");
            }
            else
            {
                for (var i = 1; i < StageThresholds.Length; i++)
                    if (StageThresholds[i] <= StageThresholds[i - 1])
                        problems.Add($"thresholds not ascending at {i}");
                if (Max < StageThresholds[StageThresholds.Length - 1])
                    problems.Add($"Max {Max} is below the eviction threshold: eviction unreachable");
            }
            if (PerSecond < 0f || RoomAccepted < 0f || Wai < 0f || SilencePerSecond < 0f)
                problems.Add("negative rate");
            return problems;
        }
    }

    /// <summary>
    /// The ghost's anger, 0..Max, read as stages 0 (calm) to 4 (evicts everyone, GDD 6.2).
    /// Increases are scaled by difficulty × player count; decreases are not. Runs on the host only.
    /// </summary>
    public sealed class AngerModel
    {
        private readonly AngerConfig m_Config;
        private readonly float m_RateMultiplier;
        private readonly HashSet<string> m_WaiRooms = new();
        private float m_SilentSeconds;

        public float Value { get; private set; }
        public int Stage { get; private set; }
        public int HighestStage { get; private set; }
        public bool IsEvicted => Stage >= 4;

        public event Action<int> StageChanged;

        public AngerModel(AngerConfig config, float rateMultiplier)
        {
            m_Config = config;
            m_RateMultiplier = rateMultiplier;
        }

        public void Apply(AngerEvent e) =>
            Increase(m_Config.EventAmounts.TryGetValue(e, out var amount) ? amount : 0f);

        public void Tick(float seconds, bool everyoneSilent)
        {
            if (IsEvicted)
                return;

            if (!everyoneSilent)
            {
                m_SilentSeconds = 0f;
                Increase(m_Config.PerSecond * seconds);
                return;
            }

            // Only the part of the silence beyond the grace period calms the ghost.
            var before = m_SilentSeconds;
            m_SilentSeconds += seconds;
            var calming = m_SilentSeconds - MathF.Max(before, m_Config.SilenceGraceSeconds);
            if (calming > 0f)
                SetValue(Value - calming * m_Config.SilencePerSecond);
        }

        public void OnRoomAccepted() => SetValue(Value - m_Config.RoomAccepted);

        /// <summary>The "wai" emote calms the ghost once per room; returns false if it had no effect.</summary>
        public bool Wai(string roomId)
        {
            if (IsEvicted || !m_WaiRooms.Add(roomId))
                return false;
            SetValue(Value - m_Config.Wai);
            return true;
        }

        /// <summary>Solo players caught by the ghost calm it by one stage (D-011): just below the current stage's threshold.</summary>
        public void CalmOneStage()
        {
            if (Stage == 0 || IsEvicted)
                return;
            SetValue(m_Config.StageThresholds[Stage - 1] - 1f);
        }

        private void Increase(float amount) => SetValue(Value + amount * m_RateMultiplier);

        private void SetValue(float value)
        {
            if (IsEvicted)
                return;

            Value = Math.Clamp(value, 0f, m_Config.Max);
            var stage = 0;
            foreach (var threshold in m_Config.StageThresholds)
                if (Value >= threshold)
                    stage++;

            if (stage == Stage)
                return;
            Stage = stage;
            HighestStage = Math.Max(HighestStage, stage);
            StageChanged?.Invoke(stage);
        }
    }
}
