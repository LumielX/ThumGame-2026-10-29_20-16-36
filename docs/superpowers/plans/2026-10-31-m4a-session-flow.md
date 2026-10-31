# M4A Session Flow & Lobby Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เดินทางของผู้เล่นครบวง — เมนูหลัก → สร้างห้อง (ได้รหัส) / ใส่รหัสเข้า / เล่นคนเดียว → Lobby ในรถตู้ (Ready, Host เลือกงาน) → งาน → Results → กลับ Lobby แล้วเล่นงานใหม่ได้ · ตรวจเวอร์ชัน · เข้าได้เฉพาะใน Lobby · ผู้เล่นหลุด/Host ออกมีข้อความชัดและได้ค่าจ้างของห้องที่ผีรับแล้ว

**Architecture:** กติกา (เข้าห้องได้ไหม, เริ่มงานได้ไหม, ป้ายจำนวนคน, ค่าจ้างตอน Host ออก) อยู่ใน Core (`Core/Session/`, EditMode) · Runtime มี `GameSession` (DontDestroyOnLoad, ถือ NetworkManager ตัวเดียวทั้งเกม + `UgsSessionService`) และฉากผลิตจริง 3 ฉาก `MainMenu` → `Lobby` → `Job_Greybox` ที่สลับด้วย NGO SceneManager (Host สั่ง Client ตาม) · ฉากทดสอบเดิม (`M3_TestRoom`, `M3_TestHouse` ที่มี NetworkManager ของตัวเอง) **ไม่เปลี่ยน** เพื่อให้ Test/บอทเดิมใช้ได้ — `Job_Greybox` สร้างจากตัวสร้างบ้านเดียวกันแต่ไม่มี NetworkManager/DevSessionRunner

**Tech Stack:** Unity 6000.3.27f1 · NGO 2.13.3 (SceneManager, ConnectionApproval) · Multiplayer Services 2.4.1 (Sessions + Relay) · Localization 1.5.13 + `LocalizedThaiText` · UGUI/TMP · Unity Test Framework · `Tools/test.ps1`, Relay + บอทแบบ M3

**Spec:** `docs/GDD.md` §9, §11, §16 · `docs/TECHNICAL_DESIGN.md` §3, §7, §14 · ภาพรวม + ข้อค้าง: `docs/superpowers/plans/2026-10-31-m4-overview.md`

## Global Constraints
- ไหลของผู้เล่น: เมนูหลัก → สร้างห้อง (ได้รหัส) / ใส่รหัสเข้าห้อง → Lobby ในรถตู้ (Ready, Host เลือกงาน) → งาน → Results → กลับ Lobby (GDD §9, TECHNICAL_DESIGN §3)
- **ตรวจเวอร์ชันตอนเข้าห้อง** — ไม่ตรงแจ้งให้อัปเดต (มี `VersionMismatchException` แล้วใน `UgsSessionService`) · ข้อความที่ผู้เล่นเห็นต้องแปลแล้ว (TECHNICAL_DESIGN §7)
- **เข้ากลางงานไม่ได้** (เข้าได้เฉพาะใน Lobby) · **ผู้เล่นหลุด:** งานดำเนินต่อ ของที่ถือหล่น (มีแล้ว) · กลับเข้าได้เมื่อทีมกลับ Lobby (GDD §9)
- **Host ออก:** จบงาน ทุกคนกลับเมนูพร้อมข้อความชัดเจน และ **ได้ค่าจ้างของห้องที่ผีรับแล้ว** (GDD §9) — M4A คำนวณและแสดง · บันทึกลง Save ใน M4B
- **จำนวนผู้เล่น:** ออกแบบ 6 คน · รับรอง 1–4 · 5–6 คน "ทดลอง" มีป้ายใน Lobby (GDD §9, D-026)
- Solo = เส้นทางเดียวกับ Host ที่ไม่เปิดรับคนอื่น (TECHNICAL_DESIGN §3)
- ข้อความทุกอันผ่าน Localization (TH/EN) + ThaiShaper · UI = UGUI Screen-space ขั้นต้น (ธีมกระดาษทำใน M5)
- Build ห้าม `-nographics` · Material สร้างครั้งเดียว · MonoBehaviour ไฟล์ละคลาส (Memory/TESTING)
- **การตัดสินใจประเภท C ในแผนนี้:**
  - **Solo ใช้ Host ในเครื่อง (UnityTransport ตรง, ไม่ผ่าน UGS/Relay)** — เล่นคนเดียวได้แม้ไม่มีอินเทอร์เน็ต และไม่เปลือง Free Tier · เส้นทางในเกมหลังจากนั้นเหมือน Host ทุกอย่าง
  - **เลือกงานใน Lobby:** บ้านเดียว (Greybox) · ระดับ "ปกติ" เท่านั้น ("ยาก" แสดงล็อกไว้จนมีระบบยศใน M6, GDD §12) · Seed สุ่มบน Host
  - **กลับ Lobby หลัง Results:** นับถอยหลัง 10 วินาที ⚙ (Host กด Enter ไปทันทีได้)
  - **โหลดฉากงานใหม่ทุกงาน** = บ้านรีเซ็ต (ปิดข้อค้าง M3C ข้อเล็ก 5)
  - ชื่อผู้เล่นใน Lobby = "ผู้เล่น N" ไปก่อน (ตั้งชื่อเองใน M4B)
