# M3B Player & Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ผู้เล่นเดิน/วิ่ง/ย่อ/กระโดดเตี้ยแบบบุคคลที่หนึ่ง และยก/วาง/หมุน/โยนของในห้องทดสอบ Greybox ได้ผ่านเครือข่าย — ของเบาถือคนเดียวแบบโอนสิทธิ์ (วิธี B) ของหนักช่วยกันยกได้ 2 คนแบบ Host จำลอง (วิธี A)

**Architecture:** กติกาที่เป็นตัวเลข (ความเร็ว, Stamina, น้ำหนัก, จุดถือ, ตรวจของหลุดมือ) อยู่ใน `SpiritMovers.Core` (`Core/Player/`) ทดสอบด้วย EditMode · ตัวละครและของเป็น `NetworkBehaviour` ใน `SpiritMovers.Runtime` (`Runtime/Player/`, `Runtime/Interaction/`) อ่าน Input ผ่าน `IPlayerInputSource` เพื่อให้ Test/บอทป้อนแทนคีย์บอร์ดได้ · ฉากทดสอบและตัวเริ่ม Session สำหรับนักพัฒนาอยู่ใน Assembly ใหม่ `SpiritMovers.Dev` (`Scripts/Dev/`) ที่อ้าง Net ได้ · ทดสอบ 3 ชั้น: EditMode (Core) → PlayMode (ฉากจริง Host ในเครื่อง ไม่ใช้ UGS) → 2 Instance ผ่าน Relay จริงด้วยสคริปต์

**Tech Stack:** Unity 6000.3.27f1 · NGO 2.13.3 (`NetworkTransform` Owner authority, `NetworkRigidbody`, `[Rpc(SendTo.Server)]`) · Input System 1.20.1 (`.inputactions` JSON) · CharacterController · Unity Test Framework (EditMode + PlayMode) · `Tools/test.ps1`

**Spec:** `docs/GDD.md` §7, §8.1, §8.3 · `docs/TECHNICAL_DESIGN.md` §6, §7, §11 · `docs/DECISIONS.md` D-014, D-015, D-021, D-034 · ภาพรวม `docs/superpowers/plans/2026-10-31-m3-overview.md`

## Global Constraints
- กล้องบุคคลที่หนึ่งระหว่างเล่น (D-014) · Character Controller ตอบสนองทันที (D-015 M1)
- ท่า: เดิน · วิ่ง (Stamina, ฝีเท้าดัง) · ย่อ (เงียบ ช้า) · กระโดดเตี้ย · ของเบามือเดียว/ของหนักสองมือ (ช้าลง วิ่งไม่ได้) · ช่วยกันยกแล้วเร็วขึ้น · โยนของเบาได้ (D-015, GDD §8.1)
- การยกของ: **วิธี B** สำหรับของที่ถือคนเดียว (`NetworkTransform` Owner authority + `DontDestroyWithOwner = true`) · **วิธี A** (Host จำลอง) เมื่อช่วยกันยก · Host ล้างสถานะการถือเมื่อคนถือหลุด · Host ตรวจความสมเหตุสมผลของตำแหน่งที่ Owner ส่งมา (D-034)
- ระยะ/มุมโต้ตอบตรวจบน Host (TECHNICAL_DESIGN §6) · Input ผ่าน Input System, Action Map `Gameplay` (TECHNICAL_DESIGN §11) — Rebinding ทำใน Milestone หลัง
- Core ห้ามอ้าง `UnityEngine` → ใช้ `System.Numerics` (เหมือน M3A) · Runtime อ้าง `Unity.Netcode.Runtime` ตรงได้เพราะ `NetworkBehaviour` เป็นชนิดของ NGO (การสร้าง Session ยังอยู่ใน Net)
- ไม่มีข้อความฝังในโค้ดที่ผู้เล่นเห็น → คำแนะนำโต้ตอบใช้คีย์ Localization ในตาราง `UI` ผ่าน `LocalizedThaiText`
- **นอกขอบเขต M3B:** Ragdoll (D-015), เครื่องมือบนเข็มขัด, รถเข็น, ประตู/บันไดที่มีกลไก, Rebinding UI, แขนบุคคลที่หนึ่ง, เสียงฝีเท้าจริง (ส่งเป็นค่า `Noise` ไว้ให้ M3D)
- ค่า ⚙ เริ่มต้น (ประเภท C ปรับได้หลัง Playtest) อยู่ใน `MovementConfig`/`CarryConfig`:

