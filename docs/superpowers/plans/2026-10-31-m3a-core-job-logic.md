# M3A Core Job Logic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** กติกาของงานจัดบ้านทั้งหมด (ตรวจของวางถูก, ประเมินห้อง, ความโกรธ, สุ่มงาน, ค่าจ้าง, สถานะงาน) เป็น C# ล้วนที่ทดสอบได้ด้วย EditMode Test

**Architecture:** ทุกอย่างอยู่ใน Assembly `SpiritMovers.Core` (`noEngineReferences: true`) โฟลเดอร์ `Assets/_Game/Scripts/Core/Job/` namespace `SpiritMovers.Core.Job` · ใช้ `System.Numerics.Vector3`/`Quaternion` แทนชนิดของ Unity (Runtime แปลงเองในแผน M3C) · ค่าปรับสมดุลทั้งหมดอยู่ใน config class ที่มีค่าเริ่มต้น (`⚙` ใน GDD) ไม่ฝังในตรรกะ · ไม่มี state ร่วมแบบ static

**Tech Stack:** C# (.NET Standard 2.1 ของ Unity 6000.3.27f1) · NUnit ผ่าน Unity Test Framework 1.6.0 · รันด้วย `Tools/test.ps1`

**Spec:** `docs/GDD.md` §4.3–4.4, §5, §6.2 · `docs/TECHNICAL_DESIGN.md` §1, §4 · ภาพรวม `docs/superpowers/plans/2026-10-31-m3-overview.md`

## Global Constraints
- Core ห้ามอ้าง `UnityEngine` (asmdef `noEngineReferences: true`) และห้ามอ้างแพ็กเกจเครือข่าย/เสียง (TECHNICAL_DESIGN §2)
- JobGenerator ต้อง **Deterministic จาก Seed** — Seed เดียวกัน + อินพุตเดียวกัน = แผนเดียวกันทุกเครื่อง (TECHNICAL_DESIGN §4.2) → ใช้ `System.Random(seed)` เท่านั้น ห้าม `Guid`/เวลา/`HashSet` ที่ลำดับไม่แน่นอนในการเลือก
- ของที่ซ่อมด้วยกาวนับ **0.5** ในการรับห้อง (TECHNICAL_DESIGN §4.2, GDD §7)
- ความโกรธมี 5 ขั้น **0–4**; ขั้น 4 = ไล่ออก จบงาน (GDD §6.2)
- ค่าจ้าง: สำเร็จเต็ม = 100% + โบนัสดาว · ออกก่อน = ตามสัดส่วนห้องที่รับ · ถูกไล่ออก = สัดส่วน × 50% · หัก Hint · แบ่งเท่ากันทั้งทีม (GDD §4.3, §5.4, §12)
- ระดับงาน: ปกติ ห้อง 3, ของ/ห้อง 3–4, ความโกรธ ×1.0, ปัญหา {ถูกย้าย, ซ่อน} · ยาก ห้อง 4–5, ของ/ห้อง 5–6, ×1.4, + {ลำดับ, ถูกสิง} (GDD §4.4)
- ค่า `⚙` ที่ GDD ไม่ได้ระบุตัวเลข ใช้ค่าเริ่มต้นตามตารางนี้ (ปรับได้หลัง Playtest; เป็นการตัดสินใจประเภท C):

