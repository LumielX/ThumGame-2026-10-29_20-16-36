using System.Collections;
using SpiritMovers.Interaction;
using SpiritMovers.Player;
using Unity.Netcode;
using UnityEngine;

namespace SpiritMovers.Dev
{
    /// <summary>
    /// Headless bot for the M3B two-instance tests: drives the local player with <see cref="ScriptedInput"/>
    /// through one scenario and records what this peer saw happen to the scenario's item.
    /// Scenarios: carry · shared (two carry, host lets go, client keeps it) · split (two carriers walk apart)
    /// · remote (client rotates and throws) · disconnect (client quits while carrying) · crash (client process killed).
    /// </summary>
    public sealed class DevBot : MonoBehaviour
    {
        private const float SideOffset = 0.6f;

        private ScriptedInput m_Input;
        private PlayerMotor m_Motor;
        private int m_LastCount;
        private float? m_CarriedYaw;

        public string Scenario { get; private set; }
        public bool IsHost { get; private set; }
        public Carryable Item { get; private set; }
        public Vector3 ItemStart { get; private set; }
        public int Pickups { get; private set; }
        public int CarrierCountMax { get; private set; }
        public int ForceDrops { get; private set; }
        public bool RotateSeen { get; private set; }
        /// <summary>shared: after the host let go the client alone carries it (and owns it, seen from the host).</summary>
        public bool HandoffOk { get; private set; }
        /// <summary>disconnect/crash: the host could pick the item up again after the client left.</summary>
        public bool ReCarried { get; private set; }
        public bool Done { get; private set; }

        public static string ItemFor(string scenario) => scenario switch
        {
            "shared" or "split" => "HeavyCrate1",
            "disconnect" or "crash" => "LightBox1",
            "remote" => "LightBox2",
            _ => "LightBox0",
        };

        public static DevBot Attach(GameObject host, string scenario, bool isHost)
        {
            var bot = host.AddComponent<DevBot>();
            bot.Scenario = scenario;
            bot.IsHost = isHost;
            return bot;
        }

        private void OnEnable() => Carryable.ForceDropped += OnForceDropped;

        private void OnDisable() => Carryable.ForceDropped -= OnForceDropped;

        private void OnForceDropped(Carryable item, ulong clientId) => ForceDrops++;

        private IEnumerator Start()
        {
            while (PlayerMotor.Local == null || FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length < 2)
                yield return null;
            m_Motor = PlayerMotor.Local;
            m_Input = new ScriptedInput();
            m_Motor.Input = m_Input;
            Item = GameObject.Find(ItemFor(Scenario)).GetComponent<Carryable>();
            ItemStart = Item.transform.position;
            yield return new WaitForSeconds(1f);
            Debug.Log($"[M3B] bot start role={(IsHost ? "host" : "client")} scenario={Scenario} item={Item.name}");

            switch (Scenario)
            {
                case "carry" when !IsHost:
                    yield return Carry();
                    break;
                case "shared":
                    yield return Shared();
                    break;
                case "split":
                    yield return Split();
                    break;
                case "remote" when !IsHost:
                    yield return RemoteRotateAndThrow();
                    break;
                case "disconnect" or "crash" when !IsHost:
                    yield return PickUp(0f);
                    yield return new WaitForSeconds(3f);
                    if (Scenario == "crash")
                    {
                        Debug.Log("[M3B] crashing on purpose (process kill, no goodbye to the host)");
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    }
                    break;
                case "disconnect" or "crash":
                    yield return PickUpAfterClientLeaves();
                    break;
            }
            Done = true;
        }

        private void Update()
        {
            if (Item == null)
                return;
            var count = Item.CarrierCount;
            if (count > m_LastCount)
                Pickups += count - m_LastCount;
            m_LastCount = count;
            CarrierCountMax = Mathf.Max(CarrierCountMax, count);

            // remote: watch the client's rotate request arrive while it carries alone.
            if (count == 1)
            {
                m_CarriedYaw ??= Item.transform.eulerAngles.y;
                if (Mathf.Abs(Mathf.DeltaAngle(m_CarriedYaw.Value, Item.transform.eulerAngles.y)) >= 10f)
                    RotateSeen = true;
            }
        }