| ค่า | ค่าเริ่มต้น |
|---|---|
| เดิน / วิ่ง / ย่อ (ม./วินาที) | 3.0 / 5.5 / 1.6 |
| กระโดด / แรงโน้มถ่วง | สูง 0.6 ม. / −20 ม./วินาที² |
| Stamina | เต็ม 5 วินาที · ฟื้น 1/วินาทีหลังหยุดวิ่ง 1 วินาที · เริ่มวิ่งได้เมื่อ ≥ 0.5 |
| ของหนัก | มวล ≥ 15 กก. · ถือคนเดียว ×0.6 · ช่วยกัน 2 คน ×0.85 · ไม่วิ่ง ไม่กระโดด ไม่โยน |
| เสียง (`Noise` 0–1) เดิน / วิ่ง / ย่อ / ยืน | 0.3 / 1.0 / 0.05 / 0 |
| ระยะเอื้อม / จุดถือ ของเบา / ของหนัก | 2.5 ม. / หน้า 1.2 สูง 1.3 / หน้า 1.0 สูง 0.9 (จากเท้า ตามทิศ Yaw — ไม่ใช้ Pitch) |
| ตามมือ | ความเร็ว = (จุดถือ − ของ) × 12 จำกัด 10 ม./วินาที |
| หลุดมือ | ห่างจุดถือ > 2.0 ม. ต่อเนื่อง > 0.5 วินาที → Host บังคับปล่อย |
| โยน / หมุน | 6 ม./วินาที ตามทิศกล้อง / 15° ต่อ 1 ขั้นล้อเมาส์ |

## Review Focus
1. **คนถือหลุดกลางทาง** — ของต้องไม่หาย, ต้องกลับเป็นของว่างที่คนอื่นยกได้, Owner กลับเป็น Host (Task 5: `ReleaseCarrier_ClearsCarrierAndReturnsOwnership` · Task 6 สถานการณ์ `disconnect`)
2. **สองคนกดยกของเบาชิ้นเดียวกันพร้อมกัน** — คนแรกที่ Host ได้รับเท่านั้นที่ได้ คนที่สองไม่มีผลอะไร (Task 4: `TryAddCarrier_LightItemSecondCarrier_IsRejected`)
3. **ของติดผนัง/ถูกดึงทะลุสิ่งกีดขวาง** — ต้องไม่ดีดแรงจนกระเด็น (จำกัดความเร็วตามมือ) และถ้าค้างห่างมือนานต้องหลุดมือเอง (Task 2: `FollowVelocity_IsClampedToMaxSpeed`, `HoldErrorWatch_*` · Task 5: `HeldItemFarFromHand_IsForceDropped`)
4. **เปิดแชต/เมนูระหว่างถือของ** — ตัวละครหยุดเดิน แต่ของยังอยู่ในมือ ไม่หล่น (Task 3: `InputDisabled_StopsMovement` · Task 4: `InputDisabledWhileCarrying_KeepsItem`)
5. **มองขึ้นฟ้า/ก้มลงพื้นสุดตอนถือของ** — จุดถืออยู่หน้าตัวที่ความสูงเดิม ไม่จมพื้นหรือลอยขึ้นเพดาน (Task 2: `HoldPoint_IgnoresPitch`)

---

### Task 1: Core — MovementRules และ Stamina

**Files:**
- Create: `Assets/_Game/Scripts/Core/Player/MovementRules.cs`, `Assets/_Game/Scripts/Core/Player/Stamina.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Player/MovementRulesTests.cs`

