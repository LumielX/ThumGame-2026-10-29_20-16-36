using System;

namespace SpiritMovers.Core.Player
{
    /// <summary>What the player is holding, as far as movement cares (D-015).</summary>
    public enum CarryLoad
    {
        None,
        Light,
        Heavy,
        /// <summary>A heavy item carried by two or more players: faster than alone (GDD 7).</summary>
        HeavyShared,
    }

    /// <summary>Movement input for one frame; forward/right in -1..1.</summary>
    public readonly struct MoveIntent
    {
        public float Forward { get; }
        public float Right { get; }
        public bool Sprint { get; }
        public bool Crouch { get; }
        public bool Jump { get; }

        public MoveIntent(float forward, float right, bool sprint, bool crouch, bool jump)
        {
            Forward = forward;
            Right = right;
            Sprint = sprint;
            Crouch = crouch;
            Jump = jump;
        }
    }

    /// <summary>⚙ movement tuning; defaults from the M3B plan.</summary>
    public sealed class MovementConfig
    {
        public float WalkSpeed { get; set; } = 3.0f;
        public float RunSpeed { get; set; } = 5.5f;
        public float CrouchSpeed { get; set; } = 1.6f;
        public float JumpHeight { get; set; } = 0.6f;
        public float Gravity { get; set; } = -20f;
        public float HeavySpeedFactor { get; set; } = 0.6f;
        public float SharedHeavySpeedFactor { get; set; } = 0.85f;
        /// <summary>Loudness 0..1 the sharp-eared ghost hears (M3D).</summary>
        public float NoiseWalk { get; set; } = 0.3f;
        public float NoiseRun { get; set; } = 1.0f;
        public float NoiseCrouch { get; set; } = 0.05f;

        public static MovementConfig Default => new();
    }

    public readonly struct MoveResult
    {
        /// <summary>Planar speed in m/s.</summary>
        public float Speed { get; }
        public bool Running { get; }
        public bool Crouching { get; }
        public bool CanJump { get; }
        public float Noise { get; }

        public MoveResult(float speed, bool running, bool crouching, bool canJump, float noise)
        {
            Speed = speed;
            Running = running;
            Crouching = crouching;
            CanJump = canJump;
            Noise = noise;
        }
    }

    /// <summary>Walk / run / crouch / carry speed rules (D-015, GDD 8.1).</summary>
    public static class MovementRules
    {
        private const float DeadZone = 0.1f;

        public static MoveResult Evaluate(MoveIntent intent, bool staminaAllowsRun, CarryLoad load, MovementConfig config)
        {
            var heavy = load == CarryLoad.Heavy || load == CarryLoad.HeavyShared;
            var moving = MathF.Sqrt(intent.Forward * intent.Forward + intent.Right * intent.Right) > DeadZone;
            var crouching = intent.Crouch;
            var running = moving && intent.Sprint && !crouching && !heavy && staminaAllowsRun;
            var canJump = !heavy;

            if (!moving)
                return new MoveResult(0f, false, crouching, canJump, 0f);

            float speed, noise;
            if (crouching)
            {
                speed = config.CrouchSpeed;
                noise = config.NoiseCrouch;
            }
            else if (running)
            {
                speed = config.RunSpeed;
                noise = config.NoiseRun;
            }
            else
            {
                speed = config.WalkSpeed;
                noise = config.NoiseWalk;
            }

            if (load == CarryLoad.Heavy)
                speed *= config.HeavySpeedFactor;
            else if (load == CarryLoad.HeavyShared)
                speed *= config.SharedHeavySpeedFactor;

            return new MoveResult(speed, running, crouching, canJump, noise);
        }
    }
}
