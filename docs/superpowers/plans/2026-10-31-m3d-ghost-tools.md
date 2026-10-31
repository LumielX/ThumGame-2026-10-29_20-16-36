# M3D Ghost & Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ใส่ผีขี้อาย (Placeholder) เข้าไปในบ้าน Greybox ของ M3C ให้ความโกรธ 5 ขั้นมีพฤติกรรมจริง (ลอยดู → แกล้ง → ป่วน → ล่า → ไล่ออก) · ถูกจับเข้าตู้ความทรงจำแล้วเพื่อนช่วยได้ · กล้องวิญญาณส่องเห็นภาพซ้อนอำพันเฉพาะคนถือ · เสียง (`NoiseEvent`) และไหว้ผีมีผลต่อความโกรธ — ทั้งหมดเล่นผ่านเครือข่ายได้

**Architecture:** กติกาผีอยู่ใน Core (`Core/Ghost/`, ไม่ใช้ UnityEngine, ทดสอบด้วย EditMode) — Runtime มี `ShyGhost` (NetworkBehaviour, คิดบน Host เท่านั้น, Client เห็นผ่าน NetworkTransform + สถานะที่ซิงก์) ที่สั่งงานผ่าน `JobDirector` (ของ/ความโกรธ) · การนำทางใช้ NavMesh ที่ Bake ในฉากด้วย `NavMeshSurface` (`com.unity.ai.navigation` 2.0.16 มีใน manifest แล้ว) แต่ผีขยับเองแบบ Kinematic ตามมุมของ Path (ไม่ใช้ NavMeshAgent) · เสียงทุกแหล่งเข้า `NoiseHub` บน Host · ฉากเดิม `M3_TestHouse` (สร้างใหม่ด้วย `TestRoomSetup`)

**Tech Stack:** Unity 6000.3.27f1 · NGO 2.13.3 · AI Navigation 2.0.16 · URP 17.3 (Shader HLSL เขียนเอง) · Localization 1.5.13 · Unity Test Framework · `Tools/test.ps1`, Relay + DevJobBot แบบ M3C

**Spec:** `docs/GDD.md` §5.2 (ข้อ 1), §6.1–6.4, §7 (กฎกันน่ารำคาญ), §8.1 (ไหว้ผี), §8.3, §11 (Solo) · `docs/TECHNICAL_DESIGN.md` §5, §6, §7, §12 (ภาพซ้อนความทรงจำ), §13 · ภาพรวม + รายการยกมาจาก M3B/M3C: `docs/superpowers/plans/2026-10-31-m3-overview.md`

## Global Constraints
- Ghost AI ทำงาน **บน Host เท่านั้น** · Client เห็นผ่านตำแหน่ง/สถานะที่ซิงก์ · State Machine `Idle → Prank → Disturb → Hunt → (Drag) → Evict` ผูกกับขั้นความโกรธ 0–4 (TECHNICAL_DESIGN §5)
- **ขี้อาย:** ขยับ/ล่า **เฉพาะตอนไม่มีผู้เล่นคนไหนมอง** · "ถูกมอง" = ผีอยู่ในมุมกล้องผู้เล่นคนใดและไม่มีสิ่งบัง (Raycast) · Client ส่งเฉพาะทิศกล้อง Host คำนวณเอง (GDD §6.4, TECHNICAL_DESIGN §5)
- `NoiseEvent(position, loudness, source)` จากฝีเท้า ของตก/ของกระแทก (วิทยุ/เสียงไมค์ทำใน M4) · เสียงดังเพิ่มความโกรธ · ผู้เล่นทั้งหมดเงียบช่วงหนึ่งความโกรธลด (GDD §6.2) — Loudness 0..1 ตาม `MovementConfig` (เดิน 0.3 วิ่ง 1.0 ย่อ 0.05)
- ตู้ความทรงจำ: ถูกลากเข้าตู้ · เคาะตู้ให้เพื่อนรู้ · เพื่อนเปิดได้เมื่อทำของในห้องนั้นให้ถูกเพิ่ม 1 ชิ้น หรือส่องกล้องใส่ตู้ · หลุดเองหลัง ~45 วินาที แลกค่าจ้าง · Solo หลุดเร็ว ~20 วินาที และผีสงบลงหนึ่งขั้น (GDD §6.3, D-011)
- กล้องวิญญาณ: ส่อง → ภาพซ้อนสีอำพันโปร่งแสงของของในตำแหน่งเดิมช่วงสั้น ๆ **เฉพาะคนถือ** · ฟิล์มจำกัดต่องาน · ส่องเพิ่มความโกรธ · ส่งต่อเพื่อนได้ (เป็นวัตถุจริง วาง/โยนได้) · จำนวนกล้อง 1 คน = 1, 2–3 คน = 1, 4–6 คน = 2 (GDD §5.2, §8.3)
- ไหว้ผีลดความโกรธ ห้องละครั้ง (GDD §6.2, §8.1) — `AngerModel.Wai` มีแล้ว
- กฎกันน่ารำคาญ: เหตุการณ์ป่วน ~10–20 วินาที · คูลดาวน์ · เห็นสาเหตุเสมอ · ไม่ทำให้แพ้แบบไม่ยุติธรรม (GDD §7) → ผีไม่แตะของที่มีคนถือหรือของในห้องที่ผีรับแล้ว
- ไม่มีหลอดความโกรธบนจอ (D-023) — สัญญาณ = สีผี + หน้าผีบนใบสั่งงาน (มีแล้ว) + ไฟดับ
- Material/Shader ที่สคริปต์สร้าง: **สร้างครั้งเดียวแล้วเปลี่ยนค่าเท่านั้น** · MonoBehaviour ไฟล์ละคลาส · Build ห้าม `-nographics` (บันทึกใน Memory/TESTING จาก M3C)
- **การตัดสินใจประเภท C ในแผนนี้:**
  - กล้องวิญญาณถือด้วยมือ (เป็น `Carryable` เบา) — **ช่องเข็มขัด (D-021) เลื่อนไปทำพร้อมเครื่องมืออื่นใน M5/M6** เพราะ M3 มีเครื่องมือชิ้นเดียว · ผลเสีย: ถือกล้องอยู่ยกของไม่ได้
  - ผีแกล้งขั้น 1 = สลับตำแหน่งของ 2 ชิ้นในห้องที่ผีอยู่ (แบบคุณยาย GDD §6.4) · ขั้น 2 = ไฟดับเป็นช่วง + ผลักของเบา ๆ 1 ชิ้น (Greybox ไม่มีประตู จึงไม่มีประตูล็อก)
  - อยู่ในตู้ใช้มุมบุคคลที่หนึ่งจากในตู้ (บุคคลที่สามทำพร้อมตัวละครจริงใน M5)
  - หลุดเองหักค่าจ้าง `SelfReleaseCost` ต่อครั้ง (เหมือน `HintCost`)