**Interfaces:**
- Produces (namespace `SpiritMovers.Core.Player`):
  - `enum CarryLoad { None, Light, Heavy, HeavyShared }`
  - `readonly struct MoveIntent(float forward, float right, bool sprint, bool crouch, bool jump)`
  - `sealed class MovementConfig` — ค่าตามตาราง: `WalkSpeed, RunSpeed, CrouchSpeed, JumpHeight, Gravity, HeavySpeedFactor = 0.6f, SharedHeavySpeedFactor = 0.85f, NoiseWalk, NoiseRun, NoiseCrouch`, `static MovementConfig Default => new()`
  - `readonly struct MoveResult { float Speed; bool Running; bool Crouching; bool CanJump; float Noise }`
  - `static class MovementRules { static MoveResult Evaluate(MoveIntent intent, bool staminaAllowsRun, CarryLoad load, MovementConfig config) }` — เคลื่อนที่เมื่อ `|(forward,right)| > 0.1` · ย่อชนะวิ่ง · Heavy/HeavyShared: ห้ามวิ่ง ห้ามกระโดด คูณความเร็ว · ยืนนิ่ง Speed 0, Noise 0
  - `sealed class Stamina(float max = 5f, float regenPerSecond = 1f, float regenDelay = 1f, float minToStart = 0.5f)` — `float Current` · `bool AllowsRun(bool wasRunning)` (วิ่งต่อได้ถ้า `Current > 0`; เริ่มใหม่ต้อง `>= minToStart`) · `void Tick(float dt, bool running)`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] Idle_HasNoSpeedAndNoNoise()                    // intent (0,0) → Speed 0, Noise 0
[Test] Walk_UsesWalkSpeedAndWalkNoise()               // (1,0) → 3.0, Noise 0.3
[Test] Sprint_WithStamina_Runs()                      // sprint, allowsRun true → 5.5, Running, Noise 1.0
[Test] Sprint_WithoutStamina_Walks()                  // allowsRun false → 3.0, !Running
[Test] Crouch_BeatsSprint()                           // sprint+crouch → 1.6, Crouching, Noise 0.05
[Test] Heavy_CannotRunOrJumpAndIsSlower()             // load Heavy, sprint → 1.8, !Running, !CanJump
[Test] SharedHeavy_IsFasterThanHeavyAlone()           // HeavyShared → 2.55
[Test] Stamina_DrainsWhileRunningAndStopsRun()        // Tick(5.1,true) → Current 0, AllowsRun(true) false
[Test] Stamina_RegeneratesAfterDelay()                // empty → Tick(1,false) still 0 → Tick(1,false) → 1
[Test] Stamina_NeedsMinimumToStartRunning()           // Current 0.3 → AllowsRun(false) false, AllowsRun(true) true
```
- [ ] **Step 2: รันให้ล้มเหลว** — `powershell -ExecutionPolicy Bypass -File Tools\test.ps1 -Filter MovementRulesTests` → คอมไพล์ไม่ผ่าน
- [ ] **Step 3: เขียน `MovementRules.cs` และ `Stamina.cs` ตาม Interfaces**
- [ ] **Step 4: รันให้ผ่าน** — 10/10
- [ ] **Step 5: Commit** — `M3B: movement rules and stamina`

---

### Task 2: Core — CarryRules และ HoldErrorWatch

**Files:**
- Create: `Assets/_Game/Scripts/Core/Player/CarryRules.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Player/CarryRulesTests.cs`

**Interfaces:**
- Consumes: `CarryLoad` (Task 1)
- Produces:
  - `sealed class CarryConfig` — `HeavyMass = 15f, ReachMeters = 2.5f, LightHoldForward = 1.2f, LightHoldHeight = 1.3f, HeavyHoldForward = 1.0f, HeavyHoldHeight = 0.9f, FollowGain = 12f, MaxFollowSpeed = 10f, MaxHoldError = 2.0f, HoldErrorGraceSeconds = 0.5f, ThrowSpeed = 6f, RotateStepDegrees = 15f, MaxCarriers = 2`, `static CarryConfig Default => new()`
  - `static class CarryRules`
    - `bool IsHeavy(float mass, CarryConfig c)` (`mass >= HeavyMass`)
    - `CarryLoad LoadFor(float mass, int carriers, CarryConfig c)` — 0 → None · เบา → Light · หนัก 1 → Heavy · หนัก ≥2 → HeavyShared
    - `int MaxCarriersFor(float mass, CarryConfig c)` — เบา 1 · หนัก `MaxCarriers`
    - `bool CanThrow(float mass, int carriers, CarryConfig c)` — เบาและคนเดียว
    - `Vector3 HoldPoint(Vector3 feet, Vector3 forward, float mass, CarryConfig c)` — ใช้เฉพาะส่วนแนวนอนของ `forward` (normalize หลังตัดแกน Y; ถ้าศูนย์ใช้ +Z)
    - `Vector3 SharedHoldPoint(IReadOnlyList<Vector3> holdPoints)` — ค่าเฉลี่ย · ว่าง → `ArgumentException`
    - `Vector3 FollowVelocity(Vector3 holdPoint, Vector3 itemPosition, CarryConfig c)` — `(hold − item) × FollowGain` จำกัดขนาดไม่เกิน `MaxFollowSpeed`
  - `sealed class HoldErrorWatch(CarryConfig c)` — `bool Update(float distanceFromHold, float dt)` คืน true เมื่อห่างเกิน `MaxHoldError` ต่อเนื่องเกิน `HoldErrorGraceSeconds` · กลับมาใกล้ = รีเซ็ต · `void Reset()`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] LoadFor_ByMassAndCarriers()              // (2kg,0)None (2kg,1)Light (20kg,1)Heavy (20kg,2)HeavyShared
[Test] MaxCarriers_LightOneHeavyTwo()
[Test] CanThrow_OnlyLightSingleCarrier()         // (2,1) true · (20,1) false · (20,2) false
[Test] HoldPoint_LightInFrontAtChestHeight()     // feet (0,0,0) forward +Z → (0,1.3,1.2)
[Test] HoldPoint_IgnoresPitch()                  // forward (0,-0.9,0.1) และ (0,0.9,0.1) → ทั้งคู่ (0,1.3,1.2)   (Review Focus 5)
[Test] SharedHoldPoint_IsAverage()               // (0,1,0),(2,1,2) → (1,1,1) · ว่าง → Throws<ArgumentException>
[Test] FollowVelocity_PullsTowardHold()          // hold (0,0,0.5) item 0 → (0,0,6)
[Test] FollowVelocity_IsClampedToMaxSpeed()      // hold (0,0,5) → ขนาด 10 ทิศ +Z   (Review Focus 3)
[Test] HoldErrorWatch_TriggersAfterGrace()       // Update(2.5,0.3) false · Update(2.5,0.3) true
[Test] HoldErrorWatch_ResetsWhenBackInRange()    // 2.5/0.4 → 0.5/0.1 → 2.5/0.4 → false
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter CarryRulesTests`
- [ ] **Step 3: เขียน `CarryRules.cs` ตาม Interfaces**
- [ ] **Step 4: รันให้ผ่าน** — 10/10
- [ ] **Step 5: Commit** — `M3B: carry rules and hold error watch`

