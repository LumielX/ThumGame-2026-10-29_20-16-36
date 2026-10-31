# M3C Job Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เล่นงานจัดบ้านได้ครบวงจรในบ้าน Greybox ผ่านเครือข่าย — เริ่มงาน (รื้อบ้านตาม Seed) → จัดของคืน → กริ่งผีพยักหน้า → ผีรับห้อง (สีห้องกลับมา) → จบงาน 3 แบบ → หน้าสรุปค่าจ้าง

**Architecture:** กติกาอยู่ใน Core แล้ว (M3A) — M3C ต่อเข้ากับฉากผ่าน `JobDirector` (NetworkBehaviour เดียวบน Host) ที่ถือ `JobProgress` + `AngerModel` ทำงานเฉพาะ Host แล้วซิงก์ผลด้วย NetworkVariable · บ้านกำหนดในฉากด้วย `HouseAuthoring` (ห้อง/ช่อง/จุดวาง/จุดซ่อนเป็นลูกใน Hierarchy) → สร้าง `HouseLayout` ตามลำดับ Hierarchy · Client สร้าง `JobPlan` เองจาก Seed ที่ซิงก์มาและตรวจ `LayoutHash` ให้ตรงกับ Host · ฉากใหม่ `M3_TestHouse` (ฉาก M3B ไม่เปลี่ยน เพื่อไม่ให้ Test เดิมพัง)

**Tech Stack:** Unity 6000.3.27f1 · NGO 2.13.3 · Localization 1.5.13 · URP (MaterialPropertyBlock) · Unity Test Framework · `Tools/test.ps1`, Relay + bot แบบ M3B

**Spec:** `docs/GDD.md` §4.1, §4.3, §5.1–5.4, §6.2, §16 · `docs/TECHNICAL_DESIGN.md` §3, §4, §7 · `docs/ART_BIBLE.md` §3.2 · ภาพรวม + รายการยกมาจาก M3A/M3B: `docs/superpowers/plans/2026-10-31-m3-overview.md`

## Global Constraints
- ประเมินห้อง **บน Host เท่านั้น** และเฉพาะเมื่อของในห้องหยุดนิ่ง · ผลซิงก์ด้วย NetworkVariable (TECHNICAL_DESIGN §4.3) · JobGenerator Deterministic จาก Seed ที่ Host ซิงก์ (§4.2)
- กริ่งผีพยักหน้า = จำนวนของที่วางถูก ไม่บอกว่าชิ้นไหน · เรียกบ่อยผีหงุดหงิด (GDD §5.2)
- ผีรับห้องอัตโนมัติเมื่อถูกครบเกณฑ์ → สีห้องกลับมา + ความโกรธลด (GDD §5.2, §6.2) · `Restoration` 0 → 1 ใน ~2–3 วินาที ⚙ (ART_BIBLE §3.2: ซีด = Saturation × 0.24, Value × 0.6; ห้ามทำ Material แยกสองชุด → ใช้ MaterialPropertyBlock ใน Greybox, Shader จริงใน M5)
- จบงาน: สำเร็จเต็ม (ผีรับทุกห้อง → พิธีส่งผี) · ออกก่อน (ทีมกด "ปิดงาน" ที่รถตู้) · ถูกไล่ออก (ความโกรธขั้น 4) (GDD §4.3) · ไม่มีการเสียของถาวร
- ใบสั่งงาน + Results ใช้ข้อความผ่าน Localization (TH/EN) และ ThaiShaper · ใบสั่งงานเป็น UI จอ (World-space ทำใน M5)
- **การตัดสินใจประเภท C ในแผนนี้:** บ้านกำหนดในฉาก (`HouseAuthoring`) แทน ScriptableObject `HouseDefinition` ของ TECHNICAL_DESIGN §4.1 เพราะตำแหน่งช่อง/จุดเป็นข้อมูลเชิงพื้นที่ที่วางในฉากได้ตรงกว่า — ข้อมูลไม่เชิงพื้นที่ (ผี, ระดับงาน) ยังเป็น Config/SO ภายหลัง
- **นอกขอบเขต M3C:** ผี (M3D), ตู้ความทรงจำ, กล้องวิญญาณ, กาว/ของแตก, Hint, ปลดล็อก/Save ค่าจ้าง, Host ออกกลางงาน (M4), ไหว้ผี (M3D)
- ค่า ⚙ เริ่มต้นของแผนนี้: ประเมินทุก 0.5 วินาที · "หยุดนิ่ง" = ความเร็ว < 0.05 ม./วินาที และไม่มีคนถือ · คูลดาวน์กริ่ง 5 วินาที · พิธีส่งผี 5 วินาที · Restoration 2.5 วินาที