- **นอกขอบเขต M3D:** ความโกรธจาก "วางผิด" (`AngerEvent.WrongNod` — ต้องนิยามก่อนว่าอะไรนับเป็นวางผิด, ถามผู้ใช้ตอน Playtest) · ผีหูไว/เสียงไมค์/วิทยุ (M4/M6) · Jumpscare + ตัวเลือก Accessibility (M5) · ช่องเข็มขัด, รถเข็น, กาว, ของแตก · ปลอบของถูกสิง · ภาพ/เสียงจริง · Host ออกกลางงาน
- ค่า ⚙ เริ่มต้นของแผนนี้ (อยู่ใน `GhostConfig`/`ToolConfig`/`NoiseConfig`):
  - มอง: มุมครึ่งกรวย 40° · ระยะ 25 ม. · ตรวจทุก 0.1 วินาที
  - ผี: ลอยสูง 1.2 ม. · ขั้น 0 เข้าหาผู้เล่นใกล้สุดแต่หยุดห่าง 4 ม. ที่ 1.0 ม./วินาที · ขั้น 3 ล่า 3.0 ม./วินาที · จับเมื่อห่าง ≤ 1.2 ม.
  - แกล้ง (ขั้น 1) คูลดาวน์ 20 วินาที · ป่วน (ขั้น 2) คูลดาวน์ 15 วินาที, ไฟดับ 3 วินาที (ความสว่าง × 0.15), ผลัก 2 ม./วินาที
  - ตู้: 45 วินาที (Solo 20) · ค่าหลุดเอง 0.05 ของค่าจ้างพื้นฐานต่อครั้ง · ผีไม่จับซ้ำ 5 วินาทีหลังออกจากตู้
  - กล้อง: ฟิล์ม 6 รูปต่อกล้อง · ภาพซ้อนอยู่ 3 วินาที · เห็นช่องในระยะ 12 ม. · ส่องใส่ตู้: ห่าง ≤ 6 ม. และมุม ≤ 20° · คูลดาวน์ 1 วินาที
  - เสียง: รายงานฝีเท้าทุก 0.5 วินาทีขณะเคลื่อนที่ · ของกระแทก ≥ 2 ม./วินาที → ดัง `min(1, v/6)` · "ดัง" = ≥ 0.8 → `AngerEvent.LoudNoise` ไม่เกิน 1 ครั้ง/5 วินาที · "เงียบ" = ไม่มีเสียง ≥ 0.1 ในเฟรมนั้น

## Review Focus
1. **ผู้เล่นมองผีผ่านกำแพง** — ไม่นับว่ามอง ผีขยับได้ (Task 1: `IsWatched_BlockedLineOfSight_IsNotWatched` · Task 4: `Ghost_LookingThroughWall_DoesNotFreeze`)
2. **คนที่อยู่ในตู้หลุดออกจากเกม / ทุกคนถูกจับ / เพิ่งออกจากตู้** — ตู้ว่างทันที ไม่มีสถานะค้าง · ผีไม่จับคนที่อยู่ในตู้หรือเพิ่งออก (Task 1: `PickTarget_SkipsCapturedAndImmune` · Task 5: `CaptiveDisconnects_CabinetEmpties`)
3. **ส่องกล้องตอนฟิล์มหมด / ไม่ได้ถือ / โยนทิ้งระหว่างภาพซ้อน** — ไม่มีภาพซ้อน ไม่เพิ่มความโกรธ ฟิล์มไม่ติดลบ ภาพซ้อนหายตามเวลาเสมอ (Task 6: `Peek_WithNoFilm_DoesNothing`, `Peek_NotHolder_IsRejected`, `Overlay_ClearsAfterDropMidPeek`)
4. **ผีแกล้งของที่มีคนถือ หรือของในห้องที่ผีรับแล้ว** — ห้ามแตะ (ทำให้เสียผลงานแบบไม่ยุติธรรม) (Task 1: `PickPrank_SkipsCarriedAndAccepted` · Task 4: `Prank_LeavesAcceptedRoomAlone`)
5. **ผีล่าตอนบรีฟ/พิธีส่งผี/จบงาน** — ไม่จับนอก Exploring · จบงานแล้วทุกคนออกจากตู้ (Task 4: `Ghost_OutsideExploring_DoesNotHunt` · Task 5: `JobEnd_ReleasesCaptives`)