---

### Task 3: ตัวละคร, Input, ฉากทดสอบ และ PlayMode Test

**Files:**
- Create: `Assets/_Game/Settings/Input/SpiritMovers.inputactions` (Action Map `Gameplay`: Move [WASD], Look [Mouse delta], Sprint [Left Shift], Crouch [Left Ctrl, C], Jump [Space], Interact [E, Left Mouse], Throw [Right Mouse], RotateItem [Mouse Scroll Y])
- Create: `Assets/_Game/Scripts/Runtime/Player/PlayerInputState.cs` (struct + `IPlayerInputSource`), `KeyboardMouseInput.cs`, `ScriptedInput.cs`, `PlayerMotor.cs`
- Create: `Assets/_Game/Scripts/Dev/SpiritMovers.Dev.asmdef` (อ้าง Core, Runtime, Net, `Unity.Netcode.Runtime` — UnityTransport อยู่ใน Assembly นี้) + `Assets/_Game/Scripts/Dev/DevSessionRunner.cs`
- Create: `Assets/_Game/Scripts/Editor/TestRoomSetup.cs` → สร้าง `Assets/_Game/Prefabs/Player.prefab`, `Assets/_Game/Scenes/M3_TestRoom.unity` (พื้น 20×20 ม., 2 ห้องคั่นผนังมีประตูกว้าง 1.6 ม., บันได 6 ขั้นสูงขั้นละ 0.2 ม. ขึ้นชั้นลอย, NetworkManager + UnityTransport + DevSessionRunner)
- Modify: `Assets/_Game/Scripts/Runtime/SpiritMovers.Runtime.asmdef` (อ้าง `Unity.Netcode.Runtime`, `Unity.InputSystem`)
- Create: `Assets/_Game/Scripts/Tests/PlayMode/SpiritMovers.Tests.PlayMode.asmdef`, `TestRoomFixture.cs`, `PlayerMotorTests.cs`

