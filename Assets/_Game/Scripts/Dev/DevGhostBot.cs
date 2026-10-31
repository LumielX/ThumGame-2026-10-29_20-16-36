using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using SpiritMovers.Core.Ghost;
using SpiritMovers.Core.Job;
using SpiritMovers.Ghost;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Bot for the M3D ghost test (Tools/m3c-job-test.ps1 -Scenario ghost). With every player in, the host starts a
    /// job; the second player ("victim") picks up the spirit camera, looks through it once and puts it down, then stands
    /// crouching and staring at the ghost — the host checks the hunting ghost stays frozen (remote watching: synced
    /// heading, pitch and crouch) — then turns its back; the ghost drags the victim into a
    /// cabinet; the host calms the ghost, puts one item of that room right, opens the cabinet; everyone walks to the
    /// van and the host closes the job. Each peer prints a [M3D] SUMMARY of what it saw.
    /// Optional "-dev-peek-shot PNG" / "-dev-hunt-shot PNG" (victim, windowed) capture the overlay and the ghost.
    /// </summary>
    public sealed class DevGhostBot : MonoBehaviour
    {
        private const float StepTimeout = 15f;

        private JobDirector m_Director;
        private ScriptedInput m_Input;
        private PlayerCarrier m_Carrier;
        private ulong m_VictimId;
        private int m_Rank;
        private int m_OverlayMax;
        private bool m_CapturedSeen;
        private bool m_ReleasedSeen;
        private bool m_WatchedSeen;
        private bool m_Frozen;
        private string m_Failure = "";

        public bool IsHost { get; private set; }
        public int Seed { get; private set; }
        public int ExpectedPlayers { get; private set; }
        public string PeekShot { get; private set; }
        public string HuntShot { get; private set; }
        public bool Done { get; private set; }

        public static DevGhostBot Attach(GameObject host, bool isHost, int seed, int expectedPlayers, string peekShot, string huntShot)
        {
            var bot = host.AddComponent<DevGhostBot>();
            bot.IsHost = isHost;
            bot.Seed = seed;
            bot.ExpectedPlayers = expectedPlayers;
            bot.PeekShot = peekShot;
            bot.HuntShot = huntShot;
            return bot;
        }

        private bool IsVictim => m_Rank == 1;
        private bool Failed => m_Failure != "";
        /// <summary>The camera the victim uses — the same one on every peer (4+ players get two cameras).</summary>
        private SpiritCamera Camera =>
            FindObjectsByType<SpiritCamera>(FindObjectsSortMode.None).OrderBy(c => c.NetworkObjectId).FirstOrDefault();
        private MemoryCabinet VictimCabinet => VictimCapture != null ? VictimCapture.Cabinet : null;

        private PlayerCapture VictimCapture =>
            FindObjectsByType<PlayerCapture>(FindObjectsSortMode.None).FirstOrDefault(c => c.OwnerClientId == m_VictimId);

        private void Update()
        {
            var ghost = FindAnyObjectByType<ShyGhost>();
            if (ghost != null && ghost.IsWatched && m_Director != null && m_Director.AngerStage >= 3)
                m_WatchedSeen = true;
            if (MemoryOverlay.Current != null)
                m_OverlayMax = Math.Max(m_OverlayMax, MemoryOverlay.Current.ActiveCount);
            var capture = VictimCapture;
            if (capture == null)
                return;
            if (capture.IsCaptured)
                m_CapturedSeen = true;
            else if (m_CapturedSeen)
                m_ReleasedSeen = true;
        }

        private IEnumerator Start()
        {
            m_Director = FindAnyObjectByType<JobDirector>();
            while (PlayerMotor.Local == null || FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length < ExpectedPlayers)
                yield return null;
            var motor = PlayerMotor.Local;
            m_Input = new ScriptedInput();
            motor.Input = m_Input;
            m_Carrier = motor.GetComponent<PlayerCarrier>();
            var ids = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Select(m => m.OwnerClientId).OrderBy(id => id).ToList();
            m_Rank = ids.IndexOf(NetworkManager.Singleton.LocalClientId);
            m_VictimId = ids[1];
            yield return new WaitForSeconds(1f);

            if (IsHost)
                m_Director.BeginJob(Seed, JobDifficulty.Normal);
            yield return Until(() => m_Director.Phase == JobPhase.Exploring && Camera != null, "job start");
            if (Failed)
                yield break;

            // Everyone but the victim waits in the kitchen facing the wall, out of the ghost's way.
            if (!IsVictim)
                motor.Teleport(new Vector3(6f, 0f, -2f + 1.5f * m_Rank), 90f);
            else
                yield return PeekThroughCamera(motor);
            if (Failed)
                yield break;

            // 1. The hunt: the victim crouches and stares at the ghost — it must freeze although it hunts — then turns away.
            yield return Until(() => Camera.Film < new ToolConfig().FilmPerCamera, "camera used");
            if (Failed)
                yield break;
            if (IsHost)
            {
                SetAngerStage(3);
                yield return CheckFrozen();
            }
            else if (IsVictim)
            {
                yield return StareAtGhost(motor);
            }
            yield return Until(() => m_CapturedSeen, "victim caught");
            if (Failed)
                yield break;
            if (IsVictim && HuntShot != null)
            {
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(HuntShot);
            }

            // 2. The rescue: calm the ghost, put one item of the cabinet's room right, open the cabinet.
            if (IsHost)
            {
                yield return new WaitForSeconds(1.5f);
                SetAngerStage(0);
                var cabinet = VictimCabinet;
                var room = m_Director.Plan.Rooms.FirstOrDefault(r => r.RoomId == cabinet.RoomId);
                var wrong = room?.Slots.FirstOrDefault(t => !InPlace(t));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (wrong != null)
                    m_Director.DevPutBackRpc(wrong.Slot.Id);
#endif
                yield return Until(() => cabinet.IsUnlocked, "cabinet unlocked");
                cabinet.Open(NetworkManager.Singleton.LocalClientId);
            }
            yield return Until(() => m_ReleasedSeen, "victim released");
            if (Failed)
                yield break;

            // 3. Everyone to the van; the host closes the job.
            yield return new WaitForSeconds(0.5f);
            motor.Teleport(new Vector3(-8.5f, 0f, -7f + 0.8f * m_Rank), 0f);
            if (IsHost)
            {
                var van = FindAnyObjectByType<VanExit>();
                yield return Until(() => van.EveryoneAtVan(), "everyone at the van");
                if (Failed)
                    yield break;
                van.Interact(m_Carrier);
            }
            yield return Until(() => m_Director.Result.IsSet, "results");
            Done = true;
        }

        /// <summary>Victim: crouch and stare at the ghost until it has hunted for a while, then turn your back.</summary>
        private IEnumerator StareAtGhost(PlayerMotor motor)
        {
            var ghost = FindAnyObjectByType<ShyGhost>();
            motor.Teleport(new Vector3(-5f, 0f, 4f), 0f);
            m_Input.Next.Crouch = true;
            var end = Time.time + StepTimeout;
            while (m_Director.AngerStage < 3 && Time.time < end)
            {
                motor.LookAt(ghost.transform.position);
                yield return null;
            }
            end = Time.time + 3.5f;
            while (Time.time < end)
            {
                motor.LookAt(ghost.transform.position);
                yield return null;
            }
            m_Input.Next.Crouch = false;
            motor.Teleport(motor.Feet, 180f);
        }

        /// <summary>Host: while the victim stares, the hunting ghost must not move.</summary>
        private IEnumerator CheckFrozen()
        {
            var ghost = FindAnyObjectByType<ShyGhost>();
            yield return new WaitForSeconds(1f);
            var start = ghost.transform.position;
            var watched = true;
            var end = Time.time + 1.5f;
            while (Time.time < end)
            {
                watched &= ghost.IsWatched;
                yield return null;
            }
            m_Frozen = watched && Vector3.Distance(start, ghost.transform.position) < 0.1f;
            Debug.Log($"[M3D] watch check: watched={watched} moved={Vector3.Distance(start, ghost.transform.position):F2}");
        }

        /// <summary>Victim: pick up the camera by the van, look through it once, put it down.</summary>
        private IEnumerator PeekThroughCamera(PlayerMotor motor)
        {
            var camera = Camera;
            var spot = camera.transform.position;
            motor.Teleport(new Vector3(spot.x, 0f, spot.z - 1.2f), 0f);
            yield return new WaitForSeconds(0.3f);
            motor.LookAt(spot);
            yield return null;
            m_Input.Next.InteractPressed = true;
            var item = camera.GetComponent<Carryable>();
            yield return Until(() => item.IsCarriedBy(NetworkManager.Singleton.LocalClientId), "camera picked up");
            motor.LookAt(motor.Head.position + Vector3.forward * 10f);
            yield return new WaitForSeconds(0.3f);
            if (PeekShot != null)
            {
                // Same view without the overlay, to compare against.
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(PeekShot, null) + "-before.png");
                yield return new WaitForSeconds(0.2f);
            }
            m_Input.Next.UsePressed = true;
            yield return Until(() => camera.PeeksSeen > 0, "peek");
            if (PeekShot != null)
            {
                yield return new WaitForSeconds(0.3f);
                if (!File.Exists(PeekShot))
                    ScreenCapture.CaptureScreenshot(PeekShot);
            }
            yield return new WaitForSeconds(0.5f);
            m_Input.Next.InteractPressed = true;
            yield return Until(() => !item.IsCarried, "camera put down");
        }

        private void SetAngerStage(int stage)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_Director.DevSetAngerStage(stage);