---

### Task 1: Core — กติกาผี, เสียง, ตู้, เครื่องมือ

**Files:**
- Create: `Assets/_Game/Scripts/Core/Ghost/GhostRules.cs` (`GhostMode`, `GhostConfig`, `Viewer`, `GhostRules`), `NoiseEvent.cs` (`NoiseSource`, `NoiseEvent`, `NoiseConfig`, `LoudNoiseGate`), `CabinetRules.cs`, `ToolConfig.cs`
- Modify: `Core/Job/AngerModel.cs` (`CalmOneStage`), `Core/Job/PayoutCalculator.cs` (`selfReleases`)
- Test: `Tests/EditMode/Ghost/GhostRulesTests.cs`, `NoiseTests.cs`, `CabinetRulesTests.cs` (+ เพิ่มใน `AngerModelTests`, `PayoutCalculatorTests`)

**Interfaces:**
- Produces (namespace `SpiritMovers.Core.Ghost`, ใช้ `System.Numerics`):
  - `enum GhostMode { Idle, Prank, Disturb, Hunt, Evict }` · `static GhostMode GhostRules.ModeFor(int angerStage)` (0→Idle … 4→Evict, ค่านอกช่วงถูกจำกัด)
  - `readonly struct Viewer(ulong ClientId, Vector3 Eye, Vector3 Look)` · `static bool GhostRules.IsWatched(Vector3 ghost, IEnumerable<Viewer> viewers, GhostConfig c, Func<Vector3, Vector3, bool> clearLine)` — อยู่ในกรวย `WatchHalfAngle` และระยะ `WatchRange` และ `clearLine(eye, ghost)`
  - `readonly struct Target(ulong ClientId, Vector3 Feet, bool Captured, float ImmuneUntil)` · `static Target? GhostRules.PickTarget(Vector3 ghost, IReadOnlyList<Target> players, float now)` — ใกล้สุดที่ไม่อยู่ในตู้และพ้นเวลาคุ้มกัน
  - `readonly struct PrankCandidate(string SlotId, string RoomId, bool Carried, bool RoomAccepted)` · `static (string A, string B)? GhostRules.PickPrank(IReadOnlyList<PrankCandidate> items, string ghostRoom, Random rng)` — 2 ชิ้นต่างกันในห้องของผี ที่ไม่มีคนถือและห้องยังไม่รับ · `static string GhostRules.PickPush(IReadOnlyList<PrankCandidate> items, string ghostRoom, Random rng)` เงื่อนไขเดียวกัน 1 ชิ้น (ไม่มี → null)
  - `GhostConfig` (ค่า ⚙ ใน Global Constraints) + `IReadOnlyList<string> Validate()`
  - `enum NoiseSource { Footstep, ItemImpact, Voice, Radio }` · `readonly struct NoiseEvent(Vector3 Position, float Loudness, NoiseSource Source)` (Loudness ถูกจำกัด 0..1) · `NoiseConfig` · `LoudNoiseGate { bool TryAccept(NoiseEvent e, float now) }` · `static float NoiseConfig.ImpactLoudness(float speed)`
  - `CabinetRules`: `static float ReleaseSeconds(bool solo, GhostConfig c)` · `static bool CanFriendOpen(int correctAtCapture, int correctNow, bool cameraAimed)` (เพิ่ม ≥ 1 หรือกล้อง)
  - `ToolConfig { int FilmPerCamera = 6; float OverlaySeconds = 3; float OverlayRange = 12; ... static int SpiritCameras(int players) }` (1/1/2 ตามตาราง GDD §8.3)
  - `void AngerModel.CalmOneStage()` — ลงไปต่ำกว่าเกณฑ์ขั้นปัจจุบัน 1 หน่วย (Solo ถูกจับ) · ไม่มีผลที่ขั้น 0 หรือหลังไล่ออก
  - `PayoutCalculator.Calculate(..., int hintsUsed, int selfReleases, int playerCount, PayoutRules rules)` · `PayoutRules.SelfReleaseCost = 0.05f` (หักแบบเดียวกับ Hint, ไม่ติดลบ)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] ModeFor_MapsStagesAndClamps()                         // 0..4 → Idle, Prank, Disturb, Hunt, Evict · −1 → Idle · 9 → Evict