**Interfaces:**
- Consumes: `MovementRules`, `Stamina`, `MoveIntent`, `CarryLoad` (Task 1)
- Produces (namespace `SpiritMovers.Player` ใน Runtime):
  - `struct PlayerInputState { Vector2 Move; Vector2 Look; bool Sprint; bool Crouch; bool JumpPressed; bool InteractPressed; bool ThrowPressed; float RotateSteps; }`
  - `interface IPlayerInputSource { PlayerInputState Read(); }` · `KeyboardMouseInput(InputActionAsset)` · `ScriptedInput { PlayerInputState Next; }` (`Read()` คืน `Next` แล้วล้างค่าแบบกดครั้งเดียว: Jump/Interact/Throw/RotateSteps)
  - `PlayerMotor : NetworkBehaviour` [RequireComponent CharacterController + NetworkTransform Owner] — `static PlayerMotor Local` · `IPlayerInputSource Input` (Owner ตั้งเอง: `KeyboardMouseInput` ยกเว้น Batch Mode/บอท) · `bool InputEnabled = true` · `CarryLoad Load` (PlayerCarrier ตั้ง) · `Stamina Stamina` · `MoveResult LastMove` · `Transform Head` (กล้อง Owner ที่ความสูง 1.6 ม.; ไม่สร้างกล้องใน Batch Mode) · `Vector3 Feet` · `Vector3 Forward` (Yaw) · `float Pitch` · ขยับเฉพาะ Owner ใน `Update`
  - `DevSessionRunner : MonoBehaviour` (namespace `SpiritMovers.Dev`) — `void StartLocalHost()` (UnityTransport 127.0.0.1:7777 ไม่ใช้ UGS) · Host/Join ด้วยรหัสผ่าน `UgsSessionService` · อาร์กิวเมนต์ `-dev-role host|client -dev-profile -dev-codefile -dev-seconds -dev-scenario -dev-screenshot` (Task 6 ใช้)
  - `TestRoomFixture` (Test) — `[UnitySetUp]` โหลด `M3_TestRoom` ด้วย `EditorSceneManager.LoadSceneAsyncInPlayMode` → `StartLocalHost()` → รอ `PlayerMotor.Local` · ใส่ `ScriptedInput` · `[UnityTearDown]` `NetworkManager.Shutdown()` + ทำลาย + รอ 2 เฟรม

- [ ] **Step 1: สร้าง Input Asset, Runtime asmdef refs, PlayMode asmdef, `TestRoomFixture` และเขียน Test ที่ล้มเหลว** (`PlayerMotorTests` — เวลาจริง ใช้ช่วงยอมรับกว้าง)
```csharp
[UnityTest] WalkForwardOneSecond_MovesAboutWalkSpeed()      // Move (0,1) 1 วินาที → ระยะแนวนอน 2.4–3.3 ม.
[UnityTest] Sprint_IsFasterThanWalk()                       // 1 วินาที ระยะ ≥ 4.4 ม.
[UnityTest] Crouch_IsSlowerThanWalk()                       // 1 วินาที ระยะ 1.1–1.9 ม.
[UnityTest] SprintStopsWhenStaminaRunsOut()                 // วิ่งค้าง 6 วินาที → LastMove.Running false และ Speed 3.0
[UnityTest] Jump_LeavesGroundAndLands()                     // JumpPressed → ภายใน 0.3 วินาทีสูงขึ้น ≥ 0.3 ม. · ภายใน 1.5 วินาทีกลับพื้น (isGrounded)
[UnityTest] InputDisabled_StopsMovement()                   // InputEnabled=false + Move (0,1) 1 วินาที → ระยะ < 0.05 ม.   (Review Focus 4)
[UnityTest] CanClimbStairs()                                // เดินจากหน้าบันไดไปชั้นลอย 3 วินาที → ความสูงเท้า ≥ 1.1 ม.
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter PlayerMotorTests` → คอมไพล์ไม่ผ่าน
- [ ] **Step 3: เขียน Runtime (`PlayerInputState`, `KeyboardMouseInput`, `ScriptedInput`, `PlayerMotor`), `DevSessionRunner` (เฉพาะ `StartLocalHost` + Host/Join UI แบบ IMGUI เล็ก ๆ) และ `TestRoomSetup.CreateScene()`** — แล้วรัน `-executeMethod SpiritMovers.Editor.TestRoomSetup.CreateScene` สร้างฉาก/Prefab · ลงทะเบียน Player prefab ใน `NetworkManager.NetworkConfig.PlayerPrefab`
- [ ] **Step 4: รันให้ผ่าน** — 7/7 · และ EditMode ทั้งหมดยังผ่าน
- [ ] **Step 5: Commit** — `M3B: first-person player motor and test room`

