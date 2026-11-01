using System;
using System.Threading.Tasks;
using SpiritMovers.Core.Job;
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
        /// <summary>Shown when the network stopped under us (Relay or transport failure), host or client.</summary>
        public const string ConnectionLostKey = "session.connection_lost";
        /// <summary>The same during a job or its results, with the pay for rooms the ghost already accepted.</summary>
        public const string ConnectionLostJobKey = "session.connection_lost_job";

        private readonly UgsSessionService m_Ugs = new();
        private NetworkManager m_Network;
        private bool m_Loading;
        private bool m_Leaving;
        private bool m_Joining;
        private string m_Refusal;
        /// <summary>NGO started during the current host/join attempt, so its disconnect reason belongs to it.</summary>
        private bool m_StartedThisAttempt;
        /// <summary>We shut NGO down ourselves; the stop that follows is not a lost connection.</summary>
        private bool m_StopExpected;

        public static GameSession Instance { get; private set; }

        /// <summary>A message (localization key) and pay the main menu shows once when the player comes back to it.</summary>
        public static string PendingMessageKey { get; set; }
        public static int PendingPay { get; set; }
        /// <summary>Format values for the pending message when it is not about pay (e.g. both game versions).</summary>
        public static object[] PendingArgs { get; set; }

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
            m_Network.OnClientDisconnectCallback += OnDisconnected;
            m_Network.OnClientStarted += () =>
            {
                m_StartedThisAttempt = true;
                m_StopExpected = false;
                m_Network.SceneManager.OnSceneEvent += OnSceneEvent;
            };
            m_Network.OnClientStopped += OnStopped;
        }

        /// <summary>Host: every player has finished loading the job scene (NGO's load-complete event).</summary>
        public bool JobSceneReady { get; private set; }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneName != JobScene)
                return;
            if (sceneEvent.SceneEventType == SceneEventType.Load && sceneEvent.ClientId == m_Network.LocalClientId)
            {
                m_Loading = true;
                JobSceneReady = false;
            }
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                JobSceneReady = true;
            }
        }

        /// <summary>
        /// Client lost the host: refused on joining (job in progress, room full) or the host left / crashed — back to
        /// the menu with a message for the phase, and the pay for rooms the ghost already accepted (GDD 9).
        /// </summary>
        private void OnDisconnected(ulong clientId)
        {
            if (m_Leaving || m_Network.IsServer || clientId != m_Network.LocalClientId)
                return;
            var reason = m_Network.DisconnectReason;
            if (m_Joining)
            {
                // Still inside the join call (slow connections): it will fail, and Fail shows the host's reason.
                if (!string.IsNullOrEmpty(reason) && reason.StartsWith("error."))
                    NoteRefusal(reason);
                return;
            }
            if (!string.IsNullOrEmpty(reason) && reason.StartsWith("error."))
            {
                Debug.Log($"[Net] refused by the host: {reason}");
                (PendingMessageKey, PendingArgs) = ParseRefusal(reason);
                PendingPay = 0;
            }
            else
            {
                var phase = Phase;
                PendingMessageKey = MessageForHostLeft(phase);
                PendingArgs = null;
                PendingPay = PayForHostLeft(phase);
                // During the results this job's pay is already in the save; the same job is never paid twice.
                Save.GameData.CreditPay(PendingPay, FindAnyObjectByType<JobDirector>());
                Debug.Log($"[Net] the host left during {phase}; pay {PendingPay}");
            }
            _ = LeaveAsync();
        }

        /// <summary>
        /// NGO stopped without us asking: a Relay or transport failure shuts it down with no disconnect callback, on the
        /// host as well as a client (M4A final review). Back to the menu with a message instead of a dead scene.
        /// </summary>
        private void OnStopped(bool wasHost)
        {
            if (m_StopExpected)
            {
                m_StopExpected = false;
                return;
            }
            if (m_Leaving || PendingMessageKey != null || SceneManager.GetActiveScene().name == MainMenuScene)
                return;
            var phase = Phase;
            PendingMessageKey = phase is SessionPhase.Job or SessionPhase.Results ? ConnectionLostJobKey : ConnectionLostKey;
            PendingArgs = null;
            PendingPay = PayForHostLeft(phase);
            Save.GameData.CreditPay(PendingPay, FindAnyObjectByType<JobDirector>());
            Debug.LogWarning($"[Net] the connection stopped during {phase} ({(wasHost ? "host" : "client")}); pay {PendingPay}");
            _ = LeaveAsync();
        }

        /// <summary>Shut NGO down on purpose (leaving, a failed attempt).</summary>
        private void StopNetwork()
        {
            if (!m_Network.IsListening)
                return;
            m_StopExpected = true;
            m_Network.Shutdown();
        }

        internal void FailForTest(Exception e) => Fail(e);

        /// <summary>The host refused us while the join call was still running; Fail shows this reason.</summary>
        internal void NoteRefusal(string reason) => m_Refusal = reason;

        /// <summary>
        /// A refusal reason from the host as (localization key, format values): "error.in_job" or
        /// "error.version:HOSTVERSION" (shown with this build's version first).
        /// </summary>
        private static (string Key, object[] Args) ParseRefusal(string reason)
        {
            var colon = reason.IndexOf(':');
            if (colon < 0)
                return (reason, Array.Empty<object>());
            return (reason.Substring(0, colon), new object[] { Bootstrap.Version.ToString(), reason.Substring(colon + 1) });
        }

        internal static string MessageForHostLeft(SessionPhase phase) => phase switch
        {
            SessionPhase.Job => "session.host_left_job",
            SessionPhase.Results => "session.host_left_results",
            _ => "session.host_left_lobby",
        };

        /// <summary>What this player keeps: the synced job report if there was one, else the accepted rooms' share.</summary>
        internal static int PayForHostLeft(SessionPhase phase)
        {
            var director = FindAnyObjectByType<JobDirector>();
            if (director == null || director.Plan == null)
                return 0;
            if (director.Result.IsSet)
                return director.Result.PerPlayer;
            var basePay = director.Plan.DifficultyId == JobDifficulty.Hard.Id ? JobDifficulty.Hard.BasePay : JobDifficulty.Normal.BasePay;
            return SessionRules.HostLeftPay(phase, director.AcceptedRooms.Count, director.Plan.Rooms.Count, basePay,
                director.Plan.PlayerCount);
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
            m_StartedThisAttempt = false;
            try
            {
                await Service.InitializeAsync(Save.DataLocation.DevProfile);
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
            m_Joining = true;
            m_Refusal = null;
            m_StartedThisAttempt = false;
            SetVersionPayload();
            try
            {
                await Service.InitializeAsync(Save.DataLocation.DevProfile);
                await Service.JoinAsync(code, Bootstrap.Version);
                JoinCode = code;
                IsSolo = false;
            }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                m_Joining = false;
            }
        }

        /// <summary>Play alone: a host on this machine only (no internet, no join code), then the same van and jobs.</summary>
        public void StartSolo()
        {
            SetVersionPayload();
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
            m_Leaving = true;
            StopNetwork();
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
            m_Leaving = false;
            SceneManager.LoadScene(MainMenuScene);
        }

        /// <summary>Our game version goes with the connection request; the host's LobbyGate checks it (TECHNICAL_DESIGN 7).</summary>
        private void SetVersionPayload() =>
            m_Network.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(Bootstrap.Version.ToString());

        /// <summary>Host: the lobby countdown ended and the job scene is about to load.</summary>
        public void MarkLoading() => m_Loading = true;

        private void LoadLobby() => m_Network.SceneManager.LoadScene(LobbyScene, LoadSceneMode.Single);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != JobScene)
                m_Loading = false;
            // Host of an online room: closed to newcomers while on a job, open again in the van (GDD 9).
            if (m_Network.IsServer && !IsSolo && JoinCode != null && (scene.name == JobScene || scene.name == LobbyScene))
                SetJoinable(scene.name == LobbyScene);
        }

        private async void SetJoinable(bool joinable)
        {
            try
            {
                await Service.SetJoinableAsync(joinable);
            }
            catch (Exception e)
            {
                // Not fatal: LobbyGate still refuses newcomers during a job.
                Debug.LogWarning($"[Net] could not {(joinable ? "open" : "lock")} the session: {e.Message}");
            }
        }

        private void Fail(Exception e)
        {
            Debug.LogWarning($"[Net] session failed: {e.GetType().Name}: {e.Message}");
            StopNetwork();
            // A refusal by the host says more than the SDK's generic join error (final-pass finding, 150 ms run).
            // NGO keeps its last reason until it starts again, so only this attempt's counts (M4A final review).
            var reason = m_Refusal ?? (m_StartedThisAttempt ? m_Network.DisconnectReason : null);
            m_Refusal = null;
            var (key, args) = !string.IsNullOrEmpty(reason) && reason.StartsWith("error.")
                ? ParseRefusal(reason)
                : (SessionErrors.KeyFor(e), SessionErrors.ArgsFor(e));
            if (SceneManager.GetActiveScene().name == MainMenuScene)
            {
                Failed?.Invoke(key, args);
                return;
            }
            // The host's scene had already synced (dead van): back to the menu with the message.
            PendingMessageKey = key;
            PendingArgs = args;
            PendingPay = 0;
            IsSolo = false;
            JoinCode = null;
            m_Loading = false;
            SceneManager.LoadScene(MainMenuScene);
        }

    }
}