[Test] IsWatched_InConeAndClear_IsWatched()                  // ผีหน้าผู้เล่น 5 ม. → true
[Test] IsWatched_OutsideCone_IsNotWatched()                  // มุม 60° → false
[Test] IsWatched_BlockedLineOfSight_IsNotWatched()           // clearLine คืน false → false   (Review Focus 1)
[Test] IsWatched_AnyOneViewerIsEnough()                      // 1 ใน 3 คนมอง → true · ไม่มีใครเลย → false
[Test] PickTarget_SkipsCapturedAndImmune()                   // ใกล้สุดอยู่ในตู้/ยังคุ้มกัน → เลือกคนถัดไป · ไม่มีใคร → null   (Review Focus 2)
[Test] PickPrank_SkipsCarriedAndAccepted()                   // มีแค่ของที่ถือ/ห้องรับแล้ว → null · ได้ 2 ชิ้นต่างกันในห้องผีเท่านั้น   (Review Focus 4)
[Test] PickPrank_SameSeed_SamePair()                         // Random(5) สองครั้ง → คู่เดิม
[Test] Validate_FindsBadGhostConfig()                        // มุม ≤ 0 หรือ ≥ 90, ความเร็วติดลบ → มีปัญหา · Default → ว่าง
// NoiseTests
[Test] NoiseEvent_ClampsLoudness()                           // 1.7 → 1 · −1 → 0
[Test] LoudNoiseGate_AcceptsLoudOncePerCooldown()            // 1.0 ที่ t=0 → true · t=2 → false · t=5.1 → true · 0.5 → false เสมอ
[Test] ImpactLoudness_ScalesAndCaps()                        // 1.5 → 0 · 3 → 0.5 · 12 → 1
// CabinetRulesTests
[Test] ReleaseSeconds_SoloIsShorter()                        // 45 / 20
[Test] CanFriendOpen_NeedsOneMoreCorrectOrCamera()           // (2,2,false) false · (2,3,false) true · (2,2,true) true
// เพิ่มในไฟล์เดิม
[Test] CalmOneStage_DropsOneStage()                          // AngerModelTests: ค่า 80 (ขั้น 3) → ขั้น 2 · ขั้น 0 → ไม่เปลี่ยน
[Test] Calculate_SelfReleasesCostPay()                       // PayoutCalculatorTests: FullSuccess 300, 3 ดาว, หลุดเอง 2 → 390 − 30 = 360 · หลุดเอง 99 → 0
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter "GhostRulesTests|NoiseTests|CabinetRulesTests|AngerModelTests|PayoutCalculatorTests"` → ไม่คอมไพล์/ล้มเหลว
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** (`JobDirector.Finish` ส่ง `selfReleases: 0` ไปก่อน — Task 5 ต่อจริง)
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M3D: core ghost, noise, cabinet and tool rules`

---

### Task 2: ผู้เล่น — ซิงก์ท่าย่อ/มุมก้ม, ไหว้ผี, งานค้างจาก M3C

**Files:**
- Modify: `Runtime/Player/PlayerMotor.cs`, `PlayerInputState.cs`, `KeyboardMouseInput.cs`, `Runtime/Job/JobDirector.cs`, `Tests/PlayMode/CarryAuthorityTests.cs`
- Create: `Runtime/Player/PlayerEmote.cs`, `Tests/PlayMode/PlayerSyncTests.cs`
- Modify: `Editor/TestRoomSetup.cs` (ใส่ `PlayerEmote` ใน Player Prefab), `Editor/LocalizationSetup.cs` (`emote.wai`)

**Interfaces:**
- Produces:
  - `PlayerMotor`: NetworkVariable ที่เจ้าของเขียน `m_NetCrouch` (bool), `m_NetPitch` (float) · เครื่องอื่นปรับความสูง Collider/หัวตามท่าย่อ (รายการยกมา M3B ข้อ 8) · `Vector3 Look` (ทิศกล้องจาก Yaw + Pitch, ใช้ค่าที่ซิงก์บนเครื่องอื่น) · `bool Crouching`
  - `PlayerInputState.WaiPressed` [G] · `PlayerInputState.UsePressed` [คลิกขวา] (ใช้ใน Task 6)
  - `PlayerEmote` (NetworkBehaviour บน Player): กด G → `RequestWaiRpc()` → Host: `JobDirector.Wai(roomId ของตำแหน่งผู้เล่น)` → `bool` · `WaiRpc` ถึงทุกเครื่อง (หัวก้มชั่วคราว 0.6 วินาที — Placeholder)
  - `bool JobDirector.Wai(string roomId)` — Exploring เท่านั้น, ห้องว่าง ("") ไม่มีผล
  - งานค้าง M3C: ห้องรับเมื่อ "หยุดนิ่ง" **2 รอบประเมินติดกัน** (ข้อ 2) · `OwnershipToUnknownClient_FallsBackToServer` Assert ทันทีหลัง `SetSingleCarrierForTest` (ข้อ 4)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