## Review Focus
1. **ถือของไปจ่อตำแหน่งถูกแล้วยังไม่วาง** — ห้องต้องยังไม่ถูกรับ จนกว่าจะวางและของหยุดนิ่ง (Task 4: `CarriedItemAtTarget_NotAcceptedUntilPutDown`)
2. **รับห้องแล้วมีคนยกของในห้องนั้นออกไป** — ห้องที่รับแล้วไม่ถูกถอนคืน (GDD: ไม่มีการเสียถาวร) และไม่รับซ้ำ (Task 4: `AcceptedRoom_StaysAcceptedWhenItemMovedAway`)
3. **กดกริ่งรัว ๆ** — นับครั้งเดียวต่อคูลดาวน์ ไม่เพิ่มความโกรธซ้ำ (Task 5: `BellWithinCooldown_IsIgnored`)
4. **Client สร้างแผนไม่ตรงกับ Host (บ้าน/ข้อมูลต่างกัน)** — ต้องรู้ทันทีพร้อม Log ชัด ไม่เล่นต่อแบบสถานะเพี้ยน (Task 1: `ContentHash_ChangesWhenLayoutChanges` · Task 3: `PlanHashMismatch_IsReported`)
5. **กด "ปิดงาน" ตอนยังบรีฟอยู่ หรือกดซ้ำหลังจบ** — จบแบบออกก่อนได้ค่าจ้าง 0 / กดซ้ำไม่เปลี่ยนผล (Task 1: `LeaveEarly_FromBriefing_EndsWithLeftEarly` · Task 6: `LeaveTwice_KeepsFirstResult`)

---