| ค่า | ค่าเริ่มต้น |
|---|---|
| ระยะคลาดเคลื่อนตำแหน่ง / มุม (ถ้าช่องไม่กำหนดเอง) | 0.25 ม. / 20° |
| เกณฑ์รับห้อง (`AcceptRatio`) | 0.8 ของจำนวนช่องในห้อง |
| ความโกรธ: ค่า 0–100, เกณฑ์ขั้น 1/2/3/4 | 25 / 50 / 75 / 100 |
| เพิ่ม: ของแตก / วางผิดที่แล้วเรียกผี (`WrongNod`) / เสียงดัง / ส่องกล้อง / กดกริ่ง / ต่อวินาที | +8 / +4 / +3 / +2 / +3 / +0.05 |
| ลด: ผีรับห้อง / ไหว้ (ห้องละครั้ง) / เงียบทุกคน (ต่อวินาทีหลังเงียบ 5 วินาที) | −20 / −10 / −0.5 |
| ตัวคูณตามจำนวนผู้เล่น 1 / 2–3 / 4 / 5–6: ของต่อห้อง | −1 / 0 / +1 / +1 |
| ตัวคูณตามจำนวนผู้เล่น 1 / 2–3 / 4 / 5–6: อัตราความโกรธ | ×0.8 / ×1.0 / ×1.1 / ×1.2 |
| ค่าจ้างฐาน ปกติ / ยาก | 300 / 500 |
| โบนัสดาว (เฉพาะสำเร็จเต็ม) / ค่า Hint ต่อครั้ง | +10% ฐานต่อดาว / −5% ฐาน |
| ดาว (1–3) | เริ่ม 3 · −1 ถ้าความโกรธเคยถึงขั้น 3 · −1 ถ้ามีของแตก · ต่ำสุด 1 |

## Review Focus
1. **ห้องที่ไม่มีช่องเลย / แผนที่สุ่มได้ช่องน้อยกว่าที่ขอ** — บ้านมีช่องไม่พอสำหรับระดับยาก + 5–6 คน → ต้องใช้เท่าที่มีโดยไม่ Exception และไม่วนไม่จบ (Task 3: `Generate_WhenHouseHasFewerSlotsThanRequested_UsesAllAvailable`)
2. **Quaternion กลับเครื่องหมาย (q กับ −q คือทิศเดียวกัน)** — ของที่วางตรงแต่ Physics คืนค่า −q ต้องนับว่าถูก (Task 1: `IsCorrect_WithNegatedQuaternion_IsTrue`)
3. **ความโกรธหลังถูกไล่ออก** — Event ที่มาหลังขั้น 4 (เช่นของตกตอนปลิวออกบ้าน) ต้องไม่ทำให้ค่าเปลี่ยนหรือกลับขั้น (Task 2: `Apply_AfterEviction_IsIgnored`)
4. **ไหว้ซ้ำในห้องเดิม** — ต้องลดความโกรธครั้งเดียวต่อห้อง (Task 2: `Wai_SameRoomTwice_ReducesOnce`)
5. **ค่าจ้างติดลบจาก Hint** — ใช้ Hint มากกว่าค่าจ้างที่ได้ → ต้องเป็น 0 ไม่ติดลบ และหารคนแล้วไม่ติดลบ (Task 4: `Calculate_HintsExceedPay_ClampsToZero`)

---

### Task 1: Placement และ RoomEvaluator

