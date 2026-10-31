using System;
using System.Threading.Tasks;
using SpiritMovers.Core.Session;
using SpiritMovers.Job;
using SpiritMovers.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiritMovers.Session
{
    /// <summary>
    /// The one network session of the game (GDD 9, TECHNICAL_DESIGN 3/7): lives on the persistent GameNetwork object
    /// next to the NetworkManager, hosts / joins / plays solo and moves the team between the production scenes
    /// MainMenu → Lobby → Job_Greybox with NGO scene management (the host loads, clients follow).
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class GameSession : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string LobbyScene = "Lobby";
        public const string JobScene = "Job_Greybox";
        private const ushort SoloPort = 7777;

        private readonly UgsSessionService m_Ugs = new();
        private NetworkManager m_Network;
        private bool m_Loading;

        public static GameSession Instance { get; private set; }

        /// <summary>A message (localization key) and pay the main menu shows once when the player comes back to it.</summary>
        public static string PendingMessageKey { get; set; }
        public static int PendingPay { get; set; }

        /// <summary>Tests replace the online service so no internet is needed.</summary>
        public ISessionService ServiceOverride { get; set; }

        public NetworkManager Network => m_Network;
        public string JoinCode { get; private set; }
        public bool IsSolo { get; private set; }

        /// <summary>
        /// Where the team is, worked out on every machine from the scene it is in (all machines are in the same scene):
        /// the lobby (or loading, once the countdown ended), the job, or its results.
        /// </summary>
        public SessionPhase Phase
        {
            get
            {
                if (SceneManager.GetActiveScene().name == JobScene)
                {
                    var director = FindAnyObjectByType<JobDirector>();
                    return director != null && director.Result.IsSet ? SessionPhase.Results : SessionPhase.Job;
                }
                return m_Loading ? SessionPhase.Loading : SessionPhase.Lobby;
            }
        }

        /// <summary>A session action failed; the argument is the localization key to show, then its format values.</summary>
        public event Action<string, object[]> Failed;

        private ISessionService Service => ServiceOverride ?? m_Ugs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            m_Network = GetComponent<NetworkManager>();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        /// <summary>Create an online room: on success this machine hosts and the team goes to the van.</summary>
        public async Task HostAsync()
        {
            try
            {
                await Service.InitializeAsync(Profile);
                JoinCode = await Service.HostAsync(SessionRules.MaxPlayers, Bootstrap.Version);
                IsSolo = false;
                LoadLobby();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Join a room by its code; the host's scene follows automatically.</summary>
        public async Task JoinAsync(string typedCode)
        {
            var code = SessionRules.NormalizeCode(typedCode);
            if (code == null)
            {
                Failed?.Invoke(SessionErrors.Code, Array.Empty<object>());
                return;
            }
            try
            {
                await Service.InitializeAsync(Profile);
                await Service.JoinAsync(code, Bootstrap.Version);
                JoinCode = code;
                IsSolo = false;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Play alone: a host on this machine only (no internet, no join code), then the same van and jobs.</summary>
        public void StartSolo()
        {
            m_Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", SoloPort, "127.0.0.1");
            if (!m_Network.StartHost())
            {
                Failed?.Invoke(SessionErrors.Join, Array.Empty<object>());
                return;
            }
            IsSolo = true;
            JoinCode = null;
            LoadLobby();
        }

        /// <summary>Leave the room (or stop the solo game) and go back to the main menu.</summary>
        public async Task LeaveAsync()
        {
            var online = !IsSolo && JoinCode != null;
            m_Network.Shutdown();
            IsSolo = false;
            JoinCode = null;
            m_Loading = false;
            if (online)
            {
                try
                {
                    await Service.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Net] leaving the session failed: {e.Message}");
                }
            }
            SceneManager.LoadScene(MainMenuScene);
        }

        /// <summary>Host: the lobby countdown ended and the job scene is about to load.</summary>
        public void MarkLoading() => m_Loading = true;

        private void LoadLobby() => m_Network.SceneManager.LoadScene(LobbyScene, LoadSceneMode.Single);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != JobScene)
                m_Loading = false;
        }

        private void Fail(Exception e)
        {
            Debug.LogWarning($"[Net] session failed: {e.GetType().Name}: {e.Message}");
            if (m_Network.IsListening)
                m_Network.Shutdown();
            Failed?.Invoke(SessionErrors.KeyFor(e), SessionErrors.ArgsFor(e));
        }

        /// <summary>Several game instances on one machine sign in as different players (dev runs only).</summary>
        private static string Profile
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-dev-profile")
                        return args[i + 1];
                return null;
            }
        }
    }
}
