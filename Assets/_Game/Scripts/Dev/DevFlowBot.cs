using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Session;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Bot for the M4A session-flow test (Tools/m4a-flow-test.ps1), driving the real menu, van and job scenes of a
    /// Development build. Roles (-dev-flow): "host" creates a room, runs two jobs and quits in the middle of the second;
    /// "client" joins by code (read from -dev-codefile), keeps ready in the van and ends on the main menu with the
    /// host-left message; "late" tries to join while the first job runs and must be refused; "host-idle" just keeps a
    /// room open (version-mismatch run). Every role prints one [M4A] SUMMARY line and quits by -dev-seconds.
    /// </summary>
    public sealed class DevFlowBot : MonoBehaviour
    {
        private const float StepTimeout = 30f;

        private readonly List<string> m_Scenes = new();
        private string m_Role;
        private string m_CodeFile;
        private int m_Players;
        private float m_QuitAt;
        private int m_Jobs;
        private string m_Refused = "none";
        private string m_Name;
        /// <summary>Saved money when the bot started and this player's pay from job 1's report (M4B).</summary>
        private int m_MoneyBefore = -1;
        private int m_JobPay = -1;
        /// <summary>The names on the van board, last seen (M4B).</summary>
        private string m_Names = "none";
        private string m_Message = "none";
        private int m_Pay = -1;
        private string m_Failure = "";
        private bool m_Done;
        private bool m_Printed;
        private string m_ShotDir;
        private float m_NextReadyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachFromCommandLine()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var role = Arg("-dev-flow");
            if (role == null)
                return;
            var go = new GameObject("DevFlowBot");
            DontDestroyOnLoad(go);
            var bot = go.AddComponent<DevFlowBot>();
            bot.m_Role = role;
            bot.m_CodeFile = Arg("-dev-codefile");
            bot.m_ShotDir = Arg("-dev-shot-dir");
            bot.m_Name = Arg("-dev-name");
            int.TryParse(Arg("-dev-players"), out bot.m_Players);
            float.TryParse(Arg("-dev-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);
            bot.m_QuitAt = Time.realtimeSinceStartup + (seconds > 0f ? seconds : 120f);
#endif
        }

        private bool Failed => m_Failure != "";

        private void UseName()
        {
            if (m_Name != null)
                Save.GameData.PlayerName = m_Name;
        }

        private void LateUpdate()
        {
            var director = Director;
            if (m_JobPay < 0 && director != null && director.Result.IsSet)
                m_JobPay = director.Result.PerPlayer;
            var lobby = Lobby;
            if (lobby != null && lobby.IsSpawned && lobby.Members.Count > 0)
                m_Names = string.Join(",", lobby.Members.OrderBy(m => m.Number).Select(m => m.Name.ToString()));
        }
        private static string Scene => SceneManager.GetActiveScene().name;
        private static JobDirector Director => FindAnyObjectByType<JobDirector>();
        private static LobbyState Lobby => FindAnyObjectByType<LobbyState>();
        private static MainMenuPanel Menu => FindAnyObjectByType<MainMenuPanel>();

        private void Update()
        {
            if (m_Scenes.Count == 0 || m_Scenes[^1] != Scene)
                m_Scenes.Add(Scene);
            if (!m_Printed && Time.realtimeSinceStartup >= m_QuitAt)
                Finish();
        }

        private IEnumerator Start()
        {
            yield return Until(() => Menu != null && GameSession.Instance != null, "main menu");
            if (Failed)
                yield break;
            m_MoneyBefore = Save.GameData.Money;
            if (int.TryParse(Arg("-dev-latency"), out var latency) && latency > 0)
            {
                GameSession.Instance.GetComponent<NetworkSimulator>().ChangeConnectionPreset(
                    NetworkSimulatorPreset.Create($"Delay {latency} ms", packetDelayMs: latency, packetJitterMs: latency / 10));
                Debug.Log($"[M4A] simulated latency {latency} ms");
            }
            switch (m_Role)
            {
                case "host":
                    yield return Host();
                    break;
                case "host-idle":
                    yield return HostIdle();
                    break;
                case "client":
                    yield return Client();
                    break;
                case "late":
                    yield return Late();
                    break;
                case "settings":
                    yield return SettingsCheck();
                    break;
            }
        }

        // ---- host ----

        private IEnumerator Host()
        {
            yield return OpenRoom();
            for (var job = 1; job <= 2 && !Failed; job++)
            {
                yield return Until(() => Lobby != null && Lobby.IsSpawned && Lobby.Members.Count == m_Players, "everyone in the van");
                yield return KeepReady();
                // Start (and restart, if a late "ready" flip cancelled it) until the job runs.
                yield return Until(() =>
                {
                    var lobby = Lobby;
                    if (lobby != null && lobby.IsSpawned && !lobby.IsCountingDown && lobby.Members.All(m => m.Ready))
                        lobby.TryStartCountdown();
                    return Director != null && Director.Phase == JobPhase.Exploring;
                }, "job running");
                if (Failed)
                    yield break;
                m_Jobs++;
                if (job == 1 && m_CodeFile != null)
                    File.WriteAllText(m_CodeFile + ".job", "running");
                yield return AcceptFirstRoom();
                if (job == 1)
                {
                    yield return MeetAtVan();
                    var van = FindAnyObjectByType<VanExit>();
                    yield return Until(() => van.EveryoneAtVan(), "everyone at the van");
                    van.Interact(PlayerMotor.Local.GetComponent<PlayerCarrier>());
                    yield return Until(() => Director.Result.IsSet, "results");
                    yield return new WaitForSeconds(1f);
                    yield return Until(() => Scene == GameSession.LobbyScene, "back in the van");
                }
            }
            if (Failed)
                yield break;
            // Job 2: the host walks out mid-job (crash-like) — the clients must land on the menu with their pay.
            m_Done = true;
            yield return new WaitForSeconds(2f);
            Finish();
        }

        private IEnumerator HostIdle()
        {
            yield return OpenRoom();
            yield return Until(() => Lobby != null && Lobby.IsSpawned, "van");
            m_Done = true; // passes if nobody ever joins (checked in the summary)
        }

        private IEnumerator OpenRoom()
        {
            UseName();
            Menu.ClickHost();
            yield return Until(() => Scene == GameSession.LobbyScene && GameSession.Instance.JoinCode != null, "room open");
            if (!Failed && m_CodeFile != null)
                File.WriteAllText(m_CodeFile, GameSession.Instance.JoinCode);
        }

        private IEnumerator AcceptFirstRoom()
        {
            var room = Director.Plan.Rooms[0];
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            foreach (var task in room.Slots)
            {
                Director.DevPutBackRpc(task.Slot.Id);
                yield return new WaitForSeconds(0.2f);
            }
#endif
            yield return Until(() => Director.AcceptedRooms.Contains(room.RoomId), "first room accepted");
        }

        // ---- clients ----

        private IEnumerator Client()
        {
            yield return Shot("main-menu-th.png");
            yield return JoinRoom();
            var vanShotTaken = false;
            while (!Failed && Time.realtimeSinceStartup < m_QuitAt - 3f)
            {
                if (Scene == GameSession.LobbyScene)
                {
                    yield return KeepReady();
                    if (!vanShotTaken && Lobby != null && Lobby.Members.Count == m_Players && Lobby.Members.All(m => m.Ready))
                    {
                        vanShotTaken = true;
                        yield return Shot("lobby-th.png");
                    }
                }
                else if (Scene == GameSession.JobScene)
                {
                    if (m_Scenes.Count(s => s == GameSession.JobScene) > m_Jobs)
                        m_Jobs++;
                    var director = Director;
                    if (director != null && director.Plan != null && director.AcceptedRooms.Contains(director.Plan.Rooms[0].RoomId))
                        yield return MeetAtVan();
                }
                else if (Scene == GameSession.MainMenuScene && m_Scenes.Count > 2)
                {
                    yield return ReadMenuMessage();
                    yield return Shot("host-left-th.png");
                    m_Done = true;
                    yield return new WaitForSeconds(1f);
                    Finish();
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        private IEnumerator Late()
        {
            yield return Until(() => m_CodeFile != null && File.Exists(m_CodeFile + ".job"), "the team on a job");
            yield return JoinRoom();
        }

        private IEnumerator JoinRoom()
        {
            string code = null;
            yield return Until(() => (code = TryReadCode()) != null, "join code");
            if (Failed)
                yield break;
            UseName();
            Menu.ClickJoin(code);
            if (m_Role == "late" || Arg("-dev-version") != null)
            {
                // Expect a refusal: the menu comes back (or stays) with an error message.
                yield return Until(() => Menu != null && Menu.StatusKey != null && Menu.StatusKey.StartsWith("error."), "refusal");
                if (!Failed)
                    m_Refused = Menu.StatusKey;
                m_Done = true;
                Finish();
                yield break;
            }
            yield return Until(() => Scene == GameSession.LobbyScene && PlayerMotor.Local != null, "in the van");
        }

        private IEnumerator ReadMenuMessage()
        {
            yield return Until(() => Menu != null && Menu.StatusKey != null, "menu message");
            if (Failed)
                yield break;
            m_Message = Menu.StatusKey;
            var digits = Regex.Matches(Menu.StatusText, @"\d+");
            m_Pay = digits.Count > 0 ? int.Parse(digits[digits.Count - 1].Value, CultureInfo.InvariantCulture) : -1;
        }

        // ---- settings (M4B, windowed) ----

        private bool m_SettingsOk;

        /// <summary>
        /// -dev-flow settings: first launch (-dev-settings-step 1) sets a name, shows the settings tabs for screenshots,
        /// changes the Ready key to T through the page and closes it (saved); the second launch (step 2, same
        /// profile) checks the key and name came back from settings.json / save_v1.json.
        /// </summary>
        private IEnumerator SettingsCheck()
        {
            var panel = FindAnyObjectByType<SpiritMovers.Settings.SettingsPanel>();
            var ready = SpiritMovers.Settings.SettingsApplier.Actions.FindAction("Gameplay/Ready", true);
            if (Arg("-dev-settings-step") == "2")
            {
                Debug.Log($"[M4B] after restart: ready={ready.bindings[0].effectivePath} name={Save.GameData.PlayerName}");
                m_SettingsOk = ready.bindings[0].effectivePath == "<Keyboard>/t" && Save.GameData.PlayerName == m_Name;
                yield return Shot("menu-after-restart-th.png");
            }
            else
            {
                UseName();
                Menu.NameField.SetTextWithoutNotify(SpiritMovers.Core.Text.ThaiShaper.Shape(Save.GameData.PlayerName));
                yield return Shot("main-menu-th.png");
                Menu.ClickSettings();
                yield return Shot("settings-general-th.png");
                panel.ShowTab(1);
                yield return Shot("settings-audio-th.png");
                // The same override a key press on the Keys tab makes (no real keyboard in a scripted run); Close saves it.
                ready.ApplyBindingOverride(0, "<Keyboard>/t");
                panel.Close();
                Menu.ClickSettings();
                panel.ShowTab(2);
                yield return Shot("settings-keys-th.png");
                panel.Close();
                m_SettingsOk = true;
            }
            m_Done = true;
            Finish();
        }

        // ---- shared ----

        /// <summary>Windowed runs with -dev-shot-dir: capture the screen as it is now.</summary>
        private IEnumerator Shot(string name)
        {
            if (m_ShotDir == null || Application.isBatchMode)
                yield break;
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(m_ShotDir, name));
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The host's join code, or null while the file is missing, empty or still being written.</summary>
        private string TryReadCode()
        {
            try
            {
                var code = m_CodeFile != null && File.Exists(m_CodeFile) ? File.ReadAllText(m_CodeFile).Trim() : "";
                return code.Length > 0 ? code : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>Press R when the van shows us not ready — at most every 2 s, so a lagging list is not toggled twice.</summary>
        private IEnumerator KeepReady()
        {
            var lobby = Lobby;
            if (lobby == null || !lobby.IsSpawned || NetworkManager.Singleton == null)
                yield break;
            var me = NetworkManager.Singleton.LocalClientId;
            if (Time.realtimeSinceStartup >= m_NextReadyAt && lobby.Members.Any(m => m.ClientId == me && !m.Ready))
            {
                lobby.ToggleReadyRpc();
                m_NextReadyAt = Time.realtimeSinceStartup + 2f;
            }
            yield return new WaitForSeconds(0.25f);
        }

        private static IEnumerator MeetAtVan()
        {
            var motor = PlayerMotor.Local;
            if (motor == null)
                yield break;
            var rank = (int)(NetworkManager.Singleton.LocalClientId % 6);
            motor.Teleport(new Vector3(-8.5f, 0f, -7f + 0.8f * rank), 0f);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator Until(Func<bool> condition, string step)
        {
            var end = Time.realtimeSinceStartup + StepTimeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    m_Failure = step.Replace(' ', '-');
                    Debug.LogError($"[M4A] step timed out: {step}");
                    yield break;
                }
                yield return null;
            }
        }

        private void Finish()
        {
            if (m_Printed)
                return;
            m_Printed = true;
            if (m_Role == "host" || m_Role == "host-idle")
                m_Jobs = Math.Max(m_Jobs, m_Scenes.Count(s => s == GameSession.JobScene));
            var ok = m_Done && !Failed && m_Role switch
            {
                "host" => m_Jobs == 2,
                "host-idle" => Lobby != null && Lobby.Members.Count == 1,
                "client" => m_Jobs == 2 && m_Message == "session.host_left_job" && m_Pay >= 0,
                "late" => m_Refused == "error.in_job",
                "settings" => m_SettingsOk,
                _ => false,
            };
            if (m_Role == "client" && Arg("-dev-version") != null)
                ok = m_Done && m_Refused == "error.version";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[M4A] SUMMARY role={0} scenes={1} jobs={2} refused={3} message={4} pay={5} failed={6} name={7} names={8} " +
                "jobpay={9} moneyBefore={10} moneyAfter={11} saved={12} ok={13}",
                m_Role, string.Join(">", m_Scenes), m_Jobs, m_Refused, m_Message, m_Pay, Failed ? m_Failure : "none",
                Save.GameData.PlayerName.Replace(' ', '_'), m_Names.Replace(' ', '_'), m_JobPay, m_MoneyBefore,
                Save.GameData.Money, SavedInOwnProfile() ? "true" : "false", ok ? "true" : "false"));
            Application.Quit();
        }

        /// <summary>This bot's save sits in its own profile folder (several bots run on one machine, M4B).</summary>
        private static bool SavedInOwnProfile()
        {
            var root = Save.DataLocation.Root;
            return root.Replace('\\', '/').Contains("/profiles/") && File.Exists(Path.Combine(root, "save_v1.json"));
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