// PlayerSyncTests (TestHouseFixture)
[UnityTest] Crouch_UpdatesSyncedState()                      // Input Crouch → Motor.Crouching, NetworkVariable = true · ปล่อย → false
[UnityTest] Look_FollowsPitch()                              // LookAt จุดต่ำ → Look.y < 0
[UnityTest] Wai_CalmsOncePerRoom()                           // ApplyAngerForTest(30) · กด G ในห้อง west → AngerValue ลด ≥ 9.9 (เวลาเดินเพิ่มเล็กน้อย) · กดซ้ำ → ไม่ลด
[UnityTest] Wai_OutsideExploring_DoesNothing()               // ก่อน BeginJob → false
// RoomAcceptanceTests
[UnityTest] Room_NeedsTwoSettledEvaluations()                // วางครบแล้ว EvaluateNow() ครั้งแรก → ยังไม่รับ · ครั้งที่สอง → รับ
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter "PlayerSyncTests|RoomAcceptanceTests|CarryAuthorityTests"`
- [ ] **Step 3: เขียนโค้ด** — `RoomAcceptanceTests` เดิมที่รอรับห้องใช้ `WaitAccepted` อยู่แล้ว (ผ่านเองหลังรอบที่สอง); Test ที่เรียก `EvaluateNow` ตรง ๆ ให้เรียก 2 ครั้ง
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M3D: synced crouch and look, wai emote, M3C carry-overs`

---

### Task 3: เสียง — `NoiseHub` และความโกรธจากเสียง/ความเงียบ

**Files:**
- Create: `Runtime/Ghost/NoiseHub.cs`, `Tests/PlayMode/NoiseTests.cs`
- Modify: `Runtime/Player/PlayerMotor.cs` (รายงานฝีเท้า), `Runtime/Interaction/Carryable.cs` (กระแทก), `Runtime/Job/JobDirector.cs`, `Editor/TestRoomSetup.cs`