### Task 1: Core — งานค้างจาก M3A + Hash ของบ้าน

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Job/Placement.cs`, `JobDifficulty.cs`, `AngerModel.cs`, `PayoutCalculator.cs`, `JobProgress.cs`, `JobGenerator.cs`, `HouseLayout.cs`
- Create: `Assets/_Game/Scripts/Core/Job/JobSetup.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/JobSetupTests.cs` (+ เพิ่มใน Test เดิมของแต่ละไฟล์)

**Interfaces:**
- Produces:
  - `static class JobSetup { static float AngerMultiplier(JobDifficulty d, int players); static AngerModel CreateAnger(AngerConfig c, JobDifficulty d, int players) }` (รายการยกมา M3A ข้อ 1)
  - `IReadOnlyList<string> JobDifficulty.Validate()` / `AngerConfig.Validate()` — คืนรายการปัญหา (ว่าง = ใช้ได้): Min > Max, ค่าลบ, Problems ว่าง · `Max < เกณฑ์สุดท้าย`, เกณฑ์ไม่เรียงจากน้อยไปมาก (ข้อ 4)
  - `PlacementMath.AngleDegrees` ถือ Quaternion ศูนย์เป็น Identity (ข้อ 3)
  - `JobProgress.LeaveEarly()` ใช้ได้ตั้งแต่ Briefing (ข้อ 5)
  - `PayoutCalculator.Calculate` จำกัด `hintsUsed >= 0`, `stars` 0–3 (ข้อ 7)
  - `JobGenerator`: จุดซ่อนที่นับว่าถูกตำแหน่งอยู่แล้วไม่ถูกเลือก · จุดสำรอง +1 ม. ใช้ `max(1, tolerance + 0.5)` (ข้อ 6)
  - `string HouseLayout.ContentHash()` — FNV-1a 64 บิตเป็นเลขฐาน 16 จาก id ห้อง/ช่อง/ไอเทม/ตำแหน่ง (ปัดทศนิยม 3 ตำแหน่ง, InvariantCulture)/Supports และจุดทั้งหมดตามลำดับ · `JobPlan.LayoutHash` (ตั้งโดย `Generate`) (ข้อ 2)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
// JobSetupTests
[Test] AngerMultiplier_CombinesDifficultyAndPlayers()        // Hard, 4 คน → 1.4 × 1.1 = 1.54
[Test] Validate_FindsBadRanges()                             // MinRooms 5 MaxRooms 3 → มีปัญหา ≥1 · Normal/Hard → ว่าง
[Test] AngerValidate_FindsUnreachableEviction()              // Max 90 กับเกณฑ์ {25,50,75,100} → มีปัญหา
[Test] ContentHash_IsStableAndOrderSensitive()               // สร้างซ้ำได้ค่าเดิม · สลับลำดับห้อง → ค่าต่าง
[Test] ContentHash_ChangesWhenLayoutChanges()                // เลื่อนช่องหนึ่ง 0.01 ม. → ค่าต่าง   (Review Focus 4)
[Test] Generate_SetsLayoutHash()                             // plan.LayoutHash == house.ContentHash()
// เพิ่มในไฟล์เดิม
[Test] IsCorrect_ZeroQuaternionCountsAsIdentity()            // RoomEvaluatorTests: target rotation (0,0,0,0) กับ actual Identity → true
[Test] LeaveEarly_FromBriefing_EndsWithLeftEarly()           // JobProgressTests   (Review Focus 5)
[Test] Calculate_ClampsHintsAndStars()                       // PayoutCalculatorTests: hints −5 → เหมือน 0 · stars 7 → เหมือน 3
[Test] Generate_HiddenNeverStartsAlreadyCorrect()            // JobGeneratorTests: จุดซ่อนตรงกับ target ของช่องหนึ่ง → ช่องนั้นไม่เริ่มที่จุดนั้น
[Test] Apply_AfterEviction_IsIgnored                         // AngerModelTests: แก้ให้เรียก Apply จริงด้วย Config Max 120 (ข้อ 9)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter "JobSetupTests|RoomEvaluatorTests|JobProgressTests|PayoutCalculatorTests|JobGeneratorTests|AngerModelTests"`
- [ ] **Step 3: แก้/เพิ่มโค้ดตาม Interfaces**
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M3C: core carry-overs from M3A and layout hash`

---

### Task 2: ความแข็งแรงของการยกของ (รายการยกมาจาก M3B)

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Interaction/Carryable.cs`, `PlayerInteractor.cs`, `Assets/_Game/Scripts/Dev/DevBot.cs`
- Test: `Assets/_Game/Scripts/Tests/PlayMode/CarryAuthorityTests.cs`

**Interfaces:**
- Produces:
  - Server ยกของได้เมื่อ **อยู่ในระยะ + มุมระหว่างทิศหัวกับของ ≤ 60° + Raycast จากหัวถึงของไม่ชนสิ่งอื่น** (M3B ข้อ 3)
  - `TryAddCarrier` ล้างแรงโยนที่ค้าง (M3B ข้อ 1) · `UpdateOwnership` ไม่มอบให้ client ที่ไม่อยู่ใน `ConnectedClientsIds` → ให้ Server แทน (ข้อ 2)
  - `Carryable.Interact(PlayerCarrier by)` ส่งคำขอเฉพาะเมื่อ `by.IsOwner` · `Carriers` และจุดถือไม่สร้าง List ทุก FixedUpdate (ใช้ buffer ของ instance) · `ForceDropped` ยังเป็น static แต่ DevBot ยกเลิกสมัครใน `OnDisable` แล้ว (ข้อ 7)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] TryAddCarrier_ThroughWall_IsRejected()       // วางผู้เล่นหลัง DividerSouth หันหาของที่อยู่อีกฝั่งระยะ 2 ม. → false