- **นอกขอบเขต M4A:** Save/Settings (M4B) · Voice/แชต/Ping (M4C) · ปุ่มรายงานบั๊ก/Checklist (M4D) · รถตู้เดินได้แบบสวย/กระดานงานจริง (M5) · ร้านแต่งตัว/ยศ (M6)
- ค่า ⚙ เริ่มต้น: MaxPlayers 6 · นับถอยหลังเริ่มงาน 3 วินาที · Results → Lobby 10 วินาที · Ready กด R

## Review Focus
1. **Client ใส่รหัสผิด/ว่าง/ตัวเล็ก/มีช่องว่าง หรือ Relay ล้ม** — ข้อความแปลแล้ว กลับมากดใหม่ได้ ไม่ค้างที่ "กำลังเชื่อมต่อ" (Task 3: `Join_EmptyCode_ShowsErrorWithoutConnecting`, `Join_ServiceFailure_ShowsErrorAndReenablesButtons`)
2. **Host เริ่มงานตอนมีคนยังไม่ Ready หรือมีคนหลุดระหว่างนับถอยหลัง** — ไม่เริ่ม / นับใหม่ (Task 1: `CanStart_NeedsEveryoneReady` · Task 4: `Countdown_CancelsWhenSomeoneUnreadies`)
3. **เข้าห้องระหว่างงาน หรือห้องเต็ม** — ถูกปฏิเสธพร้อมเหตุผลที่แปลแล้ว Host ไม่สะดุด (Task 1: `CanJoin_*` · Task 7: Relay ขั้น "เข้าระหว่างงาน")
4. **Host ออกกลางงาน / ตอน Results / ใน Lobby** — Client ทุกคนกลับเมนูพร้อมข้อความที่ตรงกับช่วงนั้น ค่าจ้างเฉพาะห้องที่รับแล้ว (Task 1: `HostLeftPay_*` · Task 6: `HostLeft_MessageMatchesPhase` · Task 7)
5. **เล่นงานที่สองใน Session เดิม** — บ้าน ผี กล้อง ตู้ ความโกรธ เริ่มใหม่หมด ไม่มีของค้างจากงานแรก (Task 5: `SecondJob_StartsFresh`)

---

### Task 1: Core — กติกา Session/Lobby

**Files:**
- Create: `Assets/_Game/Scripts/Core/Session/SessionRules.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/Session/SessionRulesTests.cs`

