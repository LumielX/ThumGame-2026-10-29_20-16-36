using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SpiritMovers.Core;
using SpiritMovers.Net;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SpiritMovers.Prototypes.NetSync
{
    /// <summary>
    /// Throwaway M2 network prototype (PROJECT_PLAN M2 item 3).
    /// Interactive: Host / Join-by-code UI, E to carry, M to switch carry mode, latency buttons.
    /// Headless automation (command line):
    ///   -proto-role host|client  -proto-profile NAME  -proto-codefile PATH
    ///   -proto-props N  -proto-seconds S  -proto-carry A|B  -proto-latency MS
    /// </summary>
    public sealed class NetSyncProtoRunner : MonoBehaviour
    {
        [SerializeField] private GameObject m_PlayerPrefab;
        [SerializeField] private GameObject m_PropPrefab;
        [SerializeField] private int m_PropCount = 40;

        private const float MetricsInterval = 5f;
        private const int MaxPlayers = 6;

        private readonly UgsSessionService m_Sessions = new();
        private TransportTrafficMeter m_Meter;
        private NetworkSimulator m_Simulator;
        private GameVersion m_Version;

        private string m_Role;
        private string m_Profile;
        private string m_CodeFile;
        private float m_AutoQuitSeconds;
        private int m_LatencyMs;

        private string m_Status = "Not connected";
        private string m_JoinCodeInput = "";
        private string m_SessionCode = "";
        private double m_LastKbps;
        private double m_KbpsSum;
        private int m_KbpsSamples;
        private double m_CarryErrorSum;
        private int m_CarryErrorSamples;
        private bool m_Busy;

        private void Awake()
        {
            m_Version = GameVersion.TryParse(Application.version, out var v) ? v : new GameVersion(0, 0, 0);
            m_Role = Arg("-proto-role");
            m_Profile = Arg("-proto-profile");
            m_CodeFile = Arg("-proto-codefile");
            if (int.TryParse(Arg("-proto-props"), out var props)) m_PropCount = props;
            float.TryParse(Arg("-proto-seconds"), out m_AutoQuitSeconds);
            int.TryParse(Arg("-proto-latency"), out m_LatencyMs);
            ProtoPlayer.PreferredCarryMode = Arg("-proto-carry") == "A" ? CarryMode.HostSimulated : CarryMode.OwnerTransfer;
            ProtoPlayer.BotMode = m_Role == "client" || m_Role == "host";

            var manager = NetworkManager.Singleton;
            manager.NetworkConfig.PlayerPrefab = m_PlayerPrefab;
            manager.AddNetworkPrefab(m_PlayerPrefab);
            manager.AddNetworkPrefab(m_PropPrefab);
            manager.OnClientConnectedCallback += id => Debug.Log($"[Proto] client connected id={id} (local={manager.LocalClientId})");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[Proto] client disconnected id={id} reason='{manager.DisconnectReason}'");

            m_Simulator = FindAnyObjectByType<NetworkSimulator>();
        }

        private IEnumerator Start()
        {
            yield return null;
            SetLatency(m_LatencyMs);

            if (m_Role == "host")
                RunAsync(HostAsync);
            else if (m_Role == "client")
                RunAsync(JoinFromFileAsync);

            var next = Time.realtimeSinceStartup + MetricsInterval;
            while (true)
            {
                if (Time.realtimeSinceStartup >= next)
                {
                    next += MetricsInterval;
                    LogMetrics();
                }

                if (m_AutoQuitSeconds > 0 && Time.realtimeSinceStartup >= m_AutoQuitSeconds)
                {
                    LogSummary();
                    RunAsync(QuitAsync);
                    yield break;
                }
                yield return null;
            }
        }

        private void FixedUpdate()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
                return;

            SampleCarryError(manager);

            // Host keeps some props moving so the test measures realistic physics traffic.
            if (manager.IsServer && Time.frameCount % 50 == 0)
            {
                var props = FindObjectsByType<ProtoProp>(FindObjectsSortMode.None).Where(p => !p.IsCarried).ToArray();
                for (var i = 0; i < Mathf.Min(5, props.Length); i++)
                    props[Random.Range(0, props.Length)].Body.AddForce(Random.insideUnitSphere * 3f + Vector3.up * 3f, ForceMode.Impulse);
            }

            // Bot carrier: pick up the nearest prop shortly after connecting.
            var local = ProtoPlayer.Local;
            if (ProtoPlayer.BotMode && m_Role == "client" && local != null && local.CarriedProp == null &&
                manager.ServerTime.Time > 3 && Time.frameCount % 100 == 0)
                local.CarryNearest();
        }

        private void SampleCarryError(NetworkManager manager)
        {
            if (!ProtoPlayer.BotMode)
                return;
            foreach (var prop in FindObjectsByType<ProtoProp>(FindObjectsSortMode.None))
            {
                if (!prop.IsCarried || prop.Carrier == NetworkManager.ServerClientId)
                    continue;
                ProtoPlayer.BotPose(manager.ServerTime.Time, out var position, out var forward);
                var expected = ProtoPlayer.HoldPointFor(position, forward);
                m_CarryErrorSum += Vector3.Distance(prop.transform.position, expected);
                m_CarryErrorSamples++;
            }
        }

        private async Task HostAsync()
        {
            await m_Sessions.InitializeAsync(m_Profile);
            m_SessionCode = await m_Sessions.HostAsync(MaxPlayers, m_Version);
            m_Status = $"Hosting — code {m_SessionCode}";
            if (!string.IsNullOrEmpty(m_CodeFile))
                File.WriteAllText(m_CodeFile, m_SessionCode);

            var manager = NetworkManager.Singleton;
            while (!manager.IsServer || !manager.IsListening)
                await Task.Yield();

            StartMeter(manager);
            SpawnProps();
        }

        private async Task JoinFromFileAsync()
        {
            var deadline = Time.realtimeSinceStartup + 90f;
            while (!File.Exists(m_CodeFile) || new FileInfo(m_CodeFile).Length == 0)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"No join code in {m_CodeFile}");
                await Task.Delay(500);
            }
            await JoinAsync(File.ReadAllText(m_CodeFile));
        }

        private async Task JoinAsync(string code)
        {
            await m_Sessions.InitializeAsync(m_Profile);
            await m_Sessions.JoinAsync(code, m_Version);
            m_SessionCode = code.Trim().ToUpperInvariant();

            var manager = NetworkManager.Singleton;
            while (!manager.IsConnectedClient)
                await Task.Yield();
            m_Status = $"Joined — code {m_SessionCode}";
            StartMeter(manager);
        }

        private void StartMeter(NetworkManager manager)
        {
            m_Meter ??= new TransportTrafficMeter(manager.NetworkConfig.NetworkTransport, Time.realtimeSinceStartupAsDouble);
        }

        private void SpawnProps()
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(m_PropCount));
            for (var i = 0; i < m_PropCount; i++)
            {
                var x = (i % columns - columns / 2f) * 1.4f;
                var z = (i / columns - columns / 2f) * 1.4f;
                var prop = Instantiate(m_PropPrefab, new Vector3(x, 0.5f + (i % 3) * 0.6f, z), Quaternion.identity);
                prop.GetComponent<NetworkObject>().Spawn();
            }
            Debug.Log($"[Proto] spawned {m_PropCount} props");
        }

        private void SetLatency(int ms)
        {
            m_LatencyMs = ms;
            if (m_Simulator == null)
                return;
            m_Simulator.ChangeConnectionPreset(ms <= 0
                ? NetworkSimulatorPresets.None
                : NetworkSimulatorPreset.Create($"Delay {ms} ms", packetDelayMs: ms, packetJitterMs: ms / 10));
            Debug.Log($"[Proto] simulated latency {ms} ms");
        }

        private void LogMetrics()
        {
            var manager = NetworkManager.Singleton;
            if (m_Meter != null)
            {
                m_LastKbps = m_Meter.SampleKilobytesPerSecond(Time.realtimeSinceStartupAsDouble);
                m_KbpsSum += m_LastKbps;
                m_KbpsSamples++;
            }
            var props = FindObjectsByType<ProtoProp>(FindObjectsSortMode.None);
            var carryError = m_CarryErrorSamples > 0 ? m_CarryErrorSum / m_CarryErrorSamples : 0;
            Debug.Log($"[Proto] t={Time.realtimeSinceStartup:F0}s role={m_Role ?? "ui"} server={manager.IsServer} " +
                      $"connected={(manager.IsServer ? manager.ConnectedClientsIds.Count : manager.IsConnectedClient ? 1 : 0)} " +
                      $"props={props.Length} carried={props.Count(p => p.IsCarried)} rxKBps={m_LastKbps:F1} " +
                      $"carryErrAvg={carryError:F3}m mode={ProtoPlayer.PreferredCarryMode} latency={m_LatencyMs}ms");
        }

        private void LogSummary()
        {
            var props = FindObjectsByType<ProtoProp>(FindObjectsSortMode.None).Where(p => !p.IsCarried).ToArray();
            var checksum = props.Aggregate(Vector3.zero, (sum, p) => sum + p.transform.position);
            var avgKbps = m_KbpsSamples > 0 ? m_KbpsSum / m_KbpsSamples : 0;
            var carryError = m_CarryErrorSamples > 0 ? m_CarryErrorSum / m_CarryErrorSamples : 0;
            Debug.Log($"[Proto] SUMMARY role={m_Role} rxTotalKB={(m_Meter?.TotalBytesReceived ?? 0) / 1024.0:F1} " +
                      $"rxAvgKBps={avgKbps:F1} carryErrAvg={carryError:F3}m samples={m_CarryErrorSamples} " +
                      $"freeProps={props.Length} posChecksum=({checksum.x:F1},{checksum.y:F1},{checksum.z:F1})");
        }

        private async Task QuitAsync()
        {
            try { await m_Sessions.LeaveAsync(); }
            finally { Application.Quit(); }
        }

        private async void RunAsync(Func<Task> action)
        {
            if (m_Busy)
                return;
            m_Busy = true;
            try { await action(); }
            catch (VersionMismatchException e) { m_Status = e.Message; Debug.LogError($"[Proto] {e.Message}"); }
            catch (Exception e) { m_Status = $"Error: {e.Message}"; Debug.LogException(e); }
            finally { m_Busy = false; }
        }

        private void OnGUI()
        {
            if (Application.isBatchMode)
                return;

            GUILayout.BeginArea(new Rect(12, 12, 360, 320), GUI.skin.box);
            GUILayout.Label($"Spirit Movers — network prototype v{m_Version}");
            GUILayout.Label(m_Status);
            var manager = NetworkManager.Singleton;
            if (!manager.IsListening && !m_Busy)
            {
                if (GUILayout.Button("Host"))
                    RunAsync(HostAsync);
                GUILayout.BeginHorizontal();
                m_JoinCodeInput = GUILayout.TextField(m_JoinCodeInput, 12, GUILayout.Width(160));
                if (GUILayout.Button("Join") && m_JoinCodeInput.Trim().Length > 0)
                    RunAsync(() => JoinAsync(m_JoinCodeInput));
                GUILayout.EndHorizontal();
            }
            else if (manager.IsListening)
            {
                GUILayout.Label($"Code: {m_SessionCode}   Players: {(manager.IsServer ? manager.ConnectedClientsIds.Count.ToString() : "-")}");
                GUILayout.Label($"Received: {m_LastKbps:F1} KB/s");
                if (GUILayout.Button($"Carry mode: {ProtoPlayer.PreferredCarryMode} (M)"))
                    ToggleMode();
                GUILayout.BeginHorizontal();
                foreach (var ms in new[] { 0, 50, 100, 200 })
                    if (GUILayout.Button(ms == m_LatencyMs ? $"[{ms}ms]" : $"{ms}ms"))
                        SetLatency(ms);
                GUILayout.EndHorizontal();
                GUILayout.Label("WASD move · mouse look · E carry/drop · Esc cursor");
            }
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.M)
                ToggleMode();
        }

        private static void ToggleMode() =>
            ProtoPlayer.PreferredCarryMode = ProtoPlayer.PreferredCarryMode == CarryMode.OwnerTransfer
                ? CarryMode.HostSimulated
                : CarryMode.OwnerTransfer;

        private void OnDestroy() => m_Meter?.Dispose();

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
