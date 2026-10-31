using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using SpiritMovers.Core;
using SpiritMovers.Net;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Developer harness for test scenes (M3): start a local host for tests, or host / join a real
    /// Relay session by code. Not part of the shipped game flow (the Lobby comes in M4).
    /// Headless automation (command line):
    ///   -dev-role host|client  -dev-profile NAME  -dev-codefile PATH  -dev-seconds S
    ///   -dev-scenario NAME (see <see cref="DevBot"/>; "job" = <see cref="DevJobBot"/> with -dev-seed N -dev-players N;
    ///   "ghost" = <see cref="DevGhostBot"/> with -dev-seed N -dev-players N -dev-peek-shot PNG -dev-hunt-shot PNG
    ///   -dev-sheet-shot PNG)  -dev-screenshot PNG  -dev-latency MS (simulated, each way)
    /// </summary>
    public sealed class DevSessionRunner : MonoBehaviour
    {
        private const ushort LocalPort = 7777;
        private const int MaxPlayers = 6;

        private readonly UgsSessionService m_Sessions = new();
        private string m_Status = "Not connected";
        private string m_JoinCode = "";
        private bool m_Busy;
        private float m_QuitAt;
        private string m_Role;
        private DevBot m_Bot;
        private DevJobBot m_JobBot;
        private DevGhostBot m_GhostBot;
        private bool m_Finished;

        public string SessionCode { get; private set; }

        private static NetworkManager Manager => NetworkManager.Singleton != null ? NetworkManager.Singleton : FindAnyObjectByType<NetworkManager>();

        /// <summary>Host on this machine only (tests); no Unity Gaming Services.</summary>
        public void StartLocalHost()
        {
            var manager = Manager;
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", LocalPort, "127.0.0.1");
            if (!manager.StartHost())
                throw new InvalidOperationException("Local host failed to start");
            m_Status = "Local host";
        }

        public async Task HostRelayAsync(string profile = null)
        {
            await m_Sessions.InitializeAsync(profile);
            SessionCode = await m_Sessions.HostAsync(MaxPlayers, CurrentVersion);
            m_Status = $"Hosting — code {SessionCode}";
        }

        public async Task JoinRelayAsync(string code, string profile = null)
        {
            await m_Sessions.InitializeAsync(profile);
            await m_Sessions.JoinAsync(code, CurrentVersion);
            SessionCode = code.Trim().ToUpperInvariant();
            m_Status = $"Joined — code {SessionCode}";
        }

        public Task LeaveAsync() => m_Sessions.LeaveAsync();

        private void Start()
        {
            m_Role = Arg("-dev-role");
            if (m_Role == null)
                return;
            float.TryParse(Arg("-dev-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out m_QuitAt);
            if (int.TryParse(Arg("-dev-latency"), out var latency) && latency > 0)
            {
                FindAnyObjectByType<NetworkSimulator>().ChangeConnectionPreset(
                    NetworkSimulatorPreset.Create($"Delay {latency} ms", packetDelayMs: latency, packetJitterMs: latency / 10));
                Debug.Log($"[M3B] simulated latency {latency} ms");
            }
            Run(AutomateAsync);
        }

        private async Task AutomateAsync()
        {
            var profile = Arg("-dev-profile");
            var codeFile = Arg("-dev-codefile");
            if (m_Role == "host")
            {
                await HostRelayAsync(profile);
                if (codeFile != null)
                    File.WriteAllText(codeFile, SessionCode);
            }
            else
            {
                var deadline = Time.realtimeSinceStartup + 90f;
                while (codeFile == null || !File.Exists(codeFile) || new FileInfo(codeFile).Length == 0)
                {
                    if (Time.realtimeSinceStartup > deadline)
                        throw new TimeoutException($"No join code in {codeFile}");
                    await Task.Delay(500);
                }
                await JoinRelayAsync(File.ReadAllText(codeFile), profile);
            }

            while (!Manager.IsServer && !Manager.IsConnectedClient)
                await Task.Yield();
            var scenario = Arg("-dev-scenario");
            if (scenario == "job")
            {
                int.TryParse(Arg("-dev-seed"), out var seed);
                int.TryParse(Arg("-dev-players"), out var players);
                m_JobBot = DevJobBot.Attach(gameObject, m_Role == "host", seed, Mathf.Max(1, players), Arg("-dev-sheet-shot"));
            }
            else if (scenario == "ghost")
            {
                int.TryParse(Arg("-dev-seed"), out var seed);
                int.TryParse(Arg("-dev-players"), out var players);
                m_GhostBot = DevGhostBot.Attach(gameObject, m_Role == "host", seed, Mathf.Max(2, players), Arg("-dev-peek-shot"),
                    Arg("-dev-hunt-shot"));
            }
            else if (scenario != null)
            {
                m_Bot = DevBot.Attach(gameObject, scenario, m_Role == "host");
            }
        }

        private void Update()
        {
            if (m_Role == null || m_Finished)
                return;

            var screenshot = Arg("-dev-screenshot");
            if (screenshot != null && m_QuitAt > 0f && Time.realtimeSinceStartup >= m_QuitAt - 5f && !File.Exists(screenshot))
                ScreenCapture.CaptureScreenshot(screenshot);

            // The disconnect scenario's client closes the game abruptly while carrying.
            var abruptQuit = m_Bot != null && m_Bot.Done && m_Bot.Scenario == "disconnect" && m_Role == "client";
            if (abruptQuit || (m_QuitAt > 0f && Time.realtimeSinceStartup >= m_QuitAt))
            {
                m_Finished = true;
                LogSummary();
                if (abruptQuit)
                    Application.Quit();
                else
                    StartCoroutine(LeaveAndQuit());
            }
        }

        private void LogSummary()
        {
            if (m_JobBot != null)
            {
                Debug.Log(m_JobBot.Summary(m_Role));
                return;
            }
            if (m_GhostBot != null)
            {
                Debug.Log(m_GhostBot.Summary(m_Role));
                return;
            }
            if (m_Bot == null || m_Bot.Item == null)
            {
                Debug.Log($"[M3B] SUMMARY role={m_Role} scenario={Arg("-dev-scenario")} ok=false reason=no-bot");
                return;
            }
            var item = m_Bot.Item.transform.position;
            var moved = Vector3.Distance(m_Bot.ItemStart, item);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[M3B] SUMMARY role={0} scenario={1} item={2} pickups={3} carrierCountMax={4} forceDrops={5} " +
                "itemPos=({6:F3},{7:F3},{8:F3}) moved={9:F2} rotateSeen={10} handoffOk={11} reCarried={12} ok={13}",
                m_Role, m_Bot.Scenario, m_Bot.Item.name, m_Bot.Pickups, m_Bot.CarrierCountMax, m_Bot.ForceDrops,
                item.x, item.y, item.z, moved, m_Bot.RotateSeen, m_Bot.HandoffOk, m_Bot.ReCarried, m_Bot.Passed() ? "true" : "false"));
        }

        private IEnumerator LeaveAndQuit()
        {
            var leave = LeaveAsync();
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!leave.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Application.Quit();
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static GameVersion CurrentVersion =>
            GameVersion.TryParse(Application.version, out var version) ? version : new GameVersion(0, 0, 0);

        private async void Run(Func<Task> action)
        {
            if (m_Busy)
                return;
            m_Busy = true;
            try { await action(); }
            catch (Exception e) { m_Status = $"Error: {e.Message}"; Debug.LogException(e); }
            finally { m_Busy = false; }
        }

        private void OnGUI()
        {
            if (Application.isBatchMode)
                return;
            GUILayout.BeginArea(new Rect(12, 12, 320, 140), GUI.skin.box);
            GUILayout.Label($"Spirit Movers — M3 test room v{Application.version}");
            GUILayout.Label(m_Status);
            if (!Manager.IsListening && !m_Busy)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host (Relay)"))
                    Run(() => HostRelayAsync());
                if (GUILayout.Button("Local host"))
                    StartLocalHost();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                m_JoinCode = GUILayout.TextField(m_JoinCode, 12, GUILayout.Width(140));
                if (GUILayout.Button("Join") && m_JoinCode.Trim().Length > 0)
                    Run(() => JoinRelayAsync(m_JoinCode));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }
}