#else
            Debug.LogError("[M3D] the ghost bot needs a Development build");
#endif
        }

        private bool InPlace(SlotTask task)
        {
            var item = m_Director.House.Slot(task.Slot.Id).Item.transform;
            var actual = new Placement(m_Director.House.RoomAt(item.position), item.position.ToNumerics(), item.rotation.ToNumerics());
            return PlacementMath.IsCorrect(task.Slot.Target, actual, task.Slot.Tolerance);
        }

        private IEnumerator Until(Func<bool> condition, string step)
        {
            var end = Time.time + StepTimeout;
            while (!condition())
            {
                if (Time.time > end)
                {
                    m_Failure = step.Replace(' ', '-');
                    Debug.LogError($"[M3D] step timed out: {step}");
                    yield break;
                }
                yield return null;
            }
        }

        public string Summary(string role)
        {
            var result = m_Director.Result;
            var film = Camera != null ? Camera.Film : -1;
            var peeks = Camera != null ? Camera.PeeksSeen : -1;
            var holderOnly = IsVictim ? peeks == 1 && m_OverlayMax > 0 : peeks == 0 && m_OverlayMax == 0;
            var ok = Done && m_Failure == "" && result.IsSet && result.Ending == JobEnding.LeftEarly && m_CapturedSeen &&
                     m_ReleasedSeen && holderOnly && m_WatchedSeen && (!IsHost || (m_Director.SelfReleases == 0 && m_Frozen));
            return string.Format(CultureInfo.InvariantCulture,
                "[M3D] SUMMARY role={0} seed={1} ending={2} teamTotal={3} captured={4} released={5} film={6} peeks={7} " +
                "overlayMax={8} angerStage={9} watched={10} frozen={11} failed={12} ok={13}",
                role, Seed, result.IsSet ? result.Ending.ToString() : "none", result.TeamTotal, m_CapturedSeen, m_ReleasedSeen,
                film, peeks, m_OverlayMax, m_Director.AngerStage, m_WatchedSeen, IsHost ? m_Frozen.ToString() : "-",
                m_Failure == "" ? "none" : m_Failure, ok ? "true" : "false");
        }
    }
}