[UnityTest] TryAddCarrier_BehindPlayer_IsRejected()      // ของอยู่หลังผู้เล่น 1.2 ม. (หันออก 180°) → false
[UnityTest] RepickBeforeThrowApplied_DoesNotFlingLater() // Throw แล้ว TryAddCarrier ทันทีในเฟรมเดียว → ถือ 0.5 วินาทีแล้ว Drop → ความเร็วแนวนอนของของหลังปล่อย < 2 ม./วินาที
[UnityTest] OwnershipToUnknownClient_FallsBackToServer() // เรียก internal SetSingleCarrierForTest(99) → OwnerClientId == ServerClientId
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter CarryAuthorityTests`
- [ ] **Step 3: แก้ตาม Interfaces** (`SetSingleCarrierForTest` เป็น internal ใช้เฉพาะเทสต์)
- [ ] **Step 4: รัน PlayMode + EditMode ทั้งหมด และ `Tools\m3b-carry-test.ps1 -Scenarios carry,shared,remote` ให้ผ่าน** (Build ห้องทดสอบใหม่ก่อน — ไม่ใช้ `-nographics`)
- [ ] **Step 5: Commit** — `M3C: carry hardening from M3B review`

---

### Task 3: บ้าน Greybox, HouseAuthoring และเริ่มงาน (รื้อบ้าน)

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Job/HouseAuthoring.cs`, `RoomVolume.cs`, `MemorySlotMarker.cs`, `JobDirector.cs`
- Modify: `Assets/_Game/Scripts/Editor/TestRoomSetup.cs` (แยกส่วนสร้างผนัง/Prefab ให้ใช้ร่วม + `CreateHouseScene()` → `Assets/_Game/Scenes/M3_TestHouse.unity`, `BuildTestHouse()` → `Builds/M3-TestHouse-v<ver>/`), `DevSessionRunner.cs` (`-dev-seed`, `-dev-scene-job` เริ่มงานอัตโนมัติ)
- Create: `Assets/_Game/Scripts/Tests/PlayMode/TestHouseFixture.cs`, `JobStartTests.cs`

**Interfaces:**
- Consumes: `JobGenerator`, `JobPlan`, `HouseLayout`, `JobSetup` (Task 1) · `Carryable` (M3B)
- Produces (namespace `SpiritMovers.Job`):
  - `RoomVolume : MonoBehaviour` [BoxCollider trigger] — `string RoomId` · `bool Contains(Vector3 p)` (ใช้ `Bounds` ของ collider)
  - `MemorySlotMarker : MonoBehaviour` — `string SlotId` · `Carryable Item` (ของในฉากที่วางตำแหน่งถูกไว้แล้ว) · `ProblemType[] Supports` · `float PositionTolerance = 0.25f, AngleTolerance = 20f`
  - `HouseAuthoring : MonoBehaviour` — `string HouseId` · `HouseLayout BuildLayout()` (ห้องจาก `RoomVolume` ลูก, ช่องจาก `MemorySlotMarker` ใน RoomVolume นั้น, จุดวางจากลูกชื่อขึ้นต้น `Scatter`, จุดซ่อนจากลูกชื่อขึ้นต้น `Hide` — **ลำดับตาม Hierarchy** · ตำแหน่ง target = ตำแหน่งของ `Item` ตอนโหลดฉาก · Rotation ศูนย์ → Identity) · `string RoomAt(Vector3 p)` (RoomVolume แรกตามลำดับที่มี p, ไม่พบ = `""`) · `MemorySlotMarker Slot(string slotId)`
  - `JobDirector : NetworkBehaviour` (Host-authoritative) — `NetworkVariable<int> Seed` · `NetworkVariable<FixedString64Bytes> DifficultyId` · `NetworkVariable<JobPhase> Phase` · `JobPlan Plan` (ทุกเครื่องสร้างจาก Seed) · `bool PlanMatchesHost` · Server: `void BeginJob(int seed, JobDifficulty difficulty)` → สร้างแผน → ย้ายของตาม `StartPlacement` (Teleport Rigidbody, ล้างความเร็ว) → `JobProgress.Begin()` · Client: เมื่อ Seed เปลี่ยน → สร้างแผนเอง → เทียบ `LayoutHash` ที่ Host ส่งมา (`NetworkVariable<FixedString64Bytes> LayoutHash`) → ไม่ตรง: `PlanMatchesHost = false` + `Debug.LogError("[Job] plan mismatch …")`
  - `internal void ApplyHostPlan(int seed, string hostLayoutHash)` — เส้นทางเดียวกับที่ Client ใช้เมื่อได้ Seed/Hash จาก Host (เทสต์เรียกตรงได้)
  - `TestHouseFixture` (Test) — เหมือน `TestRoomFixture` แต่โหลด `M3_TestHouse` และตั้ง `LocalizationSettings.SelectedLocale` เป็น th · Helper `PutBack(string slotId)` (Server ตั้งตำแหน่ง/มุมของ Item ให้ตรง target แล้วรอ 2 FixedUpdate)