**Interfaces:**
- Produces (namespace `SpiritMovers.Core.Session`):
  - `enum SessionPhase { Lobby, Loading, Job, Results }`
  - `enum JoinRefusal { None, VersionMismatch, JobInProgress, Full }` · `static JoinRefusal SessionRules.CanJoin(SessionPhase phase, int players, int maxPlayers, bool versionMatches)` (ลำดับตรวจ: เวอร์ชัน → เต็ม → กำลังทำงาน)
  - `static bool SessionRules.CanStart(IReadOnlyCollection<bool> ready)` — ทุกคน Ready และมีอย่างน้อย 1 คน
  - `enum CrewSize { Supported, Experimental, TooMany }` · `static CrewSize SessionRules.SizeLabel(int players)` (1–4 Supported, 5–6 Experimental, >6 TooMany)
  - `static int SessionRules.HostLeftPay(SessionPhase phase, int roomsAccepted, int roomsRequired, int basePay, int players)` — เฉพาะ `Job`/`Results`: ใช้ `PayoutCalculator.Calculate(JobEnding.LeftEarly, …, stars 0, hints 0, selfReleases 0, players)` แล้วคืน `PerPlayer` · Lobby/Loading → 0
  - `static string SessionRules.NormalizeCode(string raw)` — ตัดช่องว่างทุกตัว, ตัวพิมพ์ใหญ่ · คืน `null` ถ้าว่างหรือมีอักขระนอก A–Z 0–9

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] CanJoin_VersionCheckedFirst()          // (Job, 7, 6, false) → VersionMismatch
[Test] CanJoin_FullBeforePhase()              // (Job, 6, 6, true) → Full
[Test] CanJoin_OnlyInLobby()                  // Lobby → None · Loading/Job/Results → JobInProgress   (Review Focus 3)
[Test] CanStart_NeedsEveryoneReady()          // {true,true} true · {true,false} false · {} false   (Review Focus 2)
[Test] SizeLabel_SupportedExperimentalTooMany() // 1,4 → Supported · 5,6 → Experimental · 7 → TooMany
[Test] HostLeftPay_PaysAcceptedRoomsShare()   // Job, 1 ใน 3 ห้อง, ฐาน 300, 2 คน → 50   (Review Focus 4)
[Test] HostLeftPay_NothingOutsideJob()        // Lobby/Loading → 0 · Results 3/3 → 150 (300 / 2 คน, ไม่มีดาว)
[Test] NormalizeCode_TrimsAndUppercases()     // " ab c1 \n" → "ABC1" · "" / "   " / "AB-1" → null   (Review Focus 1)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter SessionRulesTests` → ไม่คอมไพล์
- [ ] **Step 3: เขียนโค้ดตาม Interfaces**
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4A: core session and lobby rules`

---

### Task 2: `GameSession` + ฉากผลิตจริง 3 ฉาก

**Files:**
- Create: `Runtime/Session/GameSession.cs`, `Runtime/Session/SessionErrors.cs` (static: เหตุผล → Localization key), `Editor/GameScenesSetup.cs`, `Tests/PlayMode/GameSessionTests.cs`
- Modify: `Runtime/Bootstrap.cs` (โหลด `MainMenu` หลังรายงานเวอร์ชัน), `Editor/TestRoomSetup.cs` (แยกเมธอดสร้างเนื้อบ้านเพื่อใช้ซ้ำกับ `Job_Greybox`), `Runtime/SpiritMovers.Runtime.asmdef` (+`SpiritMovers.Net`), `Editor/LocalizationSetup.cs`

**Interfaces:**
- Consumes: `UgsSessionService.HostAsync/JoinAsync/LeaveAsync`, `VersionMismatchException` · `SessionRules` (Task 1)
- Produces:
  - `GameSession` (MonoBehaviour, DontDestroyOnLoad, มีตัวเดียว: `static GameSession Instance`) บน Prefab `GameNetwork` (NetworkManager + UnityTransport + `NetworkPrefabsList` เดียวกับฉากทดสอบ, `EnableSceneManagement = true`, ConnectionApproval เปิด)
    - `Task<string> HostAsync()` (Relay, MaxPlayers 6, คืนรหัส) · `Task JoinAsync(string code)` · `void StartSolo()` (UnityTransport `127.0.0.1` port 7777, StartHost) · `Task LeaveAsync()` (ShutDown + ออก Session + โหลด `MainMenu`)
    - `string JoinCode` · `bool IsSolo` · `SessionPhase Phase` — **คำนวณเองในทุกเครื่อง** จากฉากที่ทำงาน: `Lobby` → Lobby (หลังนับถอยหลังจบ → Loading ผ่าน `MarkLoading()`), `Job_Greybox` → Job หรือ Results เมื่อ `JobDirector.Result.IsSet` · ไม่ซิงก์ (ทุกเครื่องเห็นฉากเดียวกัน) · `event Action<string> Failed` (คืน Localization key) · `static string PendingMessageKey` + `static int PendingPay` (ข้อความ/ค่าจ้างที่เมนูหลักแสดงตอนกลับมา)
    - เมื่อ Host สร้างห้อง/เริ่ม Solo สำเร็จ → `NetworkManager.SceneManager.LoadScene("Lobby", Single)`
  - ฉาก `MainMenu`, `Lobby` (รถตู้ Greybox: พื้น + ผนัง + จุดเกิด 6 จุด + กระดานงาน), `Job_Greybox` (เนื้อบ้านเหมือน `M3_TestHouse` ไม่มี NetworkManager/DevSessionRunner) · Build Settings: `Boot`, `MainMenu`, `Lobby`, `Job_Greybox` (ฉากทดสอบอยู่นอก Build หลัก — `BuildTestRoom/BuildTestHouse` ยัง Build แยกได้)
  - `GameScenesSetup.CreateGameScenes()` (`-executeMethod`)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Fixture ใหม่ `GameFlowFixture`: โหลด `Boot` → รอ `MainMenu`)
```csharp
[UnityTest] Boot_LoadsMainMenu()                      // ฉากที่ทำงานคือ MainMenu ภายใน 5 วินาที · มี GameSession ตัวเดียว
[UnityTest] StartSolo_LoadsLobbyAndSpawnsPlayer()     // StartSolo → ฉาก Lobby · PlayerMotor.Local อยู่ที่จุดเกิดของรถตู้
[UnityTest] Leave_ReturnsToMainMenuWithCursorFree()   // ใน Lobby → LeaveAsync → MainMenu · NetworkManager ไม่ทำงาน · Cursor.lockState None (ข้อค้าง M3B ข้อ 5)
[UnityTest] GameSession_SurvivesSceneChanges()        // Instance เดิมหลัง MainMenu → Lobby → MainMenu
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter GameSessionTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉาก** — `-executeMethod SpiritMovers.Editor.GameScenesSetup.CreateGameScenes` · ตรวจด้วย Test ว่า NGO ย้ายผู้เล่นข้ามฉากอย่างไร (ผู้เล่นคงอยู่หรือ Spawn ใหม่) แล้ว **ลงบันทึก Ruling** ตามที่พบ
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4A: game session and production scenes`

---

### Task 3: เมนูหลัก

**Files:**
- Create: `Runtime/Session/MainMenuPanel.cs`, `Tests/PlayMode/MainMenuTests.cs`
- Modify: `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`

**Interfaces:**
- Consumes: `GameSession` (Task 2), `SessionRules.NormalizeCode`, `SessionErrors`
- Produces:
  - `MainMenuPanel` (UGUI + `LocalizedThaiText`): ปุ่ม `menu.host` "สร้างห้อง" · ช่องรหัส + `menu.join` "เข้าห้อง" · `menu.solo` "เล่นคนเดียว" · `menu.settings` "ตั้งค่า" (ปิดไว้จน M4B) · `menu.quit` "ออกจากเกม" · บรรทัดสถานะ/ข้อผิดพลาด
  - `internal void ClickHost() / ClickJoin(string code) / ClickSolo()` (Test เรียกตรง) · `string StatusKey` (คีย์ล่าสุดที่แสดง) · `bool ButtonsInteractable`
  - ระหว่างเชื่อมต่อ: ปิดปุ่มทั้งหมด + สถานะ `menu.connecting` · ล้มเหลว → เปิดปุ่มคืน + ข้อความจาก `SessionErrors` (`error.version` "เวอร์ชันไม่ตรงกับห้อง (ของคุณ {0} · ห้อง {1}) — อัปเดตเกม", `error.code` "รหัสห้องไม่ถูกต้อง", `error.join` "เข้าห้องไม่ได้ — ตรวจรหัสหรืออินเทอร์เน็ต", `error.in_job` "ห้องนี้กำลังทำงานอยู่ เข้าได้ตอนทีมกลับรถตู้", `error.full` "ห้องเต็มแล้ว")
  - เปิดเมนูแล้วถ้ามี `GameSession.PendingMessageKey` → แสดงข้อความนั้น (+ค่าจ้าง) แล้วล้าง
  - Test hook: `GameSession.ServiceOverride` (interface `ISessionService { HostAsync, JoinAsync, LeaveAsync }`) เพื่อจำลอง UGS ล้มใน Test โดยไม่ต่อเน็ต

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Join_EmptyCode_ShowsErrorWithoutConnecting()          // ClickJoin("  ") → StatusKey "error.code", ServiceOverride ไม่ถูกเรียก   (Review Focus 1)
[UnityTest] Join_ServiceFailure_ShowsErrorAndReenablesButtons()   // Override โยน Exception → "error.join" · ButtonsInteractable true   (Review Focus 1)
[UnityTest] Join_VersionMismatch_ShowsBothVersions()              // Override โยน VersionMismatchException(0.0.1, "0.0.2") → ข้อความที่แสดงมี "0.0.1" และ "0.0.2"
[UnityTest] Connecting_DisablesButtons()                          // Override ค้างไว้ (TaskCompletionSource) → ButtonsInteractable false, StatusKey "menu.connecting"
[UnityTest] Solo_GoesToLobby()                                    // ClickSolo → ฉาก Lobby
[UnityTest] PendingMessage_ShownOnceOnMenu()                      // ตั้ง PendingMessageKey "session.host_left_job" + PendingPay 50 → เปิดเมนู → ข้อความมี "50" · กลับมาอีกครั้ง → ไม่มี
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter MainMenuTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่ + `LocalizationSetup.Setup`**
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4A: main menu with join, host and solo`