---

### Task 4: Interaction และการถือของคนเดียว (วิธี B)

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Interaction/IInteractable.cs`, `Carryable.cs`, `PlayerCarrier.cs`, `PlayerInteractor.cs`
- Modify: `Assets/_Game/Scripts/Editor/TestRoomSetup.cs` (Prefab `LightBox` 2 กก. 0.4 ม. × 8 ชิ้น, `HeavyCrate` 25 กก. 0.8×0.6×0.6 ม. × 2 ชิ้น, Spawn โดย Host ใน `DevSessionRunner`; ลงทะเบียนใน NetworkPrefabs; Layer `Interactable`) · `Player.prefab` เพิ่ม `PlayerCarrier` + `PlayerInteractor` · `LocalizationSetup` เพิ่มคีย์ `interact.pickup` ("ยก"/"Pick up"), `interact.drop` ("วาง"/"Put down"), `interact.throw` ("โยน"/"Throw")
- Test: `Assets/_Game/Scripts/Tests/PlayMode/CarryTests.cs`

**Interfaces:**
- Consumes: `CarryRules`, `CarryConfig`, `HoldErrorWatch` (Task 2) · `PlayerMotor` (Task 3)
- Produces (namespace `SpiritMovers.Interaction`):
  - `interface IInteractable { string PromptKey { get; } bool CanInteract(PlayerCarrier by); void Interact(PlayerCarrier by); }`
  - `Carryable : NetworkBehaviour, IInteractable` [Rigidbody, NetworkObject `DontDestroyWithOwner`, NetworkTransform Owner, NetworkRigidbody] — `float Mass` (= Rigidbody.mass) · `NetworkList<ulong>` ผู้ถือ → `IReadOnlyList<ulong> Carriers`, `int CarrierCount`, `bool IsCarried` · Server: `bool TryAddCarrier(ulong clientId)` (ตรวจระยะ ≤ ReachMeters + 0.5 จากเท้าผู้เล่น, ไม่เกิน `MaxCarriersFor`, ไม่ซ้ำ), `void ReleaseCarrier(ulong clientId)`, `void Throw(ulong clientId, Vector3 direction)` · RPC: `RequestPickUpRpc`, `RequestDropRpc`, `RequestThrowRpc(Vector3 direction)`, `RequestRotateRpc(float steps)` · ผู้ถือ 1 คน → Owner = ผู้ถือ (ถ้าเป็น Host ก็ Server) · ≥2 คน → Owner = Server · `PromptKey` = `interact.pickup`
  - `PlayerCarrier : NetworkBehaviour` — `static bool TryGet(ulong clientId, out PlayerCarrier)` · `Carryable Held` · `Vector3 HoldPoint` (= `CarryRules.HoldPoint(motor.Feet, motor.Forward, held mass)`) · `float HoldYaw` · Owner: ขยับของด้วย `FollowVelocity` ใน `FixedUpdate` เมื่อเป็น Owner ของของ · ตั้ง `PlayerMotor.Load` ตาม `CarryRules.LoadFor`
  - `PlayerInteractor : NetworkBehaviour` (Owner) — SphereCast รัศมี 0.25 ระยะ `ReachMeters` จาก `Head` · `IInteractable Focus` · กด Interact: ถือของอยู่ → Drop, ไม่ถือ → `Focus.Interact` · กด Throw: ถ้า `CanThrow` → Throw · `RotateSteps` → `RequestRotateRpc` · แสดงคำแนะนำผ่าน `LocalizedThaiText` (ไม่สร้าง UI ใน Batch Mode)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (`CarryTests` ใช้ `TestRoomFixture`; ช่วยวางผู้เล่นหน้ากล่องด้วย Helper ในเทสต์)
```csharp
[UnityTest] PickUpLightBox_FollowsHoldPoint()              // Interact หน้า LightBox → 0.5 วินาทีต่อมา ระยะของ–HoldPoint < 0.4 ม., Carriers == [host]
[UnityTest] Drop_ReleasesAndFalls()                        // Interact อีกครั้ง → CarrierCount 0, 1 วินาทีต่อมาของต่ำกว่าเดิม ≥ 0.5 ม.
[UnityTest] RotateStep_TurnsItem15Degrees()                // RotateSteps 1 → Yaw ของของเปลี่ยน 15° ±3°
[UnityTest] ThrowLight_FliesForward()                      // Throw → 1 วินาทีต่อมาไกลจากผู้เล่น ≥ 3 ม.
[UnityTest] HeavyCrate_CannotThrowAndSlowsPlayer()         // ยก HeavyCrate → Throw ไม่มีผล (CarrierCount 1) · เดิน 1 วินาที LastMove.Speed ≈ 1.8
[UnityTest] InteractOutOfReach_DoesNothing()               // ห่าง 4 ม. → Focus null, Interact ไม่ทำให้ CarrierCount เปลี่ยน
[UnityTest] TryAddCarrier_LightItemSecondCarrier_IsRejected() // Server: TryAddCarrier(host) true · TryAddCarrier(99) false (เกิน MaxCarriers)   (Review Focus 2)
[UnityTest] InputDisabledWhileCarrying_KeepsItem()         // ถืออยู่ → InputEnabled=false 1 วินาที → CarrierCount 1 และของอยู่ใกล้ HoldPoint   (Review Focus 4)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter CarryTests`
- [ ] **Step 3: เขียน 4 ไฟล์ตาม Interfaces + อัปเดต `TestRoomSetup`/`LocalizationSetup` แล้วรันสร้างฉากใหม่** — `TryAddCarrier(99)` ต้องปฏิเสธเพราะเกินจำนวนผู้ถือ ก่อนตรวจระยะ (ลำดับตรวจ: ซ้ำ → เกินจำนวน → ระยะ)
- [ ] **Step 4: รันให้ผ่าน** — CarryTests 8/8 · PlayerMotorTests 7/7 · EditMode ทั้งหมด
- [ ] **Step 5: Commit** — `M3B: interaction and single-carrier carrying (method B)`