- บ้าน Greybox (ใช้ผนังเดิม): ห้อง `west` (x < 0), `east` (x > 0 ชั้นล่าง), `loft` (ชั้นลอย) · ช่อง: west 5 (LightBox ×4 + HeavyCrate), east 4 (LightBox ×3 + HeavyCrate), loft 3 (LightBox ×3) · จุดวาง 12 (4 ต่อห้อง) · จุดซ่อน 4 (มุมห้อง) · ทุกช่องรองรับ Moved/Hidden

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] BuildLayout_FollowsHierarchyOrder()        // rooms == [west, east, loft] · west slots 5 · east 4 · loft 3
[UnityTest] RoomAt_FindsRoomOrEmpty()                  // จุดกลาง west → "west" · จุดนอกบ้าน (0,0,30) → ""
[UnityTest] BeginJob_UnpacksHouse()                    // BeginJob(1, Normal) → Phase Exploring · ทุก SlotTask: Item อยู่ที่ StartPlacement (≤0.05 ม.) และ !IsCorrect กับ target
[UnityTest] BeginJob_SameSeed_SamePlan()               // BeginJob(7) → Signature เทียบกับ JobGenerator.Generate(7, layout, Normal, 1) ตรงกัน
[UnityTest] PlanHashMismatch_IsReported()              // เรียก internal ApplyHostPlan(seed, "deadbeef") → PlanMatchesHost false + LogAssert.Expect(Error, regex "plan mismatch")   (Review Focus 4)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter JobStartTests`
- [ ] **Step 3: เขียน Runtime/Job 4 ไฟล์ + `CreateHouseScene()` แล้วรันสร้างฉาก** (`-executeMethod SpiritMovers.Editor.TestRoomSetup.CreateHouseScene` — ไม่ใช้ `-nographics`)
- [ ] **Step 4: รันให้ผ่าน** — JobStartTests 5/5 + PlayMode/EditMode ทั้งหมด
- [ ] **Step 5: Commit** — `M3C: greybox house, house authoring and job start`

---