---

### Task 4: Lobby ในรถตู้ — Ready, เลือกงาน, เริ่มงาน, เข้าได้เฉพาะ Lobby

**Files:**
- Create: `Runtime/Session/LobbyState.cs` (NetworkBehaviour ในฉาก Lobby — Prefab ลงทะเบียน), `Runtime/Session/LobbyPanel.cs`, `Runtime/Session/LobbyGate.cs` (Connection Approval บน `GameNetwork`), `Tests/PlayMode/LobbyTests.cs`
- Modify: `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`, `PlayerInputState.cs`/`KeyboardMouseInput.cs`/`.inputactions` (`ReadyPressed` [R], `ConfirmPressed` [Enter])

**Interfaces:**
- Consumes: `SessionRules.CanStart`, `SizeLabel`, `CanJoin` (Task 1) · `GameSession` (Task 2)
- Produces:
  - `LobbyState`: `NetworkList<LobbyMember>` (`ulong ClientId`, `bool Ready`, `int Number`) · `NetworkVariable<JobChoice>` (`FixedString32Bytes DifficultyId`) · `NetworkVariable<float> CountdownEndsAt` (−1 = ไม่ได้นับ)
  - ผู้เล่นกด R → `ToggleReadyRpc` · Host กด Enter ที่ Lobby → `TryStartCountdown()` (ต้อง `CanStart`) · ใครถอน Ready/หลุดระหว่างนับ → ยกเลิก · หมดเวลา → `GameSession.MarkLoading()` → `SceneManager.LoadScene("Job_Greybox")`
  - `LobbyPanel`: รายชื่อ "ผู้เล่น N ✓/–" · รหัสห้อง (Host/Client เห็น, Solo ไม่แสดง) · ป้าย `lobby.experimental` "5–6 คน: ทดลอง" เมื่อ `SizeLabel == Experimental` · บรรทัดงาน "บ้านไม้ (Greybox) · ปกติ" + "ยาก 🔒 ปลดล็อกด้วยยศ" · คำแนะนำ `lobby.hint_ready` / `lobby.hint_start`
  - `LobbyGate.Approve(request, response)` → `internal static JoinRefusal LobbyGate.Decide(SessionPhase phase, int players)`: ใช้ `SessionRules.CanJoin(GameSession.Instance.Phase, …, versionMatches: true)` (เวอร์ชันตรวจที่ Session property แล้ว) → ปฏิเสธพร้อม `response.Reason` = Localization key (`error.in_job` / `error.full`)
  - Client ถูกปฏิเสธ → `GameSession` ได้ `DisconnectReason` → `Failed(key)` → เมนูหลักแสดง

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Solo ใน `GameFlowFixture`)
```csharp
[UnityTest] Ready_TogglesForLocalPlayer()                 // กด R → member.Ready true · กดอีก → false
[UnityTest] Start_RequiresEveryoneReady()                 // ไม่ Ready → Enter → CountdownEndsAt −1
[UnityTest] Countdown_CancelsWhenSomeoneUnreadies()       // Ready → Enter → นับ → กด R ถอน → CountdownEndsAt −1   (Review Focus 2)
[UnityTest] Countdown_LoadsJobScene()                     // Ready → Enter → ≤ 4 วินาที ฉาก Job_Greybox · GameSession.Instance.Phase == Job
[UnityTest] Gate_RefusesJoinDuringJob()                   // LobbyGate.Decide(Job, 1) → JobInProgress · Decide(Lobby, 1) → None · SessionErrors.KeyFor(JobInProgress) == "error.in_job"   (Review Focus 3)
[UnityTest] Panel_ShowsExperimentalLabelForFive()         // LobbyPanel.Render(members 5 คน) → มีข้อความ lobby.experimental
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter LobbyTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่ + `LocalizationSetup.Setup`**
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4A: van lobby with ready, job choice and join gate`