        private IEnumerator PickUp(float sideOffset)
        {
            var target = Item.transform.position;
            m_Motor.Teleport(new Vector3(target.x + sideOffset, 0f, target.z - 1.3f), 0f);
            yield return new WaitForSeconds(0.3f);
            m_Motor.LookAt(target);
            yield return null;
            m_Input.Next.InteractPressed = true;
            yield return new WaitForSeconds(0.8f);
        }

        private IEnumerator Walk(Vector3 direction, float seconds)
        {
            m_Motor.LookAt(m_Motor.Head.position + direction * 10f);
            m_Input.Next.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(seconds);
            m_Input.Next.Move = Vector2.zero;
        }

        private IEnumerator WaitForCarriers(int count, float timeout)
        {
            var deadline = Time.time + timeout;
            while (Item.CarrierCount != count && Time.time < deadline)
                yield return null;
        }

        /// <summary>Client carries a light box 6 m east and puts it down.</summary>
        private IEnumerator Carry()
        {
            yield return PickUp(0f);
            yield return Walk(Vector3.right, 2f);
            m_Input.Next.InteractPressed = true;
            yield return new WaitForSeconds(2f);
        }

        /// <summary>Both lift a heavy crate and walk it north; then the host lets go and the client keeps it.</summary>
        private IEnumerator Shared()
        {
            yield return PickUp(IsHost ? -SideOffset : SideOffset);
            yield return WaitForCarriers(2, 5f);
            yield return Walk(Vector3.forward, 2f);
            if (IsHost)
            {
                m_Input.Next.InteractPressed = true;
                yield return WaitForCarriers(1, 3f);
                yield return new WaitForSeconds(0.5f);
                HandoffOk = Item.CarrierCount == 1 && Item.OwnerClientId != NetworkManager.ServerClientId;
            }
            else
            {
                yield return WaitForCarriers(1, 5f);
                HandoffOk = Item.CarrierCount == 1 && Item.IsCarriedBy(NetworkManager.Singleton.LocalClientId);
                yield return Walk(Vector3.right, 1f);
            }
        }

        /// <summary>Both lift a heavy crate, then walk away from each other.</summary>
        private IEnumerator Split()
        {
            yield return PickUp(IsHost ? -SideOffset : SideOffset);
            yield return WaitForCarriers(2, 5f);
            yield return Walk(IsHost ? Vector3.left : Vector3.right, 3f);
            yield return new WaitForSeconds(1.5f);
        }

        /// <summary>Client carries a box, rotates it one step, then throws it north.</summary>
        private IEnumerator RemoteRotateAndThrow()
        {
            yield return PickUp(0f);
            m_Input.Next.RotateSteps = 1f;
            yield return new WaitForSeconds(1f);
            m_Motor.LookAt(m_Motor.Head.position + Vector3.forward * 10f);
            yield return new WaitForSeconds(0.3f);
            m_Input.Next.ThrowPressed = true;
            yield return new WaitForSeconds(2f);
        }

        /// <summary>Host waits for the client to vanish and the item to be free, then picks it up itself.</summary>
        private IEnumerator PickUpAfterClientLeaves()
        {
            while (Pickups == 0)
                yield return null;
            while (FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None).Length > 1 || Item.CarrierCount > 0)
                yield return null;
            yield return new WaitForSeconds(1f);
            yield return PickUp(0f);
            ReCarried = Item.IsCarriedBy(NetworkManager.ServerClientId);
        }

        public bool Passed()
        {
            var moved = Item != null ? Vector3.Distance(ItemStart, Item.transform.position) : 0f;
            return Scenario switch
            {
                "carry" => Pickups >= 1 && ForceDrops == 0 && Item.CarrierCount == 0,
                "shared" => CarrierCountMax == 2 && moved >= 1.5f && HandoffOk,
                "split" when IsHost => CarrierCountMax == 2 && ForceDrops >= 1 && Item.CarrierCount <= 1,
                "split" => CarrierCountMax == 2,
                // Thrown north: lands at least 2 m beyond where it was picked up.
                "remote" when IsHost => Pickups >= 1 && RotateSeen && Item.transform.position.z - ItemStart.z >= 2f && Item.CarrierCount == 0,
                "remote" => Pickups >= 1,
                "disconnect" or "crash" when IsHost => ReCarried,
                "disconnect" or "crash" => Pickups >= 1,
                _ => false,
            };
        }
    }
}