### Task 4: ประเมินห้อง ผีรับห้อง และสีห้องกลับมา

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Job/RoomRestoration.cs`
- Modify: `JobDirector.cs`
- Test: `Assets/_Game/Scripts/Tests/PlayMode/RoomAcceptanceTests.cs`

**Interfaces:**
- Consumes: `RoomEvaluator`, `SlotState`, `RoomRules`, `AngerModel` (M3A) · `HouseAuthoring` (Task 3)
- Produces:
  - `JobDirector` Server: ทุก 0.5 วินาที ประเมินทุกห้องในแผนที่ยังไม่ถูกรับ — **ข้ามห้องที่มีของในแผนถูกถือหรือเคลื่อนเร็ว ≥ 0.05 ม./วินาที** · `RoomResult.Accepted` → `JobProgress.AcceptRoom` → `AngerModel.OnRoomAccepted()` → `NetworkList<FixedString32Bytes> AcceptedRooms` · `AngerModel.Tick(dt, everyoneSilent: false)` ทุกเฟรม · `NetworkVariable<int> AngerStage` · `event Action<string> RoomAccepted` (ทุกเครื่อง เมื่อรายการเพิ่ม)
  - `int JobDirector.EvaluateNow(string roomId)` (Server) — ประเมินห้องนั้นทันที (รับห้องได้ถ้าเข้าเงื่อนไข) คืนจำนวนที่วางถูก · ห้องที่ไม่อยู่ในแผน → −1 (ใช้กับกริ่งใน Task 5)
  - `RoomRestoration : MonoBehaviour` (ลูกของ RoomVolume) — `float Value` 0..1 · `void Restore()` ไล่ 0 → 1 ใน 2.5 วินาที · ทาสี Renderer ในห้องด้วย MaterialPropertyBlock `_BaseColor` = lerp(ซีด, สี, Value) โดยซีด = HSV(S × 0.24, V × 0.6) ของสีเดิม (ART_BIBLE §3.2)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (ใช้ `TestHouseFixture`, BeginJob(seed 3, Normal))
```csharp
[UnityTest] PuttingRoomItemsBack_AcceptsRoom()              // PutBack ทุกช่องของห้องแรกในแผน → ภายใน 1.5 วินาที AcceptedRooms มีห้องนั้น
[UnityTest] AcceptedRoom_LowersAngerAndRestoresColour()     // ตั้ง anger 30 (internal ApplyAngerForTest) → รับห้อง → Value ลด 20 · 3 วินาทีต่อมา RoomRestoration.Value == 1
[UnityTest] CarriedItemAtTarget_NotAcceptedUntilPutDown()   // PutBack ทุกชิ้นยกเว้นชิ้นสุดท้ายที่ผู้เล่นถือไว้ที่ target (FollowEnabled false) → 1.5 วินาที ยังไม่รับ · ปล่อย+PutBack → รับ   (Review Focus 1)
[UnityTest] AcceptedRoom_StaysAcceptedWhenItemMovedAway()   // รับแล้วย้ายของออก 3 ม. → 1.5 วินาที ยังอยู่ใน AcceptedRooms, AcceptedRooms.Count ไม่เพิ่ม   (Review Focus 2)
[UnityTest] EvaluateUnknownRoom_DoesNothing()               // EvaluateNow("garage") == −1 และ AcceptedRooms ไม่เปลี่ยน
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter RoomAcceptanceTests`
- [ ] **Step 3: เขียนตาม Interfaces** (`ApplyAngerForTest(float)` เป็น internal)
- [ ] **Step 4: รันให้ผ่าน** — 5/5 + ทั้งหมด
- [ ] **Step 5: Commit** — `M3C: host room evaluation, acceptance and restoration`

---

### Task 5: กริ่งผีพยักหน้า

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Job/GhostBell.cs`
- Modify: `JobDirector.cs`, `TestRoomSetup.CreateHouseScene` (กริ่ง 1 อันต่อห้อง เป็น NetworkObject ในฉาก), `LocalizationSetup` (`bell.ring` "กดกริ่งเรียกผี"/"Ring for the ghost", `bell.nod` "ผีเคาะ {0} ครั้ง"/"The ghost knocked {0} times")
- Test: `Assets/_Game/Scripts/Tests/PlayMode/BellTests.cs`

**Interfaces:**
- Consumes: `IInteractable`, `PlayerCarrier` (M3B) · `JobDirector` (Task 4)
- Produces:
  - `GhostBell : NetworkBehaviour, IInteractable` — `string RoomId` · `PromptKey = "bell.ring"` · `CanInteract` = มือว่างและงานอยู่ใน Exploring · `Interact` → `RequestRingRpc` · Server: คูลดาวน์ 5 วินาที (ภายในคูลดาวน์ = ไม่ทำอะไร) → `JobDirector.EvaluateNow(RoomId)` → `AngerModel.Apply(BellRung)` → `NodClientRpc(int count)` ทุกเครื่อง → `event Action<int> Nodded` · `int LastNod` · ไฟกะพริบตามจำนวน (Light ลูก 0.3 วินาทีต่อครั้ง)