---

### Task 5: งาน → Results → กลับ Lobby (เล่นได้หลายงานต่อ Session)

**Files:**
- Create: `Runtime/Session/JobAutoStart.cs` (บน JobDirector Prefab: Host เริ่มงานเองเมื่อฉากงานโหลดครบทุกคน), `Runtime/Session/ReturnToLobby.cs`, `Tests/PlayMode/JobCycleTests.cs`
- Modify: `Runtime/Job/ResultsPanel.cs` (บรรทัดนับถอยหลัง `results.back_in`), `Runtime/Player/PlayerMotor.cs` (Teleport ผ่าน `NetworkTransform.Teleport` เมื่อ Spawn แล้ว — ข้อค้าง M3B ข้อ 4/M3D ข้อเล็ก 7; ย้ายไปจุดเกิดของฉากใหม่ทุกครั้งที่ฉากเปลี่ยน), `Editor/LocalizationSetup.cs`

**Interfaces:**
- Consumes: `LobbyState.Phase`, `JobChoice` (Task 4) · `JobDirector.BeginJob`, `Result`, `Phase`
- Produces:
  - `JobAutoStart`: Host ฟัง `SceneManager.OnLoadEventCompleted` ของ `Job_Greybox` → `BeginJob(seed สุ่ม, difficulty จาก JobChoice)` · ฉากทดสอบ `M3_TestHouse` **ไม่มี** `JobAutoStart` (บอท/Test เริ่มงานเอง)
  - `ReturnToLobby`: เมื่อ `Result.IsSet` (Phase เป็น Results เอง) · หลัง 10 วินาที หรือ Host กด Enter → โหลด `Lobby` (Ready ทุกคนรีเซ็ตเป็น false)
  - `LoadingOverlay` (บน `GameNetwork`, ทุกเครื่อง): จอทึบ + `loading.house` "กำลังเดินทางไปบ้านลูกค้า…" ระหว่าง `Phase == Loading` จนฉากงานโหลดครบ
  - ผู้เล่นทุกคนย้ายไปจุดเกิด `PlayerSpawn{n}` ของฉากใหม่ (เจ้าของ Teleport ตัวเอง)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Solo)