---

### Task 5: อำนาจของ Host — หลุดมือ, คนถือหลุด, ช่วยกันยก (วิธี A)

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Interaction/Carryable.cs`
- Test: `Assets/_Game/Scripts/Tests/PlayMode/CarryAuthorityTests.cs`

**Interfaces:**
- Consumes: `Carryable`, `PlayerCarrier` (Task 4) · `HoldErrorWatch`, `SharedHoldPoint` (Task 2)
- Produces:
  - Server ใน `Carryable.FixedUpdate`: ผู้ถือ 1 คน → `HoldErrorWatch.Update(distance(item, carrier.HoldPoint))` true → `ReleaseCarrier` + Log `[Carry] force drop` · ผู้ถือ ≥2 → Server ขยับด้วย `FollowVelocity(SharedHoldPoint(holdPoints))`
  - Server สมัคร `NetworkManager.OnClientDisconnectCallback` ใน `OnNetworkSpawn` ด้วยเมธอด `internal void OnClientDisconnected(ulong clientId)` → `ReleaseCarrier(clientId)` (ยกเลิกใน `OnNetworkDespawn`)
  - `ReleaseCarrier`: เหลือ 1 คน → Owner = คนนั้น · เหลือ 0 → Owner = Server · ล้าง `HoldErrorWatch`
  - Event `event Action<Carryable, ulong> ForceDropped` (Static ใน Carryable) สำหรับ Log/ผีโกรธใน M3D

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] ReleaseCarrier_ClearsCarrierAndReturnsOwnership()  // Host ถือ → Server ReleaseCarrier(host) → CarrierCount 0, OwnerClientId == ServerClientId, ของยัง Spawned   (Review Focus 1)
[UnityTest] HeldItemFarFromHand_IsForceDropped()               // ถืออยู่ → ปิด FollowVelocity ชั่วคราว (`Carryable.FollowEnabled=false` สำหรับเทสต์) + ย้ายของไป 5 ม. → ภายใน 1 วินาที CarrierCount 0 และได้ ForceDropped   (Review Focus 3)
[UnityTest] ShortGlitchFarFromHand_DoesNotDrop()               // ห่าง 3 ม. 0.2 วินาทีแล้วกลับ → ยังถือ
[UnityTest] DisconnectCallback_ReleasesThatClientOnly()        // ถือโดย host → OnClientDisconnected(99) → ยังถือ · OnClientDisconnected(host id) → ปล่อย
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter CarryAuthorityTests`
- [ ] **Step 3: แก้ `Carryable` ตาม Interfaces** — `FollowEnabled` เป็น `internal` + `InternalsVisibleTo("SpiritMovers.Tests.PlayMode")` (ไม่ใช่ API ของเกม)
- [ ] **Step 4: รันให้ผ่าน** — 4/4 + PlayMode ทั้งหมด + EditMode ทั้งหมด
- [ ] **Step 5: Commit** — `M3B: host authority for carrying - force drop, disconnect, shared carry`

