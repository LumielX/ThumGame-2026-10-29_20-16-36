using System;
using System.Threading.Tasks;
using SpiritMovers.Core;
using SpiritMovers.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Developer harness for test scenes (M3): start a local host for tests, or host / join a real
    /// Relay session by code. Not part of the shipped game flow (the Lobby comes in M4).
    /// </summary>
    public sealed class DevSessionRunner : MonoBehaviour
    {
        private const ushort LocalPort = 7777;
        private const int MaxPlayers = 6;

        private readonly UgsSessionService m_Sessions = new();
        private string m_Status = "Not connected";
        private string m_JoinCode = "";
        private bool m_Busy;

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
