using NUnit.Framework;
using SpiritMovers.Core.Session;

namespace SpiritMovers.Tests
{
    public class SessionRulesTests
    {
        [Test]
        public void CanJoin_VersionCheckedFirst()
        {
            Assert.AreEqual(JoinRefusal.VersionMismatch, SessionRules.CanJoin(SessionPhase.Job, 7, 6, versionMatches: false));
        }

        [Test]
        public void CanJoin_FullBeforePhase()
        {
            Assert.AreEqual(JoinRefusal.Full, SessionRules.CanJoin(SessionPhase.Job, 6, 6, versionMatches: true));
        }

        [Test]
        public void CanJoin_OnlyInLobby()
        {
            Assert.AreEqual(JoinRefusal.None, SessionRules.CanJoin(SessionPhase.Lobby, 2, 6, true));
            foreach (var phase in new[] { SessionPhase.Loading, SessionPhase.Job, SessionPhase.Results })
                Assert.AreEqual(JoinRefusal.JobInProgress, SessionRules.CanJoin(phase, 2, 6, true), phase.ToString());
        }

        [Test]
        public void CanStart_NeedsEveryoneReady()
        {
            Assert.IsTrue(SessionRules.CanStart(new[] { true, true }));
            Assert.IsFalse(SessionRules.CanStart(new[] { true, false }));
            Assert.IsFalse(SessionRules.CanStart(new bool[0]));
        }

        [Test]
        public void SizeLabel_SupportedExperimentalTooMany()
        {
            Assert.AreEqual(CrewSize.Supported, SessionRules.SizeLabel(1));
            Assert.AreEqual(CrewSize.Supported, SessionRules.SizeLabel(4));
            Assert.AreEqual(CrewSize.Experimental, SessionRules.SizeLabel(5));
            Assert.AreEqual(CrewSize.Experimental, SessionRules.SizeLabel(6));
            Assert.AreEqual(CrewSize.TooMany, SessionRules.SizeLabel(7));
        }

        [Test]
        public void HostLeftPay_PaysAcceptedRoomsShare()
        {
            Assert.AreEqual(50, SessionRules.HostLeftPay(SessionPhase.Job, 1, 3, 300, 2));
        }

        [Test]
        public void HostLeftPay_NothingOutsideJob()
        {
            Assert.AreEqual(0, SessionRules.HostLeftPay(SessionPhase.Lobby, 1, 3, 300, 2));
            Assert.AreEqual(0, SessionRules.HostLeftPay(SessionPhase.Loading, 1, 3, 300, 2));
            Assert.AreEqual(150, SessionRules.HostLeftPay(SessionPhase.Results, 3, 3, 300, 2));
        }

        [Test]
        public void NormalizeCode_TrimsAndUppercases()
        {
            Assert.AreEqual("ABC1", SessionRules.NormalizeCode(" ab c1 \n"));
            Assert.IsNull(SessionRules.NormalizeCode(""));
            Assert.IsNull(SessionRules.NormalizeCode("   "));
            Assert.IsNull(SessionRules.NormalizeCode(null));
            Assert.IsNull(SessionRules.NormalizeCode("AB-1"));
        }
    }
}
