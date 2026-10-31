using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using SpiritMovers.Core.Job;
using SpiritMovers.Interaction;
using SpiritMovers.Job;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Bot for the M3C job test (Tools/m3c-job-test.ps1): once every player is in, the host starts a Normal job;
    /// the host rings each room's bell once; every peer puts its share of the planned items back (dev RPC)
    /// and waits for the job report. Optional "-dev-sheet-shot PNG" holds Tab halfway and captures the work order.
    /// </summary>
    public sealed class DevJobBot : MonoBehaviour
    {
        private const float PutBackGap = 0.4f;

        private JobDirector m_Director;
        private ScriptedInput m_Input;
        private PlayerCarrier m_Carrier;

        public bool IsHost { get; private set; }
        public int Seed { get; private set; }
        public int ExpectedPlayers { get; private set; }
        public string SheetShot { get; private set; }
        public bool Done { get; private set; }

        public static DevJobBot Attach(GameObject host, bool isHost, int seed, int expectedPlayers, string sheetShot)
        {
            var bot = host.AddComponent<DevJobBot>();
            bot.IsHost = isHost;
            bot.Seed = seed;
            bot.ExpectedPlayers = expectedPlayers;
            bot.SheetShot = sheetShot;
            return bot;
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
            yield return new WaitForSeconds(1f);

            if (IsHost)
                m_Director.BeginJob(Seed, JobDifficulty.Normal);
            while (m_Director.Plan == null || m_Director.Phase != JobPhase.Exploring)
                yield return null;
            Debug.Log($"[M3C] bot start role={(IsHost ? "host" : "client")} seed={Seed} rooms={string.Join(",", m_Director.Plan.Rooms.Select(r => r.RoomId))}");

            if (IsHost)
                foreach (var bell in FindObjectsByType<GhostBell>(FindObjectsSortMode.None).OrderBy(b => b.RoomId))
                    bell.Interact(m_Carrier);

            // Each peer handles every n-th item, n = player count, by its rank among the players.
            var ids = FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Select(m => m.OwnerClientId).OrderBy(id => id).ToList();
            var rank = ids.IndexOf(NetworkManager.Singleton.LocalClientId);
            var slots = m_Director.Plan.Rooms.SelectMany(r => r.Slots).Select(t => t.Slot.Id).ToList();
            var shotTaken = SheetShot == null;
            for (var i = 0; i < slots.Count; i++)
            {
                if (!shotTaken && i >= slots.Count / 2)
                {
                    shotTaken = true;
                    yield return CaptureSheet();
                }
                if (i % ids.Count != rank)
                    continue;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                m_Director.DevPutBackRpc(slots[i]);
#else
                Debug.LogError("[M3C] the job bot needs a Development build");
#endif
                yield return new WaitForSeconds(PutBackGap);
            }

            while (!m_Director.Result.IsSet)
                yield return null;
            Done = true;
        }

        private IEnumerator CaptureSheet()
        {
            m_Input.Next.JobSheetHeld = true;
            yield return new WaitForSeconds(1f);
            if (!File.Exists(SheetShot))
                ScreenCapture.CaptureScreenshot(SheetShot);
            yield return new WaitForSeconds(0.5f);
            m_Input.Next.JobSheetHeld = false;
        }

        public string Summary(string role)
        {
            var result = m_Director.Result;
            var planRooms = m_Director.Plan?.Rooms.Count ?? -1;
            var ok = Done && result.IsSet && result.Ending == JobEnding.FullSuccess && m_Director.PlanMatchesHost &&
                     m_Director.AcceptedRooms.Count == planRooms;
            return string.Format(CultureInfo.InvariantCulture,
                "[M3C] SUMMARY role={0} seed={1} phase={2} ending={3} accepted={4} teamTotal={5} perPlayer={6} stars={7} " +
                "angerStage={8} planOk={9} ok={10}",
                role, Seed, m_Director.Phase, result.IsSet ? result.Ending.ToString() : "none", string.Join(",", m_Director.AcceptedRooms),
                result.TeamTotal, result.PerPlayer, result.Stars, m_Director.AngerStage, m_Director.PlanMatchesHost, ok ? "true" : "false");
        }
    }
}