**Interfaces:**
- Consumes: `NoiseEvent`, `NoiseConfig`, `LoudNoiseGate` (Task 1)
- Produces:
  - `NoiseHub` (MonoBehaviour บน JobDirector Prefab, Host): `void Report(NoiseEvent e)` · `event Action<NoiseEvent> Heard` · `float LastNoiseTime` (เสียง ≥ `SilenceLoudness` ล่าสุด) · `IReadOnlyList<NoiseEvent> Recent` (10 รายการล่าสุด, สำหรับ Test/Log)
  - `PlayerMotor`: เจ้าของรายงานฝีเท้าทุก `FootstepInterval` ขณะ `LastMove.Noise > 0` ผ่าน `ReportFootstepRpc(float loudness)` (Host ใช้ตำแหน่งผู้เล่นที่ Host เห็น ไม่เชื่อตำแหน่งจาก Client)
  - `Carryable.OnCollisionEnter` (Host): ความเร็วสัมพัทธ์ ≥ 2 ม./วินาที → `NoiseHub.Report(ItemImpact)`
  - `JobDirector`: `Heard` → `LoudNoiseGate` → `AngerModel.Apply(LoudNoise)` · `Tick(dt, everyoneSilent: Time.time > hub.LastNoiseTime)` แทน `false` ตายตัว

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Running_ReportsLoudFootsteps()                   // วิ่ง 1.5 วินาที → Recent มี Footstep loudness 1.0 ≥ 2 รายการ
[UnityTest] Crouching_IsQuiet()                              // ย่อเดิน 1.5 วินาที → ทุกรายการ < 0.1 และ LastNoiseTime ไม่เปลี่ยน
[UnityTest] ThrownItemImpact_IsHeard()                       // โยนกล่อง → ItemImpact > 0
[UnityTest] LoudNoise_RaisesAngerOncePerCooldown()           // BeginJob · Report(1.0) 3 ครั้งใน 1 วินาที → ความโกรธเพิ่ม 3 × ตัวคูณ ครั้งเดียว
[UnityTest] Silence_CalmsAnger()                             // ApplyAngerForTest(30) · ยืนนิ่ง 8 วินาที → AngerValue < 30
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter NoiseTests`
- [ ] **Step 3: เขียนโค้ด**
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน** (Test งานเดิมต้องยังผ่าน — บอท/Test ที่วาง `PlaceAtTarget` ไม่มีเสียงกระแทกเพราะ Teleport)
- [ ] **Step 5: Commit** — `M3D: noise events and anger from noise and silence`

---

### Task 4: ผีขี้อายบน Host — NavMesh, ถูกมองแล้วหยุด, แกล้ง, ป่วน, ล่า

**Files:**
- Create: `Runtime/Ghost/ShyGhost.cs`, `Runtime/Ghost/GhostLights.cs`, `Tests/PlayMode/GhostTests.cs`
- Modify: `Runtime/Job/JobDirector.cs`, `Editor/TestRoomSetup.cs` (Ghost Prefab, จุดเกิดผี, `NavMeshSurface` + Bake, ลงทะเบียน Prefab), `Runtime/SpiritMovers.Runtime.asmdef` (`Unity.AI.Navigation`)

**Interfaces:**
- Consumes: `GhostRules`, `GhostConfig` (Task 1) · `PlayerMotor.Look`, `Head` (Task 2) · `JobDirector.AngerStage`, `Phase`, `House`
- Produces:
  - `ShyGhost` (NetworkBehaviour, Prefab ลงทะเบียน, วางในฉากที่จุดเกิดผี): Host อัปเดตทุก `ThinkInterval` · `bool IsWatched` (NetworkVariable) · `GhostMode Mode` (NetworkVariable) · `string CurrentRoom` (Host) · `event Action<ulong> Caught` (Host — Task 5 ใช้) · ร่าง Placeholder = Capsule โปร่งแสง สีอำพัน #ffb347 → แดงตามขั้น (Material สร้างครั้งเดียว, เปลี่ยนสีด้วย MaterialPropertyBlock) · ทำงานเฉพาะ `Phase == Exploring`
  - การขยับ: `NavMesh.CalculatePath` บน Host → เดินตามมุมของ Path แบบ Kinematic ที่ความสูงลอย · ถูกมอง = หยุดนิ่ง (ทุกขั้น)
  - `bool JobDirector.SwapItems(string slotA, string slotB)` · `bool JobDirector.PushItem(string slotId, Vector3 velocity)` — Host, ปฏิเสธของที่ถือ/ห้องรับแล้ว (ตรวจซ้ำใน Runtime)
  - `GhostLights` (บนฉาก): `LightsOutRpc(float seconds)` ถึงทุกเครื่อง — ไฟหลัก × 0.15 แล้วคืน
  - Test hook: `internal void ShyGhost.ThinkNow()` · `internal void ShyGhost.SetPositionForTest(Vector3)` · `internal float ShyGhost.NextPrankAt` (ตั้งเป็น 0 เพื่อแกล้งทันที)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Ghost_OutsideExploring_DoesNotHunt()             // ก่อน BeginJob ตั้งความโกรธไม่ได้ → ผีอยู่จุดเกิด, Mode Idle   (Review Focus 5)
[UnityTest] Ghost_Watched_StaysStill()                       // ขั้น 3 · ผู้เล่น LookAt ผี → 1 วินาทีตำแหน่งเปลี่ยน < 0.05 ม. · IsWatched true
[UnityTest] Ghost_Unwatched_HuntsTowardPlayer()              // ขั้น 3 · หันหลัง → 1 วินาทีระยะถึงผู้เล่นลดลง ≥ 1.5 ม.
[UnityTest] Ghost_LookingThroughWall_DoesNotFreeze()         // ผีหลังผนังกั้นห้อง ผู้เล่นหันหาผนัง → IsWatched false และผีขยับ   (Review Focus 1)
[UnityTest] Ghost_ReachesLoftByStairs()                      // ผู้เล่นบนชั้นลอย ผีเริ่มห้อง west → ถึงภายใน 15 วินาที (NavMesh ผ่านประตู+บันได)
[UnityTest] Prank_SwapsTwoItemsInGhostRoom()                 // ขั้น 1 · หันหลัง · NextPrankAt = 0 → ของ 2 ชิ้นในห้องผีสลับตำแหน่ง
[UnityTest] Prank_LeavesAcceptedRoomAlone()                  // ห้อง west รับแล้ว ผีอยู่ west → ไม่มีของขยับ   (Review Focus 4)
[UnityTest] Disturb_PushesItemAndDimsLights()                // ขั้น 2 → GhostLights ดับ ≤ 0.2 แล้วคืนใน ~3 วินาที · ของ 1 ชิ้นมีความเร็ว
[UnityTest] Ghost_ColourFollowsStage()                       // ขั้น 0 อำพัน · ขั้น 3 แดง (อ่าน MaterialPropertyBlock)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter GhostTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่** — `-executeMethod SpiritMovers.Editor.TestRoomSetup.CreateHouseScene` (Bake NavMesh เก็บข้าง ๆ ฉาก)
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน** (Test งาน M3C ต้องผ่านเหมือนเดิม — ความโกรธขั้น 0 ผีแค่ลอยดู)
- [ ] **Step 5: Commit** — `M3D: shy ghost on the host with pranks, disturbances and hunting`

---

### Task 5: ตู้ความทรงจำ — ถูกจับ, เคาะตู้, เพื่อนช่วย, หลุดเอง

**Files:**
- Create: `Runtime/Ghost/MemoryCabinet.cs`, `Runtime/Ghost/PlayerCapture.cs`, `Tests/PlayMode/CabinetTests.cs`
- Modify: `Runtime/Ghost/ShyGhost.cs`, `Runtime/Job/JobDirector.cs` (`selfReleases` เข้า `Finish`), `Editor/TestRoomSetup.cs` (ตู้ห้องละ 1, `PlayerCapture` บน Player Prefab), `Editor/LocalizationSetup.cs` (`cabinet.open`, `cabinet.knock`, `cabinet.locked`)

**Interfaces:**
- Consumes: `ShyGhost.Caught` (Task 4) · `CabinetRules`, `AngerModel.CalmOneStage` (Task 1) · `Carryable.ReleaseAll`/`PlayerCarrier.Held`
- Produces:
  - `PlayerCapture` (บน Player): NetworkVariable `NetworkObjectReference Cabinet`, `float ReleaseAt` · `bool IsCaptured` · ขณะอยู่ในตู้: ปิดการเดิน/ยก, กด E = `KnockRpc` (ไฟตู้กะพริบทุกเครื่อง + Log `[Ghost] knock`)
  - `MemoryCabinet` (NetworkBehaviour, `IInteractable`, ห้องละตู้, `RoomId`): `bool Capture(PlayerCapture p)` (Host: ปล่อยของในมือ, ย้ายเข้าตู้, เก็บจำนวนของถูกในห้องตอนถูกจับ, Solo = มีผู้เล่นเชื่อมต่อคนเดียวตอนถูกจับ (ยังไม่มีโหมด Solo จริงจนถึง M4) → `CalmOneStage` + 20 วินาที) · `bool IsUnlocked` (NetworkVariable — `CabinetRules.CanFriendOpen`) · `void UnlockByCamera()` (Task 6 เรียก) · `Interact` จากผู้เล่นที่ไม่ได้อยู่ในตู้เมื่อปลดล็อก → ปล่อย · หมดเวลา → ปล่อยเองและนับ `SelfReleases` · ปล่อย = ย้ายไปหน้าตู้ + ผีไม่จับซ้ำ `ImmuneSeconds`
  - `int JobDirector.SelfReleases` → `PayoutCalculator` · จบงาน (ทุกแบบ) → ปล่อยทุกคน · ผู้เล่นหลุดออก → ตู้ว่าง
  - Test hook: `internal void MemoryCabinet.ExpireNow()`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Caught_PlayerIsInCabinetAndCannotMove()          // ขั้น 3 หันหลังให้ผี → IsCaptured · กดเดิน 1 วินาที ตำแหน่งไม่เปลี่ยน
[UnityTest] Caught_DropsHeldItem()                           // ถือกล่องอยู่ → หลังถูกจับ กล่องไม่มีคนถือ
[UnityTest] Cabinet_LockedUntilOneMoreItemRight()            // IsUnlocked false · PutBack ของ 1 ชิ้นในห้องตู้ → true
[UnityTest] Cabinet_SelfRelease_CountsAgainstPay()           // ExpireNow → ออกมาหน้าตู้ · SelfReleases 1 · ออกก่อนแล้วค่าจ้างลด 0.05 × ฐาน
[UnityTest] Solo_Capture_CalmsGhostAndShortensTimer()        // 1 คน · ขั้น 3 → ถูกจับแล้วขั้น 2 · ReleaseAt − ตอนจับ ≈ 20
[UnityTest] Released_IsImmuneBriefly()                       // ออกจากตู้แล้วผีขั้น 3 อยู่ติดตัว 3 วินาที → ไม่ถูกจับซ้ำ
[UnityTest] JobEnd_ReleasesCaptives()                        // ถูกจับแล้ว LeaveEarly → IsCaptured false   (Review Focus 5)
[UnityTest] CaptiveDisconnects_CabinetEmpties()              // จำลองด้วย Despawn ของ PlayerCapture → ตู้ว่าง ไม่มี Exception   (Review Focus 2)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter CabinetTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M3D: memory cabinet capture, knocking and release`