- คำว่า "ผีเคาะ N ครั้ง" แสดงบนใบสั่งงาน (Task 6) โดยดึงแม่แบบจากตาราง UI แล้ว `string.Format`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] BellRing_ReportsCorrectCountOfThatRoom()     // PutBack 2 ชิ้นของห้องแรก → กดกริ่งห้องนั้น → Nodded ได้ 2 · LastNod == 2
[UnityTest] BellRing_AddsBellAnger()                     // Anger เพิ่มประมาณ 3 × ตัวคูณ (±0.2) หลังกด
[UnityTest] BellWithinCooldown_IsIgnored()               // กด 3 ครั้งใน 1 วินาที → Nodded 1 ครั้ง, Anger เพิ่มครั้งเดียว   (Review Focus 3)
[UnityTest] BellBeforeJobStarts_CannotInteract()         // ก่อน BeginJob → CanInteract false
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter BellTests`
- [ ] **Step 3: เขียนตาม Interfaces + สร้างฉากใหม่**
- [ ] **Step 4: รันให้ผ่าน** — 4/4 + ทั้งหมด
- [ ] **Step 5: Commit** — `M3C: ghost bell nod`

---

### Task 6: จบงาน 3 แบบ ค่าจ้าง ใบสั่งงาน และ Results

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Job/VanExit.cs`, `JobSheetPanel.cs`, `ResultsPanel.cs`
- Modify: `JobDirector.cs`, `TestRoomSetup.CreateHouseScene` (รถตู้ = กล่องใหญ่หน้าบ้าน + `VanExit`; Panel ทั้งสองใน Player prefab หรือ Canvas ในฉาก), `LocalizationSetup` (คีย์ด้านล่าง)
- Test: `Assets/_Game/Scripts/Tests/PlayMode/JobEndingTests.cs`