**Files:**
- Create: `Assets/_Game/Scripts/Core/Job/Placement.cs`, `Assets/_Game/Scripts/Core/Job/RoomEvaluator.cs`
- Modify: `Tools/test.ps1` (เพิ่ม `-Filter`)
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/RoomEvaluatorTests.cs`

**Interfaces:**
- Produces:
  - `readonly struct Placement(string roomId, Vector3 position, Quaternion rotation)` — properties `RoomId`, `Position`, `Rotation`
  - `sealed class Tolerance { float PositionMeters = 0.25f; float AngleDegrees = 20f; static Tolerance Default }`
  - `static class PlacementMath { static float AngleDegrees(Quaternion a, Quaternion b); static bool IsCorrect(Placement target, Placement actual, Tolerance tolerance) }` — ห้องต้องตรง + ระยะ ≤ tolerance + มุม ≤ tolerance
  - `enum ItemCondition { Intact, Glued }` (ของที่แตกและยังไม่ซ่อม = ไม่อยู่ในช่องใด ๆ ไม่ต้องมีค่า)
  - `readonly struct SlotState(string slotId, Placement target, Tolerance tolerance, Placement actual, ItemCondition condition)`
  - `sealed class RoomRules { float AcceptRatio = 0.8f; float GluedWeight = 0.5f; static RoomRules Default }`
  - `readonly struct RoomResult { int CorrectCount; float Score; bool Accepted }` — `CorrectCount` = จำนวนที่วางถูก (ใช้กับผีพยักหน้า นับของกาวเต็ม 1), `Score` = ผลรวมน้ำหนัก (กาว 0.5), `Accepted` = `Score >= AcceptRatio × slotCount` และ slotCount > 0
  - `static class RoomEvaluator { static RoomResult Evaluate(IReadOnlyList<SlotState> slots, RoomRules rules) }`

- [ ] **Step 1: เพิ่ม `-Filter` ใน `Tools/test.ps1`** — พารามิเตอร์ `[string]$Filter`; ถ้ามีค่าให้ส่ง `-testFilter $Filter` ให้ Unity · ตรวจด้วย `powershell -ExecutionPolicy Bypass -File Tools\test.ps1 -Filter GameVersionTests` → `total=` เท่าจำนวน Test ของ GameVersionTests เท่านั้น

- [ ] **Step 2: เขียน Test ที่ล้มเหลว** ใน `RoomEvaluatorTests.cs` (namespace `SpiritMovers.Tests`):
```csharp
// target: room "living", (1,0,2), identity
[Test] IsCorrect_WithinTolerance_IsTrue()            // actual (1.2,0,2), yaw 15° → true
[Test] IsCorrect_TooFar_IsFalse()                    // actual (1.3,0,2) → false
[Test] IsCorrect_RotatedTooMuch_IsFalse()            // yaw 25° → false
[Test] IsCorrect_WrongRoom_IsFalse()                 // same pose, room "kitchen" → false
[Test] IsCorrect_WithNegatedQuaternion_IsTrue()      // actual rotation = -identity components → true   (Review Focus 2)
[Test] Evaluate_ThreeCorrect_IsAcceptedWithCountThree()            // CorrectCount 3, Score 3.0, Accepted true
[Test] Evaluate_TwoCorrectOneGlued_IsAccepted()                    // CorrectCount 3, Score 2.5, Accepted true (2.5 >= 2.4)
[Test] Evaluate_TwoCorrectOneWrong_IsNotAccepted()                 // CorrectCount 2, Score 2.0, Accepted false
[Test] Evaluate_EmptyRoom_IsNotAccepted()                          // CorrectCount 0, Accepted false
```

- [ ] **Step 3: รัน Test ให้เห็นว่าล้มเหลว** — `powershell -ExecutionPolicy Bypass -File Tools\test.ps1 -Filter RoomEvaluatorTests` → คอมไพล์ไม่ผ่าน (`Placement` ไม่มี) ดูใน `Logs/test-EditMode.log`

- [ ] **Step 4: เขียน `Placement.cs` และ `RoomEvaluator.cs` ตาม Interfaces** — มุมระหว่าง Quaternion: `2·acos(min(1, |dot(a,b)|))` เป็นองศา (ใช้ `|dot|` จึงรองรับ q/−q)

- [ ] **Step 5: รัน Test ให้ผ่าน** — คำสั่งเดิม → `Result: Passed` 9/9

- [ ] **Step 6: Commit** — `git add Assets/_Game/Scripts/Core/Job Assets/_Game/Scripts/Tests/EditMode/Job Tools/test.ps1` → `git commit -m "M3A: placement check and room evaluator"`

---

### Task 2: AngerModel

**Files:**
- Create: `Assets/_Game/Scripts/Core/Job/AngerModel.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/AngerModelTests.cs`

**Interfaces:**
- Produces:
  - `enum AngerEvent { ItemBroken, WrongNod, LoudNoise, CameraUsed, BellRung }`
  - `sealed class AngerConfig` — ค่าเริ่มต้นตามตาราง Global Constraints: `float[] StageThresholds = {25,50,75,100}`, `float Max = 100`, `IReadOnlyDictionary<AngerEvent,float> EventAmounts`, `float PerSecond = 0.05f`, `float RoomAccepted = 20`, `float Wai = 10`, `float SilenceGraceSeconds = 5`, `float SilencePerSecond = 0.5f`, `static AngerConfig Default`
  - `sealed class AngerModel(AngerConfig config, float rateMultiplier)` — `rateMultiplier` = ระดับงาน × จำนวนผู้เล่น (คูณเฉพาะการ **เพิ่ม**)
    - `float Value`, `int Stage` (0–4), `int HighestStage`, `bool IsEvicted` (Stage == 4)
    - `event Action<int> StageChanged`
    - `void Apply(AngerEvent e)` · `void Tick(float seconds, bool everyoneSilent)` (ไม่เงียบ: เพิ่ม `PerSecond × rateMultiplier` · เงียบ: ไม่เพิ่ม และเมื่อเงียบต่อเนื่องเกิน grace ลด `SilencePerSecond` ต่อวินาทีเฉพาะส่วนที่เกิน) · `void OnRoomAccepted()` · `bool Wai(string roomId)` (คืน false ถ้าห้องนี้ไหว้แล้ว)
    - ค่าถูกจำกัด 0..Max · หลัง `IsEvicted` ทุกเมธอดไม่เปลี่ยนค่า

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] NewModel_IsCalmStageZero()                         // Value 0, Stage 0
[Test] Apply_ItemBroken_AddsEightTimesMultiplier()        // multiplier 1.4 → Value 11.2 (±0.001)
[Test] Stage_CrossesThresholds()                          // Value 24.9→0, 25→1, 50→2, 75→3 (ใช้ Apply ซ้ำ หรือ Tick)
[Test] ReachingMax_IsEvicted()                            // Apply จนถึง 100 → Stage 4, IsEvicted true
[Test] Apply_AfterEviction_IsIgnored()                    // ถึง 100 แล้ว OnRoomAccepted() → Value ยัง 100   (Review Focus 3)
[Test] Wai_SameRoomTwice_ReducesOnce()                    // Value 30 → Wai("a") true → 20 · Wai("a") false → 20 · Wai("b") → 10   (Review Focus 4)
[Test] Tick_NotSilent_AddsPerSecond()                     // Tick(10,false) → 0.5
[Test] Tick_SilentLongerThanGrace_Reduces()               // Value 30 · Tick(5,true) → 30 (ช่วง grace: ไม่เพิ่มตามเวลาและไม่ลด) · Tick(4,true) → 28 · Tick(1,false) → 28.05 และรีเซ็ตตัวนับความเงียบ
[Test] Value_NeverBelowZero()                              // OnRoomAccepted() ตอน 0 → 0
[Test] StageChanged_FiresOncePerChange()                   // ข้ามจาก 0 ไป 2 ด้วยครั้งเดียว → ได้ event 1 ครั้ง ค่า 2
[Test] HighestStage_RemembersPeak()                        // ขึ้นถึง 3 แล้วลดลง 1 → HighestStage 3
```

