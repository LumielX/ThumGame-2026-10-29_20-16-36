using System.Collections.Generic;

namespace SpiritMovers.Core.Job
{
    /// <summary>Job size and pressure per difficulty (GDD 4.4); pay values are ⚙ defaults from the M3A plan.</summary>
    public sealed class JobDifficulty
    {
        public string Id { get; set; }
        public int MinRooms { get; set; }
        public int MaxRooms { get; set; }
        public int MinItemsPerRoom { get; set; }
        public int MaxItemsPerRoom { get; set; }
        public float AngerMultiplier { get; set; } = 1f;
        public IReadOnlyList<ProblemType> Problems { get; set; }
        public int BasePay { get; set; }

        public static JobDifficulty Normal => new()
        {
            Id = "normal",
            MinRooms = 3, MaxRooms = 3,
            MinItemsPerRoom = 3, MaxItemsPerRoom = 4,
            AngerMultiplier = 1f,
            Problems = new[] { ProblemType.Moved, ProblemType.Hidden },
            BasePay = 300,
        };

        public static JobDifficulty Hard => new()
        {
            Id = "hard",
            MinRooms = 4, MaxRooms = 5,
            MinItemsPerRoom = 5, MaxItemsPerRoom = 6,
            AngerMultiplier = 1.4f,
            Problems = new[] { ProblemType.Moved, ProblemType.Hidden, ProblemType.Order, ProblemType.Possessed },
            BasePay = 500,
        };
    }
}
