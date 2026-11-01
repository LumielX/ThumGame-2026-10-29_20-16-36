using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Core.Text;
using SpiritMovers.Core.Voice;
using SpiritMovers.Ghost;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Voice;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// M4C voice check over real Relay + Vivox (Tools/m4c-voice-test.ps1, Development builds only). The host speaks
    /// from a WAV (-dev-wav) and drives phases through "{codefile}.phase"; the client keeps its microphone shut and
    /// measures what it hears: the van, the house near and 40 m away, the radio through its walkie (and silence once
    /// it is dropped). Both chat in Thai; the client pings and sends a quick message; the host counts how often its own
    /// microphone reached the ghost. "-dev-voice client-fail" joins with voice refused and must still play the job.
    /// Every role prints one [M4C] SUMMARY line.
    /// </summary>
    public sealed class DevVoiceBot : MonoBehaviour
    {
        private const float StepTimeout = 40f;

        private string m_Role;
        private string m_CodeFile;
        private string m_ShotDir;
        private readonly System.Collections.Generic.HashSet<string> m_ShotsTaken = new();
        private float m_QuitAt;
        private string m_Failure = "";
        private bool m_Printed;
        private string m_HostVoiceId = "";

        // client measurements
        private double m_VanHeard, m_NearHeard, m_FarHeard;
        private float m_RadioPeak;
        private bool m_DroppedMuted;
        // both
        private bool m_ChatHeard;
        private bool m_JobReached;
        // host
        private bool m_PingSeen, m_QuickSeen;
        private int m_VoiceNoises, m_NoisesPerSecondMax;
        private readonly System.Collections.Generic.List<float> m_NoiseTimes = new();

        private static string Scene => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        private static MainMenuPanel Menu => FindAnyObjectByType<MainMenuPanel>();
        private static LobbyState Lobby => FindAnyObjectByType<LobbyState>();
        private static JobDirector Director => FindAnyObjectByType<JobDirector>();
        private static VoiceDirector Voice => VoiceDirector.Instance;
        private static ScriptedInput Input => PlayerMotor.Local != null ? PlayerMotor.Local.Input as ScriptedInput : null;
        private string PhaseFile => m_CodeFile + ".phase";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachFromCommandLine()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var role = Arg("-dev-voice");
            if (role == null)
                return;
            var go = new GameObject("DevVoiceBot");
            DontDestroyOnLoad(go);
            var bot = go.AddComponent<DevVoiceBot>();
            bot.m_Role = role;
            bot.m_CodeFile = Arg("-dev-codefile");
            bot.m_ShotDir = Arg("-dev-shot-dir");
            float.TryParse(Arg("-dev-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);
            bot.m_QuitAt = Time.realtimeSinceStartup + (seconds > 0f ? seconds : 180f);
#endif
        }

        private IEnumerator Start()
        {
            yield return Until(() => Menu != null && GameSession.Instance != null && Voice != null, "main menu");
            if (int.TryParse(Arg("-dev-latency"), out var latency) && latency > 0)
                GameSession.Instance.GetComponent<NetworkSimulator>().ChangeConnectionPreset(
                    NetworkSimulatorPreset.Create($"Delay {latency} ms", packetDelayMs: latency, packetJitterMs: latency / 10));
            var name = Arg("-dev-name");
            if (name != null)
                GameData.PlayerName = name;
            GameData.Settings.ghostHearsMic = true;
            GameData.Settings.pushToTalk = false;
            if (m_Role == "client-fail")
                Voice.ServiceOverride = new FakeVoiceService { FailLogin = true, FailJoin = true };
            if (m_Role == "host")
                yield return Host();
            else
                yield return Client();
            Finish();
        }

        private void Update()
        {
            if (!m_Printed && Time.realtimeSinceStartup > m_QuitAt)
            {
                Fail("out-of-time");
                Finish();
            }
        }

        // ---- host ----

        private IEnumerator Host()
        {
            Menu.ClickHost();
            yield return Until(() => Scene == GameSession.LobbyScene && GameSession.Instance.JoinCode != null, "room open");
            File.WriteAllText(m_CodeFile, GameSession.Instance.JoinCode);
            yield return Until(() => Lobby != null && Lobby.Members.Count == 2 && Voice.State == VoiceState.On, "client in the van");
            // Give the client's voice a moment to join the van channel before the host's message.
            yield return Phase("van", 4f, null);
            Chat("สวัสดี [h]");
            yield return new WaitForSeconds(6f);
            yield return Until(() => { Ready(); TryStart(); return Director != null && Director.Phase == JobPhase.Exploring; }, "job running");
            m_JobReached = Director != null;
            yield return Until(() => Voice.State == VoiceState.On && Voice.Space == VoiceSpace.House, "house voice");
            if (NoiseHub.Current != null)
                NoiseHub.Current.Heard += CountNoise;
            yield return Phase("near", 8f, null);
            // Vivox keeps a participant who walks away listed for a while: only the second part of "far" is measured.
            yield return Phase("far", 10f, () => Move(new Vector3(60f, 0f, 0f)));
            yield return Phase("far2", 6f, null);
            yield return Phase("radio", 8f, () =>
            {
                var walkies = FindObjectsByType<Walkie>(FindObjectsSortMode.InstanceID);
                var client = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != NetworkManager.ServerClientId);
                walkies[0].GetComponent<Carryable>().SetSingleCarrierForTest(NetworkManager.ServerClientId);
                walkies[1].GetComponent<Carryable>().SetSingleCarrierForTest(client);
                if (Input != null)
                    Input.Next.UseHeld = true;
            });
            yield return Phase("drop", 4f, () =>
            {
                var client = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != NetworkManager.ServerClientId);
                foreach (var walkie in FindObjectsByType<Walkie>(FindObjectsSortMode.None))
                    walkie.GetComponent<Carryable>().ReleaseCarrier(client);
            });
            if (Input != null)
                Input.Next.UseHeld = false;
            Move(Vector3.zero);
            yield return Phase("comms", 6f, null);
            m_PingSeen = CommsHub.Current != null && CommsHub.Current.Markers.Count > 0;
            m_QuickSeen = ThaiShaper.Unshape(FindAnyObjectByType<ChatPanel>().LastText).Contains(JobUi.Text("quick.ghost_here"));
            WritePhase("done");
            yield return new WaitForSeconds(3f);
        }

        private IEnumerator Phase(string name, float seconds, Action start)
        {
            WritePhase(name);
            start?.Invoke();
            Debug.Log($"[M4C] phase {name}");
            yield return new WaitForSeconds(seconds);
        }

        private void CountNoise(NoiseEvent e)
        {
            if (e.Source != NoiseSource.Voice)
                return;
            m_VoiceNoises++;
            var now = Time.time;
            m_NoiseTimes.Add(now);
            m_NoiseTimes.RemoveAll(t => now - t > 1f);
            m_NoisesPerSecondMax = Math.Max(m_NoisesPerSecondMax, m_NoiseTimes.Count);
        }

        // ---- client ----

        private IEnumerator Client()
        {
            string code = null;
            yield return Until(() => (code = ReadText(m_CodeFile)) != null, "join code");
            Menu.ClickJoin(code);
            yield return Until(() => Scene == GameSession.LobbyScene && Lobby != null && Lobby.Members.Count == 2, "in the van");
            m_HostVoiceId = Lobby.Members.First(m => m.ClientId == NetworkManager.ServerClientId).VoiceId.ToString();
            var chatted = false;
            while (Time.realtimeSinceStartup < m_QuitAt && ReadText(PhaseFile) != "done")
            {
                var phase = ReadText(PhaseFile);
                Ready();
                if (phase == "van" && !chatted && Voice.State == VoiceState.On)
                {
                    chatted = true;
                    yield return new WaitForSeconds(1f);
                    Chat("สวัสดี [c]");
                }
                if (Scene == GameSession.JobScene)
                    m_JobReached = true;
                Measure(phase);
                yield return Shots(phase);
                if (phase == "comms" && CommsHub.Current != null && PlayerMotor.Local != null && !m_SentComms)
                {
                    m_SentComms = true;
                    CommsHub.Current.Ping(PlayerMotor.Local);
                    yield return new WaitForSeconds(0.6f);
                    CommsHub.Current.SendQuick(QuickMessage.GhostHere);
                }
                yield return null;
            }
        }

        private bool m_SentComms;

        private void Measure(string phase)
        {
            var voice = Voice;
            if (voice == null || voice.State != VoiceState.On || string.IsNullOrEmpty(m_HostVoiceId))
                return;
            var host = voice.Service.Participants.Where(p => p.PlayerId == m_HostVoiceId).ToList();
            double On(string prefix) => host.Where(p => p.Channel.StartsWith(prefix)).Select(p => p.Energy).DefaultIfEmpty(0d).Max();
            switch (phase)
            {
                case "van": m_VanHeard = Math.Max(m_VanHeard, On("van-")); break;
                case "near": m_NearHeard = Math.Max(m_NearHeard, On("house-")); break;
                case "far2": m_FarHeard = Math.Max(m_FarHeard, On("house-")); break;
                case "radio":
                    var filter = FindAnyObjectByType<RadioListener>()?.FilterFor(m_HostVoiceId);
                    if (filter != null)
                        m_RadioPeak = Math.Max(m_RadioPeak, filter.TakePeak());
                    break;
                case "drop":
                    var dropped = FindAnyObjectByType<RadioListener>()?.FilterFor(m_HostVoiceId);
                    m_DroppedMuted = dropped != null && dropped.Source.mute;
                    break;
            }
        }

        /// <summary>Windowed client with -dev-shot-dir: one picture per phase for docs/screenshots/m4.</summary>
        private IEnumerator Shots(string phase)
        {
            if (m_ShotDir == null || Application.isBatchMode || phase == null || m_ShotsTaken.Contains(phase))
                yield break;
            switch (phase)
            {
                case "van" when m_ChatHeard && Voice.State == VoiceState.On:
                    m_ShotsTaken.Add(phase);
                    yield return new WaitForSeconds(2f); // our own message too
                    yield return Shot("chat-thai.png");
                    break;
                case "near":
                    m_ShotsTaken.Add(phase);
                    yield return new WaitForSeconds(2f);
                    yield return Shot("voice-hud-th.png");
                    break;
                case "radio":
                    m_ShotsTaken.Add(phase);
                    yield return new WaitForSeconds(3f);
                    yield return Shot("radio-held-th.png");
                    break;
                case "comms" when m_SentComms:
                    m_ShotsTaken.Add(phase);
                    PlayerMotor.Local.SetPitchForTest(35f);
                    CommsHub.Current.Ping(PlayerMotor.Local);
                    if (Input != null)
                        Input.Next.QuickWheelHeld = true;
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("ping-quick-th.png");
                    var wheel = FindAnyObjectByType<QuickWheel>();
                    wheel?.PointForTest(Vector2.right);
                    if (Input != null)
                        Input.Next.QuickWheelHeld = false;
                    break;
            }
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, name));
            yield return new WaitForSeconds(0.5f);
        }

        // ---- shared ----

        private void Chat(string text)
        {
            var chat = FindAnyObjectByType<ChatPanel>();
            if (chat != null)
                chat.Type(text);
        }

        private void LateUpdate()
        {
            var chat = FindAnyObjectByType<ChatPanel>();
            if (chat == null || m_ChatHeard)
                return;
            var other = m_Role == "host" ? "[c]" : "[h]";
            m_ChatHeard = ThaiShaper.Unshape(chat.LastText).Contains(other);
        }

        private float m_NextReadyAt;

        private void Ready()
        {
            var lobby = Lobby;
            if (lobby == null || !lobby.IsSpawned || Time.realtimeSinceStartup < m_NextReadyAt)
                return;
            var me = NetworkManager.Singleton.LocalClientId;
            if (lobby.Members.Any(m => m.ClientId == me && !m.Ready))
            {
                lobby.ToggleReadyRpc();
                m_NextReadyAt = Time.realtimeSinceStartup + 2f;
            }
        }

        private static void TryStart()
        {
            var lobby = Lobby;
            if (lobby != null && lobby.IsSpawned && !lobby.IsCountingDown && lobby.Members.All(m => m.Ready))
                lobby.TryStartCountdown();
        }

        private static void Move(Vector3 offset)
        {
            var motor = PlayerMotor.Local;
            if (motor == null)
                return;
            var spawn = GameObject.Find($"PlayerSpawn{motor.OwnerClientId % 6}");
            motor.Teleport((spawn != null ? spawn.transform.position : Vector3.zero) + offset, 0f);
        }

        private void WritePhase(string phase)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.WriteAllText(PhaseFile, phase);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        private static string ReadText(string path)
        {
            try
            {
                var text = path != null && File.Exists(path) ? File.ReadAllText(path).Trim() : "";
                return text.Length > 0 ? text : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private IEnumerator Until(Func<bool> condition, string step)
        {
            var end = Time.realtimeSinceStartup + StepTimeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Fail(step);
                    yield break;
                }
                yield return null;
            }
        }

        private void Fail(string step)
        {
            if (m_Failure == "")
                m_Failure = step.Replace(' ', '-');
            Debug.LogError($"[M4C] step failed: {step}");
        }

        private void Finish()
        {
            if (m_Printed)
                return;
            m_Printed = true;
            var voiceState = Voice != null ? Voice.State.ToString() : "none";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[M4C] SUMMARY role={0} voice={1} job={2} chat={3} van={4:0.00} near={5:0.00} far={6:0.00} radio={7:0.00} " +
                "droppedMuted={8} ping={9} quick={10} noises={11} noisesPerSec={12} failed={13}",
                m_Role, voiceState, m_JobReached ? "true" : "false", m_ChatHeard ? "true" : "false", m_VanHeard, m_NearHeard,
                m_FarHeard, m_RadioPeak, m_DroppedMuted ? "true" : "false", m_PingSeen ? "true" : "false",
                m_QuickSeen ? "true" : "false", m_VoiceNoises, m_NoisesPerSecondMax, m_Failure == "" ? "none" : m_Failure));
            Application.Quit();
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];
            return null;
        }
    }
}
