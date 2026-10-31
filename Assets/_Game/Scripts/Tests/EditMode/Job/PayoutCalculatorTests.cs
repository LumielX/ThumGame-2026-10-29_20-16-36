using NUnit.Framework;
using SpiritMovers.Core.Job;

namespace SpiritMovers.Tests
{
    public class PayoutCalculatorTests
    {
        private static Payout Pay(JobEnding ending, int accepted, int required, int stars = 0, int hints = 0, int players = 2) =>
            PayoutCalculator.Calculate(ending, 300, accepted, required, stars, hints, players, PayoutRules.Default);

        [Test]
        public void FullSuccess_ThreeStars_PaysBasePlusThirtyPercent()
        {
            var payout = Pay(JobEnding.FullSuccess, 3, 3, stars: 3);
            Assert.AreEqual(390, payout.TeamTotal);
            Assert.AreEqual(195, payout.PerPlayer);
            Assert.AreEqual(3, payout.Stars);
        }

        [Test]
        public void LeftEarly_PaysProportionOfAcceptedRooms()
        {
            var payout = Pay(JobEnding.LeftEarly, 2, 3, stars: 3);
            Assert.AreEqual(200, payout.TeamTotal);
            Assert.AreEqual(0, payout.Stars, "stars only for a full success");
        }

        [Test]
        public void Evicted_PaysHalfOfProportion()
        {
            Assert.AreEqual(100, Pay(JobEnding.Evicted, 2, 3).TeamTotal);
        }

        [Test]
        public void Hints_ReduceTeamTotal()
        {
            Assert.AreEqual(300, Pay(JobEnding.FullSuccess, 3, 3, stars: 1, hints: 2).TeamTotal);
        }

        [Test]
        public void Calculate_HintsExceedPay_ClampsToZero()
        {
            var payout = Pay(JobEnding.LeftEarly, 0, 3, hints: 3);
            Assert.AreEqual(0, payout.TeamTotal);
            Assert.AreEqual(0, payout.PerPlayer);
        }

        [Test]
        public void PerPlayer_RoundsDown()
        {
            Assert.AreEqual(33, Pay(JobEnding.Evicted, 2, 3, players: 3).PerPlayer);
        }

        [Test]
        public void RoomsRequiredZero_PaysNothingWithoutThrowing()
        {
            Assert.AreEqual(0, Pay(JobEnding.LeftEarly, 0, 0).TeamTotal);
        }

        [Test]
        public void StarRating_FollowsRules()
        {
            Assert.AreEqual(3, StarRating.Calculate(2, 0));
            Assert.AreEqual(2, StarRating.Calculate(3, 0));
            Assert.AreEqual(1, StarRating.Calculate(3, 2));
            Assert.AreEqual(1, StarRating.Calculate(4, 5));
        }
    }
}
