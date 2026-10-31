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
    /// </summary>
    public sealed class DevBot : MonoBehaviour
    {
        private const float SideOffset = 0.6f;

        private ScriptedInput m_Input;
        private PlayerMotor m_Motor;
        private int m_LastCount;

        public string Scenario { get; private set; }
        public bool IsHost { get; private set; }
        public Carryable Item { get; private set; }
        public Vector3 ItemStart { get; private set; }
        public int Pickups { get; private set; }
        public int CarrierCountMax { get; private set; }
        public int ForceDrops { get; private set; }
        public bool Done { get; private set; }

        public static string ItemFor(string scenario) => scenario switch
        {
            "shared" => "HeavyCrate1",
            "disconnect" => "LightBox1",
            _ => "LightBox0",
        };

        public static DevBot Attach(GameObject host, string scenario, bool isHost)
        {
            var bot = host.AddComponent<DevBot>();
            bot.Scenario = scenario;
            bot.IsHost = isHost;
            return bot;
        }

        private IEnumerator Start()
        {
            Carryable.ForceDropped += (_, _) => ForceDrops++;
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
                case "disconnect" when !IsHost:
                    yield return PickUp(0f);
                    yield return new WaitForSeconds(3f);
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

        /// <summary>Client carries a light box 6 m east and puts it down.</summary>
        private IEnumerator Carry()
        {
            yield return PickUp(0f);
            yield return Walk(Vector3.right, 2f);
            m_Input.Next.InteractPressed = true;
            yield return new WaitForSeconds(2f);
        }

        /// <summary>Host and client lift the same heavy crate side by side and walk it north.</summary>
        private IEnumerator Shared()
        {
            yield return PickUp(IsHost ? -SideOffset : SideOffset);
            // Give the other player's pick-up time to reach the host before walking.
            var deadline = Time.time + 5f;
            while (Item.CarrierCount < 2 && Time.time < deadline)
                yield return null;
            yield return Walk(Vector3.forward, 2f);
        }

        public bool Passed()
        {
            var moved = Item != null ? Vector3.Distance(ItemStart, Item.transform.position) : 0f;
            return Scenario switch
            {
                "carry" => Pickups >= 1 && ForceDrops == 0 && Item.CarrierCount == 0,
                "shared" => CarrierCountMax == 2 && moved >= 1.5f,
                "disconnect" when IsHost => Pickups >= 1 && Item.CarrierCount == 0 && Item.IsSpawned &&
                                            Item.OwnerClientId == NetworkManager.ServerClientId,
                "disconnect" => Pickups >= 1,
                _ => false,
            };
        }
    }
}