- [ ] **Step 2: รัน Test ให้เห็นว่าล้มเหลว** — `Tools\test.ps1 -Filter AngerModelTests` → คอมไพล์ไม่ผ่าน

- [ ] **Step 3: เขียน `AngerModel.cs`** — Stage = จำนวน threshold ที่ `Value >= threshold` · ตัวนับความเงียบสะสมข้าม `Tick` และรีเซ็ตเมื่อ `everyoneSilent == false`

- [ ] **Step 4: รัน Test ให้ผ่าน** — 11/11

- [ ] **Step 5: Commit** — `git commit -m "M3A: anger model with five stages"`

---

### Task 3: House layout, ระดับงาน และ JobGenerator

**Files:**
- Create: `Assets/_Game/Scripts/Core/Job/HouseLayout.cs`, `JobDifficulty.cs`, `PlayerCountScaling.cs`, `JobGenerator.cs` (ใน `Core/Job/`)
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/JobGeneratorTests.cs`

**Interfaces:**
- Consumes: `Placement`, `Tolerance` (Task 1)
- Produces:
  - `enum ProblemType { Moved, Hidden, Order, Possessed }`
  - `sealed class SlotDefinition(string id, string itemId, Placement target, Tolerance tolerance, IReadOnlyList<ProblemType> supports)`
  - `sealed class RoomLayout(string id, IReadOnlyList<SlotDefinition> slots)`
  - `sealed class HouseLayout(string id, IReadOnlyList<RoomLayout> rooms, IReadOnlyList<Placement> scatterPoints, IReadOnlyList<Placement> hidingSpots)`
  - `sealed class JobDifficulty { string Id; int MinRooms, MaxRooms, MinItemsPerRoom, MaxItemsPerRoom; float AngerMultiplier; IReadOnlyList<ProblemType> Problems; int BasePay; static JobDifficulty Normal, Hard }` — ค่าตาม Global Constraints
  - `static class PlayerCountScaling { static int ItemOffset(int players); static float AngerMultiplier(int players) }` — players < 1 ถือเป็น 1, > 6 ถือเป็น 6
  - `sealed class SlotTask(SlotDefinition slot, ProblemType problem, Placement startPlacement)`
  - `sealed class RoomTask(string roomId, IReadOnlyList<SlotTask> slots)`
  - `sealed class JobPlan(int seed, string houseId, string difficultyId, int playerCount, IReadOnlyList<RoomTask> rooms)`
  - `static class JobGenerator { static JobPlan Generate(int seed, HouseLayout house, JobDifficulty difficulty, int playerCount) }`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** — Fixture สร้างบ้านทดสอบ 5 ห้อง ห้องละ 8 ช่อง (ทุกช่องรองรับทุก ProblemType), scatterPoints 10 จุด (กระจายทุกห้อง), hidingSpots 6 จุด
```csharp
[Test] Generate_SameSeed_GivesIdenticalPlan()           // เทียบทุก roomId/slotId/problem/startPlacement
[Test] Generate_DifferentSeed_GivesDifferentPlan()      // seed 1 vs 2 ต่างกันอย่างน้อย 1 ช่อง
[Test] Generate_Normal_TwoPlayers_ThreeRoomsThreeToFourItems()
[Test] Generate_Hard_FourToFiveRoomsFiveToSixItems()     // players 2
[Test] Generate_Solo_RemovesOneItemPerRoom()             // Normal, players 1 → ของ/ห้อง 2–3
[Test] Generate_OnlyUsesProblemsAllowedByDifficultyAndSlot() // Normal ไม่มี Order/Possessed · ช่องที่ supports แค่ Moved ได้ Moved เท่านั้น
[Test] Generate_MovedItemsStartAwayFromTarget()          // startPlacement ของ Moved ไม่ผ่าน PlacementMath.IsCorrect กับ target
[Test] Generate_HiddenItemsStartAtHidingSpot()           // startPlacement อยู่ใน hidingSpots
[Test] Generate_NeverUsesSameSlotTwice()
[Test] Generate_WhenHouseHasFewerSlotsThanRequested_UsesAllAvailable() // บ้าน 2 ห้อง ห้องละ 2 ช่อง + Hard 6 คน → 2 ห้อง ห้องละ 2 ช่อง ไม่ Exception   (Review Focus 1)
[Test] PlayerCountScaling_ClampsOutOfRange()             // ItemOffset(0)==ItemOffset(1) · AngerMultiplier(9)==AngerMultiplier(6)
```

- [ ] **Step 2: รัน Test ให้เห็นว่าล้มเหลว** — `Tools\test.ps1 -Filter JobGeneratorTests`

- [ ] **Step 3: เขียนไฟล์ทั้ง 4 ตาม Interfaces** — ลำดับการสุ่มต้องคงที่: (1) สุ่มจำนวนห้องใน [Min,Max] จำกัดด้วยจำนวนห้องที่มีช่อง (2) สับลำดับห้องด้วย Fisher–Yates จาก `Random` เดียวกัน แล้วเลือกตามลำดับ id ของบ้าน (3) ต่อห้อง: จำนวนของ = สุ่มใน [Min,Max] + `ItemOffset` จำกัดด้วยช่องในห้อง (ขั้นต่ำ 1) (4) สับช่องแล้วเลือก (5) ปัญหา = สุ่มจากจุดตัดของ `difficulty.Problems` กับ `slot.supports` (ว่าง → ข้ามช่องนั้น) (6) ตำแหน่งเริ่ม: Hidden → hidingSpot · อื่น ๆ → scatterPoint ที่ `!IsCorrect` กับ target (ถ้าไม่มีเลย ใช้ target เลื่อน +1 ม. แกน X)

- [ ] **Step 4: รัน Test ให้ผ่าน** — 11/11

- [ ] **Step 5: Commit** — `git commit -m "M3A: deterministic job generator and difficulty tables"`

---

### Task 4: PayoutCalculator และดาวรีวิว

**Files:**
- Create: `Assets/_Game/Scripts/Core/Job/PayoutCalculator.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/PayoutCalculatorTests.cs`

**Interfaces:**
- Consumes: `JobDifficulty.BasePay` (Task 3), `AngerModel.HighestStage` (Task 2 — ส่งเป็น int)
- Produces:
  - `enum JobEnding { FullSuccess, LeftEarly, Evicted }`
  - `sealed class PayoutRules { float StarBonusPerStar = 0.10f; float HintCost = 0.05f; float EvictedFactor = 0.5f; static PayoutRules Default }`
  - `static class StarRating { static int Calculate(int highestAngerStage, int itemsBroken) }` — ตามตาราง Global Constraints
  - `readonly struct Payout { int TeamTotal; int PerPlayer; int Stars }`
  - `static class PayoutCalculator { static Payout Calculate(JobEnding ending, int basePay, int roomsAccepted, int roomsRequired, int stars, int hintsUsed, int playerCount, PayoutRules rules) }` — `Stars` ในผล = 0 ถ้าไม่ใช่ FullSuccess · ปัดลงเป็นจำนวนเต็ม · `PerPlayer = TeamTotal / max(1, playerCount)`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] FullSuccess_ThreeStars_PaysBasePlusThirtyPercent()      // base 300, 3/3, stars 3, hints 0, players 2 → TeamTotal 390, PerPlayer 195, Stars 3
[Test] LeftEarly_PaysProportionOfAcceptedRooms()               // base 300, 2/3 → 200 · Stars 0
[Test] Evicted_PaysHalfOfProportion()                          // base 300, 2/3 → 100
[Test] Hints_ReduceTeamTotal()                                 // FullSuccess stars 1, hints 2 → 300+30-30 = 300
[Test] Calculate_HintsExceedPay_ClampsToZero()                 // LeftEarly 0/3, hints 3 → TeamTotal 0, PerPlayer 0   (Review Focus 5)
[Test] PerPlayer_RoundsDown()                                  // TeamTotal 100, players 3 → 33
[Test] RoomsRequiredZero_PaysNothingWithoutThrowing()          // LeftEarly 0/0 → 0
[Test] StarRating_FollowsRules()                               // (2,0)→3 · (3,0)→2 · (3,2)→1 · (4,5)→1
```