```csharp
[UnityTest] JobScene_StartsJobForTheTeam()                // จาก Lobby เริ่มงาน → JobDirector.Phase Exploring · Plan != null
[UnityTest] Results_ReturnToLobbyAfterCountdown()         // LeaveEarly → Results → ≤ 11 วินาที ฉาก Lobby · Ready false
[UnityTest] Results_HostConfirmGoesNow()                  // LeaveEarly → Enter → ≤ 1 วินาที ฉาก Lobby
[UnityTest] SecondJob_StartsFresh()                       // งานแรก: ApplyAngerForTest(85), Capture เข้าตู้, ส่องกล้อง 1 ครั้ง → LeaveEarly → Lobby → งานที่สอง: AngerValue < 1, ไม่มีใครในตู้, กล้อง Film 6, ของในแผนอยู่จุดเริ่ม   (Review Focus 5)
[UnityTest] SceneChange_TeleportsPlayersToSpawns()        // หลังเข้า Job_Greybox ผู้เล่นอยู่ห่างจุดเกิด ≤ 0.5 ม.
[UnityTest] Loading_OverlayHiddenOnceJobStarts()          // ระหว่างโหลด LoadingOverlay.Visible true · เมื่อ JobDirector.Phase Exploring → false
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter JobCycleTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน** (Test M3 เดิมต้องผ่าน — Teleport ใหม่ต้องไม่ทำให้ Test ที่ Teleport ผู้เล่นพัง)
- [ ] **Step 5: Commit** — `M4A: job, results and back to the van`

---

### Task 6: ออกจากห้อง — ผู้เล่นออก, Host ออก, ข้อความตามช่วง

**Files:**
- Create: `Runtime/Session/SessionWatcher.cs` (บน `GameNetwork`: ฟัง `OnClientStopped`/`OnClientDisconnectCallback`/`DisconnectReason`), `Runtime/Session/PauseMenu.cs` (Esc: `pause.resume` "กลับเข้าเกม" / `pause.leave` "ออกจากห้อง" — ปล่อยเคอร์เซอร์ขณะเปิด), `Tests/PlayMode/SessionLeaveTests.cs`
- Modify: `Runtime/Session/GameSession.cs`, `Runtime/Player/PlayerMotor.cs` (คืนกล้อง/เคอร์เซอร์ตอน Despawn ของผู้เล่นในเครื่อง — ข้อค้าง M3B ข้อ 5), `Runtime/Interaction/PlayerInteractor.cs` (ลบป้ายคำแนะนำตอน Despawn), `Editor/LocalizationSetup.cs`

**Interfaces:**
- Consumes: `SessionRules.HostLeftPay` (Task 1) · `LobbyState.Phase` (Task 4) · `JobDirector.AcceptedRooms`, `Plan`, `Result` (ฝั่ง Client อ่านค่าที่ซิงก์มา)
- Produces:
  - Client: Host หาย → `GameSession.PendingMessageKey` = `session.host_left_lobby` "หัวหน้าห้องออกแล้ว" | `session.host_left_job` "หัวหน้าห้องออกระหว่างงาน — ได้ค่าจ้างห้องที่ผีรับแล้ว {0}" | `session.host_left_results` (ใช้ `Result.PerPlayer` ที่ซิงก์มาแล้ว) + `PendingPay` → โหลด `MainMenu`
  - Client ถูกปฏิเสธตอนเข้า (`DisconnectReason` เป็นคีย์) → `Failed(key)`
  - ผู้เล่นออกเอง (เมนูหยุด Esc → "ออกจากห้อง") → `LeaveAsync` · Host เห็นคนหลุด: งานดำเนินต่อ (มีแล้ว) · ใน Lobby รายชื่อหายและนับถอยหลังยกเลิก
  - `internal static string GameSession.MessageForHostLeft(SessionPhase phase)` (Test เรียกตรง)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] HostLeft_MessageMatchesPhase()                // Lobby/Job/Results → คีย์ 3 แบบ   (Review Focus 4)
[UnityTest] LeaveDuringJob_ReturnsToMenuCleanly()          // Solo งาน → Esc → ออกจากห้อง → MainMenu · ไม่มี Exception · Cursor ปล่อย · ไม่มีกล้องผู้เล่นค้าง · ไม่มีป้ายคำแนะนำค้าง
[UnityTest] PlayerLeavesLobby_CountdownCancels()           // จำลองผู้เล่นหาย (ลบ member) ระหว่างนับ → ยกเลิก
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter SessionLeaveTests`
- [ ] **Step 3: เขียนโค้ด + `LocalizationSetup.Setup`**
- [ ] **Step 4: รัน PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4A: leaving, host-left messages and pay`

---

### Task 7: เส้นทางเต็มหลายเครื่องผ่าน Relay

**Files:**
- Create: `Dev/DevFlowBot.cs` (ขับ UI จริงผ่าน `MainMenuPanel`/`LobbyState` — ไม่ใช้ `DevSessionRunner`), `Tools/m4a-flow-test.ps1`
- Modify: `Runtime/Bootstrap.cs` (`-dev-flow ROLE` ต่อ `DevFlowBot` และ `-dev-version X.Y.Z` แทนเวอร์ชันของ Build — เฉพาะ Development Build), `Tools/m3c-job-test.ps1` (ข้อเล็ก M3C 1 + M3D 9: `@()`, Build ใหม่สุด, ลบ Log เก่า), `Editor/BuildScript.cs` (Build เกมจริง: `Builds/SpiritMovers-v<ver>` จากฉากใน Build Settings — มีอยู่แล้ว ตรวจให้ใช้ 4 ฉากใหม่), `docs/TESTING.md`, `docs/STATUS.md`, `CHANGELOG.md`, `docs/superpowers/plans/2026-10-31-m4-overview.md`

**Interfaces:**
- Produces: สถานการณ์ (Host + 1–3 Client, รหัสส่งผ่านไฟล์แบบ M3):
  1. Host สร้างห้อง → Client ใส่รหัสเข้า → ทุกคน Ready → Host เริ่ม → ทุกคนอยู่ `Job_Greybox` · Client คนที่ 2 (ถ้ามี) ลองเข้าใหม่ระหว่างงาน → ถูกปฏิเสธ `error.in_job`
  2. Host วางของคืน 1 ห้อง (Dev RPC) → ปิดงานที่รถ → Results → กลับ Lobby → งานที่สองเริ่มได้
  3. งานที่สอง: วางของคืน 1 ห้อง → **Host ปิดเกมกลางงาน** → Client ทุกคนอยู่ `MainMenu` ข้อความ `session.host_left_job` ค่าจ้าง = ส่วนแบ่งของ 1 ห้อง
  4. แยกรอบ: Client ที่ตั้ง `-dev-version 0.0.0` → ถูกปฏิเสธ `error.version`
  - SUMMARY `[M4A] role=… scenes=MainMenu>Lobby>Job_Greybox>Lobby>Job_Greybox>MainMenu jobs=2 refused=… message=… pay=… ok=…`
- `Tools/m4a-flow-test.ps1 [-Runs 3] [-Clients 1] [-LatencyMs 0] [-VersionMismatch]` — ผ่านเมื่อทุกเครื่อง `ok=true` และ `jobs/message/pay` ตรงกัน · `-VersionMismatch` = Host + 1 Client ที่ `-dev-version 0.0.0` ผ่านเมื่อ Client ได้ `error.version` และ Host ไม่สะดุด

- [ ] **Step 1: เขียนบอท + สคริปต์** แล้วรันกับ Build เดิม → ล้มเหลว
- [ ] **Step 2: Build เกมจริง** (`Tools\build.ps1`, ไม่มี `-nographics`)
- [ ] **Step 3: รัน** — `Tools\m4a-flow-test.ps1 -Runs 3` PASS · `-Clients 2` PASS · `-LatencyMs 150` PASS · `-VersionMismatch` PASS · M3 เดิม: `m3c-job-test.ps1 -Runs 10` และ `-Scenario ghost -Runs 2` ยัง PASS
- [ ] **Step 4: ภาพหน้าจอจาก Build จริง (มีหน้าต่าง)** — `docs/screenshots/m4/main-menu-th.png`, `lobby-th.png` (มีรหัสห้อง/รายชื่อ/Ready), `host-left-th.png` · ดูภาพจริง (ไทยไม่เพี้ยน ไม่มีสีม่วงแดง) · ค้น Log หา `is missing or no valid script`
- [ ] **Step 5: เอกสาร** — TESTING (สคริปต์ + ผล), STATUS (M4A `Tested`), CHANGELOG, m4-overview (ผล M4A)
- [ ] **Step 6: Commit** — `M4A: full session flow over Relay`
