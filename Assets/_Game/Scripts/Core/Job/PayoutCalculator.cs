using System;

namespace SpiritMovers.Core.Job
{
    /// <summary>The three ways a job ends (GDD 4.3). A host leaving pays like <see cref="LeftEarly"/>.</summary>
    public enum JobEnding
    {
        FullSuccess,
        LeftEarly,
        Evicted,
    }

    /// <summary>⚙ pay tuning; defaults from the M3A plan.</summary>
    public sealed class PayoutRules
    {
        public float StarBonusPerStar { get; set; } = 0.10f;
        public float HintCost { get; set; } = 0.05f;
        public float EvictedFactor { get; set; } = 0.5f;

        public static PayoutRules Default => new();
    }

    /// <summary>The ghost's review, 1–3 stars, used for the full-success bonus.</summary>
    public static class StarRating
    {
        public static int Calculate(int highestAngerStage, int itemsBroken)
        {
            var stars = 3;
            if (highestAngerStage >= 3)
                stars--;
            if (itemsBroken > 0)
                stars--;
            return Math.Max(1, stars);
        }
    }

    public readonly struct Payout
    {
        public int TeamTotal { get; }
        public int PerPlayer { get; }
        /// <summary>0 unless the job was a full success.</summary>
        public int Stars { get; }

        public Payout(int teamTotal, int perPlayer, int stars)
        {
            TeamTotal = teamTotal;
            PerPlayer = perPlayer;
            Stars = stars;
        }
    }

    /// <summary>In-game pay, split equally across the team (GDD 4.3, 5.4, 12). Never negative.</summary>
    public static class PayoutCalculator
    {
        public static Payout Calculate(JobEnding ending, int basePay, int roomsAccepted, int roomsRequired, int stars,
            int hintsUsed, int playerCount, PayoutRules rules)
        {
            // decimal keeps shares like 2/3 of 300 exact (float gives 199.99…, which would floor to 199).
            decimal pay = basePay;
            decimal share = ending == JobEnding.FullSuccess ? 1m
                : roomsRequired > 0 ? (decimal)Math.Clamp(roomsAccepted, 0, roomsRequired) / roomsRequired
                : 0m;
            pay *= share;

            if (ending == JobEnding.Evicted)
                pay *= (decimal)rules.EvictedFactor;
            var awardedStars = ending == JobEnding.FullSuccess ? Math.Clamp(stars, 0, 3) : 0;
            hintsUsed = Math.Max(0, hintsUsed);
            pay += basePay * (decimal)rules.StarBonusPerStar * awardedStars;
            pay -= basePay * (decimal)rules.HintCost * hintsUsed;

            var team = (int)Math.Max(0m, Math.Floor(pay));
            return new Payout(team, team / Math.Max(1, playerCount), awardedStars);
        }
    }
}