- [ ] **Step 2: รัน Test ให้เห็นว่าล้มเหลว** — `Tools\test.ps1 -Filter PayoutCalculatorTests`

- [ ] **Step 3: เขียน `PayoutCalculator.cs` ตาม Interfaces**

- [ ] **Step 4: รัน Test ให้ผ่าน** — 8/8

- [ ] **Step 5: Commit** — `git commit -m "M3A: payout calculator and star rating"`

---

### Task 5: JobProgress (สถานะงาน)

**Files:**
- Create: `Assets/_Game/Scripts/Core/Job/JobProgress.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Job/JobProgressTests.cs`

**Interfaces:**
- Consumes: `JobPlan` (Task 3), `JobEnding` (Task 4)
- Produces:
  - `enum JobPhase { Briefing, Exploring, Farewell, Ended }` (TECHNICAL_DESIGN §3)
  - `sealed class JobProgress(JobPlan plan)`
    - `JobPhase Phase` · `JobEnding? Ending` · `IReadOnlyCollection<string> AcceptedRooms` · `int RoomsRequired` (= `plan.Rooms.Count`)
    - `event Action<JobPhase> PhaseChanged` · `event Action<string> RoomAccepted`
    - `void Begin()` Briefing→Exploring · `bool AcceptRoom(string roomId)` (เฉพาะห้องในแผน, ครั้งเดียวต่อห้อง, เฉพาะตอน Exploring; ห้องสุดท้าย → Farewell) · `void CompleteFarewell()` Farewell→Ended(FullSuccess) · `void LeaveEarly()` Exploring/Farewell→Ended(LeftEarly) · `void Evict()` Exploring/Farewell→Ended(Evicted)
    - คำสั่งที่ไม่ถูกต่อ Phase ปัจจุบัน = ไม่ทำอะไร (ไม่ Exception) — Host อาจได้ RPC ซ้ำ/ช้า

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** — แผนทดสอบ 3 ห้อง "a","b","c" (สร้าง `JobPlan` ตรงด้วย RoomTask ว่าง)
```csharp
[Test] NewProgress_IsBriefing()
[Test] Begin_MovesToExploring()
[Test] AcceptRoom_UnknownOrDuplicate_ReturnsFalse()          // "x" false · "a" true · "a" false · AcceptedRooms.Count 1
[Test] AcceptRoom_BeforeBegin_ReturnsFalse()
[Test] AcceptingAllRooms_MovesToFarewell()                    // a,b,c → Phase Farewell
[Test] CompleteFarewell_EndsWithFullSuccess()
[Test] LeaveEarly_EndsWithLeftEarlyKeepingAcceptedRooms()     // accept a → LeaveEarly → Ended, Ending LeftEarly, AcceptedRooms {a}
[Test] Evict_EndsWithEvicted()
[Test] CommandsAfterEnd_AreIgnored()                          // Evict แล้ว LeaveEarly/AcceptRoom/Begin → ยัง Evicted, ไม่มี PhaseChanged เพิ่ม
[Test] PhaseChanged_FiresForEachTransition()                  // Begin + 3 accept + CompleteFarewell → [Exploring, Farewell, Ended]
```

- [ ] **Step 2: รัน Test ให้เห็นว่าล้มเหลว** — `Tools\test.ps1 -Filter JobProgressTests`

- [ ] **Step 3: เขียน `JobProgress.cs` ตาม Interfaces**

- [ ] **Step 4: รัน Test ทั้งหมดให้ผ่าน** — `Tools\test.ps1` (ไม่ใส่ Filter) → ผ่านทั้งหมด (33 เดิม + 49 ใหม่ = 82)

- [ ] **Step 5: อัปเดตเอกสาร** — `docs/TESTING.md` ตาราง "Test ที่มีตอนนี้" เพิ่ม 5 ชุด · `docs/STATUS.md` บันทึก M3A เสร็จ · `CHANGELOG.md` เพิ่มบรรทัด Core job logic

- [ ] **Step 6: Commit** — `git commit -m "M3A: job progress state machine"`