**Interfaces:**
- Consumes: `JobProgress`, `PayoutCalculator`, `StarRating`, `JobDifficulty.BasePay` (M3A) · `JobDirector` (Task 3–5)
- Produces:
  - `JobDirector` Server: Phase Farewell → 5 วินาที → `CompleteFarewell` · Anger ขั้น 4 → `Evict` · `void LeaveEarly()` (เรียกจาก VanExit RPC) · เมื่อ Ended → คำนวณ `Payout` (stars จาก `AngerModel.HighestStage`, ของแตก 0, Hint 0, จำนวนผู้เล่น = `ConnectedClientsIds.Count`) → ซิงก์ `NetworkVariable<JobResult>` (`INetworkSerializable`: Ending, RoomsAccepted, RoomsRequired, TeamTotal, PerPlayer, Stars)
  - `VanExit : NetworkBehaviour, IInteractable` — `PromptKey = "job.leave"` · ใช้ได้ทุกเฟสก่อน Ended → `RequestLeaveRpc` → `JobDirector.LeaveEarly()`
  - `JobSheetPanel` (เจ้าของเครื่อง, กด Tab ค้างเพื่อดู — เพิ่ม Action `JobSheet` [Tab] ใน `SpiritMovers.inputactions` และ `PlayerInputState.JobSheetHeld`) — รายการห้องในแผน: ชื่อห้อง (คีย์ `room.<id>`), ตราประทับเมื่อรับแล้ว (`job.accepted`), ผลกริ่งล่าสุด (`bell.nod`) · หน้าผีตามขั้นความโกรธ (ข้อความ `ghost.mood.<0-4>`)
  - `ResultsPanel` — แสดงเมื่อ Ended: `results.title`, ผลจบ (`results.full` / `results.early` / `results.evicted`), ห้อง N/M, ค่าจ้างต่อคน, ดาว
  - คีย์ใหม่ (TH/EN): `job.leave` ปิดงาน/Close the job · `job.sheet.title` ใบสั่งงาน/Work order · `job.accepted` ผีรับแล้ว/Accepted · `room.west` ห้องนั่งเล่น/Living room · `room.east` ห้องครัว/Kitchen · `room.loft` ห้องนอนชั้นลอย/Loft bedroom · `ghost.mood.0`–`4` สงบ/หงุดหงิด/โมโห/อาละวาด/ไล่ออก · Calm/Annoyed/Angry/Raging/Evicting · `results.title` สรุปงาน/Job report · `results.full` ส่งผีสำเร็จ/Ghost at peace · `results.early` ปิดงานก่อนเสร็จ/Left early · `results.evicted` ถูกไล่ออก/Evicted · `results.rooms` ห้องที่ผีรับ {0}/{1}/Rooms accepted {0}/{1} · `results.pay` ค่าจ้างต่อคน {0}/Pay each {0} · `results.stars` รีวิว {0} ดาว/{0}-star review

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] AllRoomsAccepted_EndsFullSuccessAfterFarewell() // PutBack ทุกช่องในแผน → Phase Farewell → ภายใน 6 วินาที Ended, Result.Ending FullSuccess, TeamTotal == 300 + 3 ดาว × 30 = 390 (Normal, ผู้เล่น 1, ความโกรธไม่ถึงขั้น 3)
[UnityTest] VanLeave_EndsLeftEarlyWithProportionalPay()     // รับ 1 จาก 3 ห้อง → VanExit.Interact → Result LeftEarly, TeamTotal 100
[UnityTest] LeaveTwice_KeepsFirstResult()                   // ออก 2 ครั้ง → Result เท่าครั้งแรก   (Review Focus 5)
[UnityTest] AngerAtMax_EvictsWithHalfPay()                  // รับ 1 ห้อง → ApplyAngerForTest(200) → Ended Evicted, TeamTotal 50
[UnityTest] LeaveDuringBriefing_PaysNothing()               // ก่อน BeginJob → LeaveEarly → Ended LeftEarly, TeamTotal 0
[UnityTest] ResultsPanel_TextIsLocalizedAndShaped()        // Locale th → LeaveEarly → ResultsPanel.LastText มี ThaiShaper.Shape("สรุปงาน") และ "0/" 
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter JobEndingTests`
- [ ] **Step 3: เขียนตาม Interfaces + อัปเดตฉาก/ตาราง UI** — Panel สร้าง UI ได้ใน Batch Mode ด้วย (ไม่มีกล้องก็ยังตั้งข้อความได้) เพื่อให้เทสต์อ่าน `LastText`
- [ ] **Step 4: รันให้ผ่าน** — 6/6 + ทั้งหมด
- [ ] **Step 5: Commit** — `M3C: job endings, payout, job sheet and results`

---

### Task 7: เล่นจบงานหลายเครื่องผ่าน Relay (เกณฑ์ M3)

**Files:**
- Modify: `DevBot.cs` (สถานการณ์ `job`), `DevSessionRunner.cs`
- Create: `Tools/m3c-job-test.ps1`
- Modify: `TestRoomSetup.BuildTestHouse()`

**Interfaces:**
- Produces: สถานการณ์ `job` — Host เริ่มงาน (Seed จาก `-dev-seed`) · บอททุกเครื่องผลัดกันเรียก `DevPutBackRpc(slotId)` (Server RPC สำหรับ Dev Build เท่านั้น: วางของตรง target) ทีละช่องตามลำดับแผน เว้น 0.4 วินาที · กดกริ่งห้องละครั้ง · รอ Results · Log `[M3C] SUMMARY role=… seed=… phase=… ending=… accepted=a,b,c teamTotal=… perPlayer=… stars=… angerStage=… planOk=… ok=…`
- สคริปต์: `-Runs 10` (Seed 1..10) กับ Host + 1 Client และ `-Clients 3` อีก 1 รอบ (Host + 3 Client) · ผ่านเมื่อทุกเครื่อง `ok=true` **และ** ค่า `ending/accepted/teamTotal/stars` ของทุกเครื่องในรอบเดียวกันเท่ากัน (ไม่ Desync)

- [ ] **Step 1: เขียน `Tools/m3c-job-test.ps1`** แล้วรัน → ล้มเหลว (ยังไม่มี Build/สถานการณ์)
- [ ] **Step 2: เขียนสถานการณ์ `job` + `DevPutBackRpc` (คอมไพล์เฉพาะ `DEVELOPMENT_BUILD || UNITY_EDITOR`) + Build**
- [ ] **Step 3: รัน `Tools\m3c-job-test.ps1 -Runs 10`** — Expected: 10/10 PASS ไม่ Desync · **และ** `Tools\m3c-job-test.ps1 -Runs 1 -Clients 3` PASS
- [ ] **Step 4: เอกสาร** — `docs/TESTING.md`, `docs/STATUS.md` (M3C `Tested` + เกณฑ์ M3), `CHANGELOG.md`, ภาพหน้าจอใบสั่งงานไทยและหน้า Results ใน `docs/screenshots/m3/`
- [ ] **Step 5: Commit** — `M3C: multi-instance job runs over Relay`
