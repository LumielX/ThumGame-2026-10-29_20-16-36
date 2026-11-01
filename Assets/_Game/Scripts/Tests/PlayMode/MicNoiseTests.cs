using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Dev;
using SpiritMovers.Ghost;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class MicNoiseTests : GameFlowFixture
    {
        private static int VoiceNoises => NoiseHub.Current.Recent.Count(e => e.Source == NoiseSource.Voice);

        private IEnumerator Talking(FakeVoiceService voice)
        {
            yield return OnlineIntoJob(voice);
            yield return WaitFor(() => VoiceDirector.Instance.State == VoiceState.On, 5f, "voice on");
            voice.SetEnergy(0.8);
        }

        [UnityTest]
        public IEnumerator MicNoise_LoudVoiceReachesTheGhost()
        {
            var voice = new FakeVoiceService();
            yield return Talking(voice);
            yield return WaitFor(() => VoiceNoises > 0, 0.5f, "the ghost hears the voice");
            var heard = NoiseHub.Current.Recent.Last(e => e.Source == NoiseSource.Voice);
            var head = PlayerMotor.Local.Head.position;
            Assert.Less(Vector3.Distance(new Vector3(heard.Position.X, heard.Position.Y, heard.Position.Z), head), 0.5f);
            Assert.AreEqual(0.8f, heard.Loudness, 0.001f);
        }

        [UnityTest]
        public IEnumerator MicNoise_OffSendsNothing()
        {
            GameData.Settings.ghostHearsMic = false;
            var voice = new FakeVoiceService();
            yield return Talking(voice);
            var micNoise = PlayerMotor.Local.GetComponent<MicNoise>();
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(0, VoiceNoises);
            Assert.AreEqual(0, micNoise.ReportsSent, "nothing even leaves this machine");
        }

        [UnityTest]
        public IEnumerator MicNoise_MutedOrPttReleasedSendsNothing()
        {
            GameData.Settings.pushToTalk = true;
            var voice = new FakeVoiceService();
            yield return Talking(voice);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, VoiceNoises);
        }

        [UnityTest]
        public IEnumerator MicNoise_ServerRateLimited()
        {
            GameData.Settings.ghostHearsMic = false; // only the direct reports below
            var voice = new FakeVoiceService();
            yield return Talking(voice);
            var micNoise = PlayerMotor.Local.GetComponent<MicNoise>();
            for (var i = 0; i < 20; i++)
                micNoise.ReportVoiceRpc(9f);
            yield return null;
            Assert.AreEqual(1, VoiceNoises, "twenty in one frame count once");
            Assert.AreEqual(1f, NoiseHub.Current.Recent.Last(e => e.Source == NoiseSource.Voice).Loudness, "clamped");
            var start = Time.time;
            while (Time.time - start < 0.9f)
            {
                micNoise.ReportVoiceRpc(0.9f);
                yield return null;
            }
            Assert.LessOrEqual(VoiceNoises, 5, "at most 4 a second after the first");
        }
    }
}