---

### Task 6: บอทและการทดสอบ 2 Instance ผ่าน Relay

**Files:**
- Create: `Assets/_Game/Scripts/Dev/DevBot.cs` (ป้อน `ScriptedInput` ตามสถานการณ์) · `Tools/m3b-carry-test.ps1`
- Modify: `DevSessionRunner.cs` (สถานการณ์ + Log), `TestRoomSetup.cs` (`BuildTestRoom()` → `Builds/M3-TestRoom-v<ver>/SpiritMovers.exe` Development)

**Interfaces:**
- Consumes: ทุกอย่างจาก Task 3–5
- Produces: `-dev-scenario carry|shared|disconnect` · บรรทัด Log `[M3B] SUMMARY role=<host|client> scenario=<…> pickups=<n> carrierCountMax=<n> forceDrops=<n> itemPos=(x,y,z) ok=<true|false>` · สคริปต์พิมพ์สรุปและ Exit 1 ถ้ามี `ok=false` หรือไม่มี SUMMARY

สถานการณ์ (Host และ Client เป็นบอททั้งคู่, ฉากเดียวกัน, Relay จริง):
| สถานการณ์ | บอท | ผ่านเมื่อ |
|---|---|---|
| `carry` | Client เดินไปยก LightBox #0 ถือเดิน 5 วินาที วาง | ทั้งสองฝั่ง pickups ≥ 1 · ตำแหน่ง LightBox #0 หลังวางห่างกัน < 0.15 ม. ระหว่าง Host/Client (เทียบจาก Log) · forceDrops 0 |
| `shared` | Host และ Client ยก HeavyCrate #0 พร้อมกัน เดิน 3 วินาที | `carrierCountMax == 2` ทั้งสองฝั่ง · Crate ขยับ ≥ 1.5 ม. |
| `disconnect` | Client ยก LightBox #1 แล้วปิดเกมขณะถือ | Host: CarrierCount กลับ 0 ภายใน 3 วินาที · LightBox #1 ยัง Spawned · Owner = Server |

- [ ] **Step 1: เขียน `Tools/m3b-carry-test.ps1`** (รูปแบบเดียวกับ `Tools/proto-netsync-matrix.ps1`: Headless Host + Client, Profile ต่างกัน, Code file, Timeout + kill) — รันแล้วต้องล้มเหลวเพราะยังไม่มี Build/บอท
- [ ] **Step 2: เขียน `DevBot` + สถานการณ์ใน `DevSessionRunner` + `BuildTestRoom()`** แล้ว Build
- [ ] **Step 3: รัน `powershell -ExecutionPolicy Bypass -File Tools\m3b-carry-test.ps1`** — Expected: 3 สถานการณ์ `ok=true` ทั้ง Host/Client
- [ ] **Step 4: เอกสาร** — `docs/TESTING.md` (Test PlayMode + สคริปต์), `docs/STATUS.md` (M3B เสร็จ สถานะ `Tested`), `CHANGELOG.md`, ภาพหน้าจอห้องทดสอบ 1 ภาพใน `docs/screenshots/m3/` (รันแบบมีหน้าต่างด้วย `-dev-screenshot <PNG>` — ถ่าย 5 วินาทีก่อนปิดอัตโนมัติ แบบเดียวกับต้นแบบ M2)
- [ ] **Step 5: Commit** — `M3B: two-instance carry tests over Relay`
