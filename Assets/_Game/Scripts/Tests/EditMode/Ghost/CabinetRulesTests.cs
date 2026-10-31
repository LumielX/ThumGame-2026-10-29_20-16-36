using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;

namespace SpiritMovers.Tests
{
    public class CabinetRulesTests
    {
        [Test]
        public void ReleaseSeconds_SoloIsShorter()
        {
            Assert.AreEqual(45f, CabinetRules.ReleaseSeconds(solo: false, new GhostConfig()));
            Assert.AreEqual(20f, CabinetRules.ReleaseSeconds(solo: true, new GhostConfig()));
        }

        [Test]
        public void CanFriendOpen_NeedsOneMoreCorrectOrCamera()
        {
            Assert.IsFalse(CabinetRules.CanFriendOpen(correctAtCapture: 2, correctNow: 2, cameraAimed: false));
            Assert.IsFalse(CabinetRules.CanFriendOpen(correctAtCapture: 2, correctNow: 1, cameraAimed: false));
            Assert.IsTrue(CabinetRules.CanFriendOpen(correctAtCapture: 2, correctNow: 3, cameraAimed: false));
            Assert.IsTrue(CabinetRules.CanFriendOpen(correctAtCapture: 2, correctNow: 2, cameraAimed: true));
        }

        [Test]
        public void SpiritCameras_FollowPlayerCountTable()
        {
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 2, 2, 2 },
                new[] { 1, 2, 3, 4, 5, 6 }.Select(ToolConfig.SpiritCameras));
        }
    }
}
