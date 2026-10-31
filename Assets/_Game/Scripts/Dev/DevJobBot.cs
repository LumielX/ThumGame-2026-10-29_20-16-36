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
    /// the host rings each room's bell once; the second player carries one light item to its place by hand (real
    /// pick-up, ownership to that client and back); every peer puts its share of the other items back (dev RPC)
    /// and waits for the job report. Optional "-dev-sheet-shot PNG" holds Tab halfway and captures the work order.
    /// </summary>
    public sealed class DevJobBot : MonoBehaviour
    {
        private const float PutBackGap = 0.4f;
        private const float LightMass = 10f;
        /// <summary>Walking pace while carrying; the host drops items that fall far behind the hand.</summary>
        private const float CarryStepMeters = 0.15f;
        private const float CarryStepSeconds = 0.05f;

        private JobDirector m_Director;
        private ScriptedInput m_Input;
        private PlayerCarrier m_Carrier;
        private SlotTask m_HandCarried;
        private bool m_IsHandCarrier;
        private bool m_CarryOwnedSeen;

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
            if (ids.Count > 1)
                m_HandCarried = HandCarryCandidate();
            m_IsHandCarrier = m_HandCarried != null && rank == 1;
            if (m_IsHandCarrier)
                yield return CarryIntoPlace(m_HandCarried);
            var slots = m_Director.Plan.Rooms.SelectMany(r => r.Slots).Select(t => t.Slot.Id)
                .Where(id => id != m_HandCarried?.Slot.Id).ToList();
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

        /// <summary>Same on every peer: the first light item that starts and belongs on the ground floor of one room.</summary>
        private SlotTask HandCarryCandidate()
        {
            var tasks = m_Director.Plan.Rooms.SelectMany(r => r.Slots)
                .Where(t => m_Director.House.Slot(t.Slot.Id).Item.Mass < LightMass &&
                            t.Slot.Target.Position.Y < 0.5f && t.StartPlacement.Position.Y < 0.5f &&
                            t.StartPlacement.RoomId == t.Slot.Target.RoomId)
                .ToList();
            return tasks.FirstOrDefault(t => t.Problem == ProblemType.Moved) ?? tasks.FirstOrDefault();
        }

        /// <summary>
        /// Picks the item up, walks (in short steps, at about walking pace) to where the hold point is over its place
        /// with the item facing the remembered way, and puts it down — the path a real player takes, so the host must
        /// settle and accept it after the handback.
        /// </summary>
        private IEnumerator CarryIntoPlace(SlotTask task)
        {
            var item = m_Director.House.Slot(task.Slot.Id).Item;
            var target = m_Director.House.Slot(task.Slot.Id).transform;
            var motor = PlayerMotor.Local;
            var start = item.transform.position;
            var inward = new Vector3(-start.x, 0f, -start.z);
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.back;
            motor.Teleport(new Vector3(start.x, 0f, start.z) + inward * 1.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            motor.LookAt(start);
            yield return null;
            m_Input.Next.InteractPressed = true;
            var deadline = Time.time + 3f;
            while (!(item.IsCarriedBy(NetworkManager.Singleton.LocalClientId) && item.IsOwner) && Time.time < deadline)
                yield return null;
            m_CarryOwnedSeen = item.IsOwner;
            Debug.Log($"[M3C] hand-carry {task.Slot.Id} picked={m_CarryOwnedSeen} item={start} me={motor.Feet} carriers={item.CarrierCount}");
            if (!m_CarryOwnedSeen)
                yield break;
            yield return new WaitForSeconds(0.5f);

            var motorYaw = motor.transform.eulerAngles.y;
            var hold = Quaternion.Euler(0f, -motorYaw, 0f) * (m_Carrier.HoldPoint - motor.Feet);
            // Face the place along the way we walk in (the far side may be taken by other items), turning the item
            // in hand (whole rotate steps) so it ends up the remembered way; the player's yaw takes the remainder.
            var approach = Mathf.Atan2(target.position.x - motor.Feet.x, target.position.z - motor.Feet.z) * Mathf.Rad2Deg;
            var offset = Mathf.DeltaAngle(motorYaw, item.transform.eulerAngles.y);
            m_Input.Next.RotateSteps = 1f;
            yield return new WaitForSeconds(0.5f);
            var stepDegrees = Mathf.DeltaAngle(offset, Mathf.DeltaAngle(motorYaw, item.transform.eulerAngles.y));
            if (Mathf.Abs(stepDegrees) > 1f)
            {
                var turns = Mathf.RoundToInt(Mathf.DeltaAngle(offset + stepDegrees, target.eulerAngles.y - approach) / stepDegrees);
                m_Input.Next.RotateSteps = turns;
                yield return new WaitForSeconds(0.5f);
            }
            var yaw = target.eulerAngles.y - Mathf.DeltaAngle(motorYaw, item.transform.eulerAngles.y);
            var feet = target.position - Quaternion.Euler(0f, yaw, 0f) * hold;
            feet.y = 0f;
            var from = motor.Feet;
            from.y = 0f;
            var steps = Mathf.CeilToInt(Vector3.Distance(from, feet) / CarryStepMeters);
            for (var i = 1; i <= steps; i++)
            {
                motor.Teleport(Vector3.Lerp(from, feet, (float)i / steps), Mathf.LerpAngle(motorYaw, yaw, (float)i / steps));
                yield return new WaitForSeconds(CarryStepSeconds);
            }
            yield return new WaitForSeconds(1f);
            // Like a player eyeing the item in hand: shuffle until it hangs over its place (it may have been bumped).
            for (var i = 0; i < 4; i++)
            {
                var miss = target.position - item.transform.position;
                miss.y = 0f;
                var near = string.Join(",", Physics.OverlapSphere(target.position + Vector3.up * 0.5f, 0.6f).Select(c => c.name));
                Debug.Log($"[M3C] hand-carry aim miss={miss.magnitude:F2} me={motor.Feet} item={item.transform.position} near={near}");
                if (miss.magnitude < 0.05f)
                    break;
                motor.Teleport(motor.Feet + miss, yaw);
                yield return new WaitForSeconds(0.5f);
            }
            m_Input.Next.InteractPressed = true;
            deadline = Time.time + 3f;
            while (item.IsCarried && Time.time < deadline)
                yield return null;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[M3C] hand-carry {task.Slot.Id} put down at {item.transform.position} (place {target.position})");
        }

        /// <summary>Every peer: is the hand-carried item where the ghost remembers it ("none" in a one-player run)?</summary>
        private string HandCarryResult()
        {
            if (m_HandCarried == null)
                return "none";
            var body = m_Director.House.Slot(m_HandCarried.Slot.Id).Item.transform;
            var actual = new Placement(m_Director.House.RoomAt(body.position), body.position.ToNumerics(), body.rotation.ToNumerics());
            var placed = PlacementMath.IsCorrect(m_HandCarried.Slot.Target, actual, m_HandCarried.Slot.Tolerance);
            Debug.Log($"[M3C] hand-carry {m_HandCarried.Slot.Id} check room={actual.RoomId} pos={body.position} rot={body.rotation.eulerAngles} " +
                      $"angle={PlacementMath.AngleDegrees(m_HandCarried.Slot.Target.Rotation, actual.Rotation):F1} placed={placed}");
            return placed && (!m_IsHandCarrier || m_CarryOwnedSeen) ? "ok" : "fail";
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
            var carried = HandCarryResult();
            var ok = Done && result.IsSet && result.Ending == JobEnding.FullSuccess && m_Director.PlanMatchesHost &&
                     m_Director.AcceptedRooms.Count == planRooms && (ExpectedPlayers == 1 ? carried == "none" : carried == "ok");
            return string.Format(CultureInfo.InvariantCulture,
                "[M3C] SUMMARY role={0} seed={1} phase={2} ending={3} accepted={4} teamTotal={5} perPlayer={6} stars={7} " +
                "angerStage={8} planOk={9} carried={10} ok={11}",
                role, Seed, m_Director.Phase, result.IsSet ? result.Ending.ToString() : "none", string.Join(",", m_Director.AcceptedRooms),
                result.TeamTotal, result.PerPlayer, result.Stars, m_Director.AngerStage, m_Director.PlanMatchesHost, carried, ok ? "true" : "false");
        }
    }
}