---

### Task 6: กล้องวิญญาณ — ภาพซ้อนอำพันเฉพาะคนถือ

**Files:**
- Create: `Runtime/Ghost/SpiritCamera.cs`, `Runtime/Ghost/MemoryOverlay.cs`, `Assets/_Game/Shaders/SpiritMemory.shader`, `Tests/PlayMode/SpiritCameraTests.cs`
- Modify: `Runtime/Job/JobDirector.cs` (Spawn กล้องตาม `ToolConfig.SpiritCameras` ตอน `BeginJob` ที่จุดเครื่องมือข้างรถตู้), `Runtime/Interaction/PlayerInteractor.cs` (ส่ง `UsePressed` ให้ของที่ถือที่เป็น `IUsable`), `Runtime/Interaction/IInteractable.cs` (`IUsable`), `Editor/TestRoomSetup.cs` (Prefab กล้อง + Material ภาพซ้อนสร้างครั้งเดียว + จุดเครื่องมือ), `Editor/LocalizationSetup.cs` (`camera.use` "ส่องกล้อง (เหลือ {0})", `camera.empty`)

**Interfaces:**
- Consumes: `ToolConfig` (Task 1) · `MemoryCabinet.UnlockByCamera` (Task 5) · `Carryable` (ถือ/วาง/โยน = ส่งต่อเพื่อน)
- Produces:
  - `interface IUsable { string UsePromptKey { get; } void Use(PlayerCarrier by); }` · `PlayerInteractor` เรียก `Held.Use` เมื่อ `UsePressed` และของที่ถือ implement `IUsable`
  - `SpiritCamera` (บน Prefab ที่มี `Carryable` เบา): NetworkVariable `int Film` · `Use` (เจ้าของ) → `RequestPeekRpc(Vector3 eye, Vector3 look)` → Host: ต้องเป็นคนถือคนเดียว, Exploring, Film > 0, พ้นคูลดาวน์ → Film−1, `AngerEvent.CameraUsed`, ตู้ในระยะ/มุม → `UnlockByCamera` → `PeekRpc` **ถึงผู้ส่งคนเดียว** (`RpcTarget.Single`) · `int PeeksSeen` (เครื่องนี้, Test/บอทใช้)
  - `MemoryOverlay` (Client ในเครื่อง): สร้างร่างโปร่งแสงของช่องในแผนที่ยังไม่ถูก ภายในระยะ 12 ม. ด้วย Mesh/Scale ของไอเทมนั้น + `SpiritMemory` Material · `int ActiveCount` · ลบทั้งหมดหลัง 3 วินาที (และเมื่อจบงาน)
  - `SpiritMemory.shader` — URP Unlit Transparent, สี #ffb347, Alpha 0.35, ขอบ Fresnel สว่างขึ้น, ZWrite Off (Shader ขั้นต้น, TECHNICAL_DESIGN §12)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] BeginJob_SpawnsCamerasForPlayerCount()           // 1 คน → กล้อง 1 ตัว Film 6
[UnityTest] Peek_ShowsMisplacedSlotsInRange()                // ถือกล้อง ยืนห้อง west · Use → ActiveCount = ช่องในแผนที่ยังไม่ถูกและห่าง ≤ 12 ม. · Film 5 · ความโกรธเพิ่ม CameraUsed
[UnityTest] Overlay_ClearsAfterThreeSeconds()                // 3.5 วินาทีหลังส่อง → ActiveCount 0
[UnityTest] Overlay_ClearsAfterDropMidPeek()                 // ส่องแล้ววางกล้องทันที → 3.5 วินาที ActiveCount 0   (Review Focus 3)
[UnityTest] Peek_WithNoFilm_DoesNothing()                    // Film 0 → ActiveCount 0, ความโกรธไม่เพิ่ม, Film ยัง 0   (Review Focus 3)
[UnityTest] Peek_NotHolder_IsRejected()                      // เรียก RequestPeek โดยไม่ได้ถือ → ไม่มีผล   (Review Focus 3)
[UnityTest] Peek_AtCabinet_UnlocksIt()                       // มีคนในตู้ (Capture ตรง) · ส่องใส่ตู้ห่าง 3 ม. → IsUnlocked
[UnityTest] Overlay_MaterialIsSpiritMemoryShader()           // Renderer ของภาพซ้อนใช้ Shader "SpiritMovers/SpiritMemory" (กันม่วงแดงใน Build)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter SpiritCameraTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่**
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน** · Build บ้าน (ไม่มี `-nographics`) → เปิดแบบมีหน้าต่าง ถ่ายภาพภาพซ้อน `docs/screenshots/m3/spirit-camera.png` + ภาพผีขั้น 3 `ghost-hunt.png` และ **ดูภาพจริงว่าไม่ม่วงแดง** · ค้น Log หา `is missing or no valid script`
- [ ] **Step 5: Commit** — `M3D: spirit camera with holder-only memory overlay`

---

### Task 7: ผีและเครื่องมือหลายเครื่องผ่าน Relay (เกณฑ์ M3 ครบ)

**Files:**
- Modify: `Dev/DevJobBot.cs`, `Dev/DevSessionRunner.cs`, `Runtime/Job/JobDirector.cs` (`DevSetAngerRpc` เฉพาะ Dev Build), `Tools/m3c-job-test.ps1`
- Modify: `docs/TESTING.md`, `docs/STATUS.md`, `CHANGELOG.md`, `docs/superpowers/plans/2026-10-31-m3-overview.md`

**Interfaces:**
- Produces: สถานการณ์ใหม่ `-dev-scenario ghost` (คนละอันกับ `job` ที่ต้องผ่านเหมือนเดิม):
  1. Host เริ่มงาน · Client คนแรกยกกล้อง ส่อง 1 ครั้ง (`PeeksSeen` ของ Client = 1, Host = 0 — เห็นเฉพาะคนถือ)
  2. Host ตั้งความโกรธขั้น 3 (`DevSetAngerRpc`) · Client ยืนหันหลังให้ผี → ถูกจับเข้าตู้
  3. Host วางของในห้องตู้ 1 ชิ้น (`DevPutBackRpc`) → ตู้ปลดล็อก → Host เปิดตู้ → Client ออก
  4. Host ปิดงานที่รถ (ทุกคนเดินไปรถ) → Results
  - SUMMARY เพิ่ม `captured=… released=friend|self|none peeks=… overlayOnHost=…` · ผ่านเมื่อทุกเครื่อง `ok=true` และ `captured/released/ending/teamTotal` ตรงกัน
- `m3c-job-test.ps1`: เพิ่ม `-Scenario job|ghost` (ค่าเริ่มต้น `job`) และ `-LatencyMs N` (ส่ง `-dev-latency` แบบ `m3b-carry-test.ps1`)

- [ ] **Step 1: เขียนสถานการณ์ + สคริปต์** แล้วรันกับ Build เดิม → ล้มเหลว (ยังไม่มีสถานการณ์)
- [ ] **Step 2: Build บ้าน** (ไม่มี `-nographics`)
- [ ] **Step 3: รัน** — `Tools\m3c-job-test.ps1 -Scenario ghost -Runs 5` PASS 5/5 · `-Scenario ghost -Runs 1 -Clients 3` PASS · `-Scenario ghost -Runs 2 -LatencyMs 150` PASS · `Tools\m3c-job-test.ps1 -Runs 10` (job เดิม) ยัง PASS 10/10
- [ ] **Step 4: เอกสาร** — TESTING (สถานการณ์ ghost + ผล), STATUS (M3D `Tested`, **เกณฑ์ M3 ครบทั้ง Milestone**, รอผู้ใช้ลองเล่น Prototype ตาม PROJECT_PLAN "Playtest ภายใน"), CHANGELOG, m3-overview (ผล M3D)
- [ ] **Step 5: Commit** — `M3D: ghost and tools over Relay`
