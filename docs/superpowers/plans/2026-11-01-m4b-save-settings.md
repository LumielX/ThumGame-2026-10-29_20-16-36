# M4B Save & Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ผู้เล่นมีของที่ติดตัวข้ามการเปิดเกม — เงินสะสมจากงาน (รวมกรณี Host ออก) และชื่อที่ตั้งเอง (เห็นในรถตู้) · หน้า Settings: ภาษา, ระดับเสียง 6 กลุ่ม, ความไวเมาส์/กลับแกน Y/FOV, เปลี่ยนปุ่ม, Push-to-Talk และ "ให้ผีได้ยินไมค์ของฉัน" · ทั้งหมดเก็บในไฟล์ที่เขียนแบบ Atomic มีสำรอง 1 รุ่นและ Migration

**Architecture:** ข้อมูลและกติกา (โครง Save/Settings, จัดชื่อ, จำกัดค่า, ตัวเก็บไฟล์ Atomic + สำรอง + Migration, ปุ่มซ้ำ) อยู่ใน Core (`Core/Save/`, EditMode, ใช้ `System.IO` ได้) · การแปลง JSON ผ่าน `IJsonCodec` ที่ Runtime ทำด้วย `JsonUtility` (ไม่เพิ่มแพ็กเกจ) · Runtime มี `GameData` (โหลดตอน Boot, ถือ Save + Settings, บันทึกเมื่อเปลี่ยน), `SettingsApplier` (ภาษา/เมาส์/FOV/เสียง/ปุ่ม) และ `SettingsPanel` (UGUI สร้างด้วยโค้ดแบบ `SessionUi`, เปิดจากเมนูหลักและเมนู Esc) · ชื่อผู้เล่นไปกับ Connection Payload เดียวกับเวอร์ชัน แล้ว Host ใส่ใน `LobbyMember`

**Tech Stack:** Unity 6000.3.27f1 · `JsonUtility` · Input System 1.20.1 (Binding Overrides JSON, `PerformInteractiveRebinding`) · Localization 1.5.13 (`SelectedLocale`) · NGO 2.13.3 (ConnectionApproval payload) · UGUI/TMP + `ThaiShaper` · Unity Test Framework · `Tools/test.ps1`, `Tools/m4a-flow-test.ps1`

**Spec:** `docs/TECHNICAL_DESIGN.md` §9, §11, §13, §19 · `docs/GDD.md` §9, §10, §12, §18, §19 · `docs/PROJECT_PLAN.md` M4 ("Save/Settings พื้นฐาน") · ภาพรวม + ข้อค้าง: `docs/superpowers/plans/2026-10-31-m4-overview.md`

## Global Constraints
- **ไฟล์:** `Application.persistentDataPath/save_v{n}.json` และ `settings.json` แยกกัน · มี Schema Version + Migration ที่ทดสอบด้วย EditMode (TECHNICAL_DESIGN §9)
- **เขียนแบบ Atomic:** เขียนไฟล์ชั่วคราว → แทนที่ · เก็บสำรอง 1 รุ่น · ไฟล์เสีย → ใช้สำรอง + แจ้งผู้เล่น (TECHNICAL_DESIGN §9)
- ไม่มีการเข้ารหัส/กันโกง · **ไม่มีข้อมูลส่วนตัวนอกจากชื่อที่ผู้เล่นตั้ง** (TECHNICAL_DESIGN §9, §19) — ห้ามใช้ชื่อบัญชี Windows/เครื่องเป็นค่าเริ่มต้น
- **เงิน:** ค่าจ้างแบ่งเท่ากันทั้งทีม · Save ในเครื่องของแต่ละคน (GDD §12) · Host ออก → ได้ค่าจ้างของห้องที่ผีรับแล้ว (GDD §9, ทำใน M4A แล้ว — M4B บันทึก)
- **Settings:** ภาษาไทย (ค่าเริ่มต้น)/อังกฤษ (GDD §19) · เสียง 6 กลุ่ม Master/Music/SFX/Ambience/Voice/UI (TECHNICAL_DESIGN §13) · ความไวเมาส์ · กลับแกน Y · FOV · Key Rebinding (GDD §18) · Binding Overrides เก็บเป็น JSON ของ Input System (TECHNICAL_DESIGN §11) · Push-to-Talk/พูดแล้วส่ง และ "ให้ผีได้ยินเสียงไมค์ของฉัน" (GDD §10)
- **แสดงชื่อปุ่มตาม Binding ปัจจุบันในทุกคำแนะนำ** (TECHNICAL_DESIGN §11, D-024)
- ข้อความทุกอันผ่าน Localization (TH/EN) + ThaiShaper · Core ไม่อ้าง UnityEngine (C# 9, ไม่มี record struct) · MonoBehaviour ไฟล์ละคลาส · Build ห้าม `-nographics` · ฉากที่สร้างใหม่/แก้ใช้ `SaveWithSceneIds`
- **การตัดสินใจประเภท C ในแผนนี้:**
  - **JSON ด้วย `JsonUtility` ผ่าน `IJsonCodec`** — ไม่เพิ่มแพ็กเกจ (Newtonsoft มีอยู่แค่เป็น Dependency ทางอ้อม) · Migration แบบมี Type ต่อรุ่น (`string → string` ทีละขั้น)
  - **ไม่มี Audio Mixer ใน M4B** — ยังไม่มี AudioSource ในเกม และสร้าง Mixer ด้วยสคริปต์ไม่ได้ด้วย API สาธารณะ · Master คุม `AudioListener.volume` · กลุ่มอื่นเก็บค่าและให้ระบบที่ตามมาอ่านผ่าน `GameAudio.Gain(group)` (Voice ใน M4C, เสียงจริงใน M5 สร้าง Mixer ตอนนั้น) — บันทึก DECISIONS เป็นข้อเทคนิค
  - **ค่าเริ่มต้น ⚙:** ความไวเมาส์ ×1.0 (ช่วง 0.2–3.0) · FOV 75° (ช่วง 60–100) · เสียงทุกกลุ่ม 1.0 · Push-to-Talk ปิด (พูดแล้วส่ง) ปุ่ม V · ผีได้ยินไมค์ เปิด · ชื่อเริ่มต้น "พนักงาน" + เลข 4 หลักสุ่ม (สร้างครั้งแรกแล้วเก็บ)
  - **ชื่อ:** ตัดช่องว่างหัวท้าย/ยุบช่องว่างซ้ำ · เก็บเฉพาะตัวอักษร (รวมสระ/วรรณยุกต์ไทย), ตัวเลข, ช่องว่าง, `-_.` · ยาวไม่เกิน 20 ตัว (UTF-16) · ว่าง → ชื่อเริ่มต้น · ชื่อซ้ำกันได้
  - **เปลี่ยนปุ่มได้:** Move (บน/ล่าง/ซ้าย/ขวา), Sprint, Crouch, Jump, Interact, Throw, JobSheet, Wai, Use, Ready, Confirm, PushToTalk — เฉพาะ Binding แรกของแต่ละ Action · ไม่ให้เปลี่ยน Look, RotateItem (ล้อเมาส์), Pause (Esc ใช้ยกเลิกการกด) · ปุ่มซ้ำ **ยอมให้ แต่แสดงคำเตือนทั้งสองแถว** ยกเว้นคู่ที่ตั้งใจซ้ำ Throw/Use (คลิกขวาตามบริบท)
  - **หน้า Settings มี 3 แท็บ:** ทั่วไป (ภาษา, เมาส์, FOV) · เสียง (6 กลุ่ม + Push-to-Talk + ผีได้ยินไมค์) · ปุ่ม (รายการ + คืนค่าเริ่มต้น) · มีผลทันทีที่เปลี่ยน · บันทึกตอนปิดหน้า
  - **โฟลเดอร์ข้อมูล:** `persistentDataPath` · Development Build ที่ใส่ `-dev-profile X` ใช้ `persistentDataPath/profiles/X/` (บอทหลายตัวในเครื่องเดียวไม่เขียนไฟล์เดียวกัน) · Test ใช้โฟลเดอร์ชั่วคราว
- **ข้อค้างที่รับ:** M4A ข้อเล็ก 8 (Numpad Enter → Confirm) · M4A ข้อเล็ก 6 (`-dev-profile` อ่านเฉพาะ Development Build — ย้ายมาอยู่ที่ `DataLocation`) · **M3C ข้อเล็ก 3 (`AngerConfig.Validate()` ตอนโหลด) เลื่อนไป M5** เพราะ M4B ยังไม่อ่านค่าสมดุลจากไฟล์
- **นอกขอบเขต M4B:** กราฟิก/ความละเอียด/หน้าต่าง/V-Sync/FPS, ความสว่าง, คำบรรยายเสียง, ปิด Head Bob/ลด Jumpscare (M5 "Accessibility หลัก") · เลือกอุปกรณ์ไมค์/ลำโพง, ปิดเสียงรายคน, ปุ่มวิทยุ (M4C — ใช้ค่าที่ M4B เก็บ) · ยศ/XP/ของแต่งตัว/อัลบั้มผี/สถิติ (M6) · กดค้าง/กดสลับ วิ่ง-ย่อ (M5 Accessibility) · ปุ่มรายงานบั๊กที่แนบ Settings (M4D)

## Review Focus
1. **ไฟล์ Save/Settings เสีย, ว่าง, เขียนค้างครึ่งไฟล์, หรือมาจากเวอร์ชันเกมที่ใหม่กว่า** — ใช้สำรองพร้อมข้อความ หรือเริ่มใหม่โดยเก็บไฟล์เสียไว้ · ไฟล์จากเวอร์ชันใหม่กว่า **ไม่ถูกเขียนทับ** (Task 2: `Load_*`)
2. **Test และบอทต้องไม่แตะ Save จริงของผู้เล่น** และหลาย Instance ในเครื่องเดียวไม่เขียนไฟล์เดียวกัน (Task 3: `DataLocation_*`, Fixture ใช้โฟลเดอร์ชั่วคราว)
3. **ชื่อที่ไม่ปกติ** — ว่าง, ช่องว่างล้วน, 200 ตัว, อีโมจิ, `|`, อักขระควบคุม, ไทยมีวรรณยุกต์ — ถูกจัดทั้งฝั่งผู้เล่นและฝั่ง Host (ไม่เชื่อ Payload) และไม่ทำ `FixedString` ล้น (Task 1: `PlayerName_*`, Task 4: `Payload_*`)
4. **ค่าจ้างเข้าซ้ำ** — งานจบแล้ว Host ออกตอน Results, เน็ตหลุดตอน Results, งานที่สอง, Solo — เข้าครั้งเดียวต่องาน (Task 3: `Pay_*`)
5. **เปลี่ยนปุ่ม** — Esc ยกเลิก, ขยับเมาส์ไม่ถูกจับเป็นปุ่ม, ปุ่มซ้ำมีคำเตือน, คืนค่าเริ่มต้นได้, ค่ายังอยู่หลังเปิดเกมใหม่, คำแนะนำบนจอเปลี่ยนตาม (Task 5: `Bindings_*`, Task 6: `Rebind_*`)

---

### Task 1: Core — ข้อมูล Save/Settings และกติกา

**Files:**
- Create: `Core/Save/SaveData.cs`, `Core/Save/SettingsData.cs`, `Core/Save/PlayerName.cs`, `Core/Save/BindingRules.cs`
- Test: `Tests/EditMode/Save/SaveRulesTests.cs`

**Interfaces:**
- Produces (namespace `SpiritMovers.Core.Save`, คลาส `[Serializable]` ฟิลด์ public ให้ `JsonUtility` ใช้ได้):
  - `class SaveData { int schema = 1; int money; string playerName; }` · `const int SaveData.CurrentSchema = 1` · `void Credit(int amount)` (ติดลบ/0 → ไม่ทำอะไร · ไม่ล้นเกิน `int.MaxValue`) · `SaveData Sanitized()` (money < 0 → 0, ชื่อผ่าน `PlayerName.Clean`)
  - `class SettingsData { int schema = 1; string language = "th"; float mouseSensitivity = 1f; bool invertY; float fov = 75f; float master, music, sfx, ambience, voice, ui = 1f; bool pushToTalk; bool ghostHearsMic = true; string bindingOverrides = ""; }` · `const int SettingsData.CurrentSchema = 1` · `SettingsData Sanitized()` (ภาษานอก th/en → th · ความไว 0.2–3.0 · FOV 60–100 · เสียง 0–1 · NaN → ค่าเริ่มต้น)
  - `enum AudioGroup { Master, Music, Sfx, Ambience, Voice, Ui }` · `float SettingsData.Volume(AudioGroup)` / `void SetVolume(AudioGroup, float)`
  - `static class PlayerName`: `const int MaxLength = 20` · `string Clean(string raw)` (คืน `""` ถ้าไม่เหลืออะไร) · `string Default(int seed)` → `"พนักงาน " + 4 หลัก` (`seed` → 1000–9999)
  - `static class BindingRules`: `IReadOnlyList<(string A, string B)> Conflicts(IReadOnlyList<(string Action, string Path)> bindings)` — คู่ที่ Path เดียวกัน (ไม่สนตัวพิมพ์) ยกเว้นคู่ Throw/Use

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] Credit_AddsAndIgnoresNonPositive()        // 0 +50 → 50 · +0, -10 → 50
[Test] Credit_DoesNotOverflow()                  // money int.MaxValue-5, +100 → int.MaxValue
[Test] SaveSanitized_FixesNegativeMoneyAndName()  // money -3 → 0 · ชื่อ " \u0007บี|ม  " → "บีม"
[Test] PlayerName_TrimsCollapsesAndFilters()      // "  ป้า   แจ่ม  " → "ป้า แจ่ม" · "a|b\nc" → "abc" · "😀ผู้ใหญ่" → "ผู้ใหญ่"   (Review Focus 3)
[Test] PlayerName_CapsLength()                    // 200 ตัว → 20 ตัว · ไม่ตัดกลางสระ/วรรณยุกต์ (ตัวสุดท้ายไม่ใช่ Mark ที่ไม่มีพยัญชนะ)
[Test] PlayerName_EmptyStaysEmpty()               // "", "   ", null, "😀" → ""
[Test] PlayerName_DefaultIsEmployeeNumber()       // Default(1) ตรงรูป "พนักงาน dddd" · Default เดิม → ชื่อเดิม
[Test] SettingsSanitized_ClampsEverything()       // ความไว 9 → 3 · FOV 10 → 60 · เสียง -1 → 0, 2 → 1 · NaN → ค่าเริ่มต้น · ภาษา "fr" → "th"
[Test] Volume_ReadsAndWritesEachGroup()           // SetVolume(Music, .3) → Volume(Music) .3 · กลุ่มอื่นไม่เปลี่ยน
[Test] Conflicts_FindsSamePathExceptThrowUse()    // Jump=space, Ready=space → (Jump,Ready) · Throw=rmb, Use=rmb → ไม่มี · "<Keyboard>/T" vs "<Keyboard>/t" → ซ้ำ
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter SaveRulesTests` → ไม่คอมไพล์
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** — `Clean` ใช้ `char.GetUnicodeCategory` (Letter/NonSpacingMark/SpacingCombiningMark/DecimalDigit + ช่องว่าง + `-_.`), ทิ้ง Surrogate ทั้งคู่
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: core save and settings data`

---

### Task 2: Core — ตัวเก็บไฟล์ Atomic, สำรอง และ Migration

**Files:**
- Create: `Core/Save/IJsonCodec.cs`, `Core/Save/JsonFileStore.cs`, `Core/Save/LoadOutcome.cs`
- Test: `Tests/EditMode/Save/JsonFileStoreTests.cs` (ใช้ `JsonUtility` เป็น Codec ในเทสต์ + โฟลเดอร์ชั่วคราวใต้ `Path.GetTempPath()` ลบใน TearDown)

**Interfaces:**
- Consumes: `SaveData`, `SettingsData` (Task 1)
- Produces:
  - `interface IJsonCodec { string ToJson(object value); T FromJson<T>(string json); int ReadSchema(string json); }` (`ReadSchema` คืน -1 ถ้าอ่านไม่ได้)
  - `enum LoadOutcome { Loaded, Fresh, RestoredFromBackup, ResetCorrupt, NewerVersion }`
  - `sealed class JsonFileStore<T> where T : class, new()`:
    - ctor `(string directory, Func<int, string> fileName, int currentSchema, IJsonCodec codec, IReadOnlyDictionary<int, Func<string, string>> migrations = null)` — `fileName(schema)` ให้ Save คืน `save_v{n}.json` และ Settings คืน `settings.json` เสมอ · `migrations[n]` แปลง JSON รุ่น n → n+1
    - `(T Value, LoadOutcome Outcome) Load()` · `void Save(T value)` · `bool ReadOnly` (true เมื่อพบไฟล์รุ่นใหม่กว่า → `Save` ไม่ทำอะไร)
  - กติกา `Load`: ไฟล์รุ่นปัจจุบัน → อ่าน (เสีย/ว่าง/schema ไม่ตรง → ลอง `.bak` → `RestoredFromBackup` และเขียนกลับ · `.bak` เสียด้วย → เปลี่ยนชื่อไฟล์เสียเป็น `<name>.corrupt` แล้ว `ResetCorrupt`) · ไม่มีไฟล์รุ่นปัจจุบัน → หาไฟล์รุ่นเก่าที่ใหม่ที่สุด แล้ว Migrate ทีละขั้นเป็นรุ่นปัจจุบัน (ไฟล์เก่าเก็บไว้) → `Loaded` · ไม่มีอะไรเลย → `Fresh` · พบรุ่นใหม่กว่า (ชื่อไฟล์หรือ schema ในไฟล์) → ค่าเริ่มต้น + `NewerVersion` + `ReadOnly`
  - กติกา `Save`: เขียน `<name>.tmp` (Flush) → ถ้ามีไฟล์เดิม `File.Replace(tmp, file, file.bak)` ไม่มีก็ `File.Move` · สร้างโฟลเดอร์ถ้ายังไม่มี

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] SaveThenLoad_RoundTrips()                 // money 120, ชื่อไทย → Load ได้ค่าเดิม · Outcome Loaded
[Test] Load_MissingIsFresh()                     // โฟลเดอร์ว่าง → new T(), Fresh
[Test] Save_KeepsOneBackup()                     // Save A, Save B → file = B, .bak = A, ไม่มี .tmp เหลือ
[Test] Load_CorruptUsesBackup()                  // file = "{\"money\":5" (ค้างครึ่ง) → ค่าจาก .bak + RestoredFromBackup + file ถูกเขียนใหม่   (Review Focus 1)
[Test] Load_EmptyFileUsesBackup()                // file = "" → RestoredFromBackup
[Test] Load_BothCorruptResetsAndKeepsEvidence()  // file + .bak เสีย → Fresh value + ResetCorrupt + มี .corrupt
[Test] Load_LeftoverTmpIgnored()                 // มี .tmp ค้าง (เขียนค้างตอนไฟดับ) + file ดี → Loaded จาก file
[Test] Load_MigratesOlderFileStepByStep()        // currentSchema 3, migrations {1→2, 2→3} (ของเทสต์), มีแค่ save_v1.json → ค่าผ่านทั้งสองขั้น · มี save_v3.json · save_v1.json ยังอยู่
[Test] Load_NewerVersionIsNotOverwritten()       // มี save_v2.json (currentSchema 1) → NewerVersion, ReadOnly · Save() แล้วไฟล์ v2 เหมือนเดิมทุกไบต์   (Review Focus 1)
[Test] Settings_NewerSchemaInsideFile_ReadOnly() // settings.json schema 9 → NewerVersion, ไม่ถูกเขียนทับ
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter JsonFileStoreTests`
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** — Core ใช้ `System.IO` ได้ (ไม่อ้าง UnityEngine) · ทุก Exception ของ IO ตอนอ่านนับเป็น "เสีย" · ตอนเขียนให้โยนต่อ (ผู้เรียกแสดงข้อความ)
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: atomic json store with backup and migration`

---

### Task 3: `GameData` — โหลดตอนเปิดเกม, เงินสะสม และข้อความ Save

**Files:**
- Create: `Runtime/Save/UnityJsonCodec.cs`, `Runtime/Save/DataLocation.cs` (static), `Runtime/Save/GameData.cs` (static), `Runtime/Save/JobPay.cs` (MonoBehaviour ในฉากงาน)
- Modify: `Runtime/Job/JobDirector.cs` (`AcceptRoomForTest`), `Runtime/Bootstrap.cs` (เรียก `GameData.Load()` ก่อนโหลด MainMenu), `Runtime/Session/GameSession.cs` (เข้าเงินตอน Host ออก/เน็ตหลุด, `Profile` → `DataLocation.DevProfile`), `Runtime/Session/MainMenuPanel.cs` (บรรทัดเงินสะสม + ข้อความ Save), `Runtime/Session/LobbyPanel.cs` (เงินสะสมของฉัน), `Runtime/Job/ResultsPanel.cs` (บรรทัด "ยอดสะสม"), `Editor/GameScenesSetup.cs` (ใส่ `JobPay` ใน `Job_Greybox`), `Editor/LocalizationSetup.cs`, `Tests/PlayMode/GameFlowFixture.cs` (โฟลเดอร์ชั่วคราว)
- Test: `Tests/PlayMode/GameDataTests.cs`

**Interfaces:**
- Consumes: `JsonFileStore<T>`, `LoadOutcome` (Task 2) · `SaveData`, `SettingsData`, `PlayerName` (Task 1) · `JobDirector.Result` (`PerPlayer`), `GameSession.PendingPay`
- Produces:
  - `DataLocation.Root` (`persistentDataPath`, หรือ `.../profiles/<X>` เมื่อ `DevProfile != null`) · `static string DevProfile` (อ่าน `-dev-profile` เฉพาะ `DEVELOPMENT_BUILD || UNITY_EDITOR`, อื่นๆ null) · `static string OverrideRoot` (Test ตั้ง)
  - `GameData.Load()` (เรียกซ้ำได้ — อ่านใหม่จาก `DataLocation.Root`) · `SaveData Save` · `SettingsData Settings` · `void SaveNow()` / `void SaveSettingsNow()` · `int Money` · `void CreditPay(int amount, object job)` — **เข้าครั้งเดียวต่อ `job`** (จำ Reference ของงานล่าสุดที่จ่ายแล้ว) แล้วบันทึกทันที · `string PlayerName` (get/set → Clean, ว่าง → ชื่อเริ่มต้นที่เก็บไว้) · `string StartupMessageKey` (`save.restored`, `save.reset`, `save.newer` หรือ null — ดูครั้งเดียว) · `event Action Changed`
  - `internal void JobDirector.AcceptRoomForTest(int roomIndex)` — Host: ให้ผีรับห้องนั้นทันที (ทางเดียวกับการรับปกติ) สำหรับเทสต์ค่าจ้าง
  - `JobPay` (ฉากงาน): เมื่อ `Result.IsSet` บนเครื่องนี้ → `GameData.CreditPay(result.PerPlayer, director)`
  - `GameSession` ตอน Host ออก/เน็ตหลุดระหว่าง **Job** → `CreditPay(PendingPay, director)` (ระหว่าง Results ไม่เพิ่ม เพราะ `JobPay` จ่ายไปแล้ว — ใช้ `job` เดียวกันจึงไม่ซ้ำ)
  - Localization: `menu.money` "เงินสะสม {0}", `results.total` "ยอดสะสม {0}", `save.restored` "ไฟล์บันทึกเสีย — ใช้สำรองล่าสุดแทน", `save.reset` "ไฟล์บันทึกเสียและไม่มีสำรอง — เริ่มใหม่ (เก็บไฟล์เสียไว้ในโฟลเดอร์เกม)", `save.newer` "ไฟล์บันทึกมาจากเกมรุ่นใหม่กว่า — รอบนี้จะไม่บันทึก", `save.failed` "บันทึกไม่สำเร็จ — ตรวจพื้นที่ดิสก์" (+ อังกฤษ)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Fixture ตั้ง `DataLocation.OverrideRoot` เป็นโฟลเดอร์ชั่วคราวใหม่ก่อนโหลด Boot และลบหลังจบ — **ทุก PlayMode Test ไม่แตะ Save จริง**)
```csharp
[UnityTest] DataLocation_TestsUseTheirOwnFolder()     // DataLocation.Root ขึ้นต้นด้วยโฟลเดอร์ชั่วคราว ไม่ใช่ persistentDataPath   (Review Focus 2)
[UnityTest] DataLocation_DevProfileGetsSubfolder()    // DevProfile "m4ahost" → Root ลงท้าย "profiles/m4ahost" (ทดสอบผ่าน internal static RootFor(base, profile))
[UnityTest] Pay_ResultsCreditOnce()                   // SoloIntoJob → LeaveEarly → Results: Money เพิ่ม PerPlayer · รออีก 1 วินาที ไม่เพิ่มซ้ำ · ไฟล์ save_v1.json มีค่าใหม่   (Review Focus 4)
[UnityTest] Pay_HostLeftDuringJobCreditsShare()       // AcceptRoomForTest(0) → จำลองเน็ตหลุด (Shutdown) → Money เพิ่มเท่า PendingPay (> 0)
[UnityTest] Pay_LostDuringResultsNotTwice()           // Results แล้วเน็ตหลุด → Money เพิ่มเท่า PerPlayer ครั้งเดียว   (Review Focus 4)
[UnityTest] Pay_SecondJobCreditsAgain()               // งาน 1 Results → กลับรถตู้ → งาน 2 Results → เพิ่มสองครั้ง
[UnityTest] Menu_ShowsMoneyAndSaveMessage()           // เขียน save_v1.json เสีย + .bak ดีก่อน Boot → เมนูแสดง save.restored และ "เงินสะสม" ตามค่าใน .bak
[UnityTest] Restart_KeepsMoney()                      // เข้าเงิน → GameData.Load() ใหม่ → Money เท่าเดิม
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter GameDataTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่** — `-executeMethod SpiritMovers.Editor.GameScenesSetup.CreateGameScenes` และ `...LocalizationSetup.Setup` · บันทึกล้ม (IOException) → Log Warning + ข้อความ `save.failed` บนจอที่เปิดอยู่ (เมนู/รถตู้) ไม่ทำเกมค้าง
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: saved money from jobs and host-left pay`

---

### Task 4: ชื่อผู้เล่น — ตั้งในเมนู เห็นในรถตู้

**Files:**
- Create: `Core/Session/ConnectionPayload.cs`
- Modify: `Runtime/Session/GameSession.cs` (`SetVersionPayload` → Payload ใหม่), `Runtime/Session/LobbyGate.cs` (อ่าน Payload, จำชื่อต่อ Client), `Runtime/Session/LobbyState.cs` (`LobbyMember.Name`), `Runtime/Session/LobbyPanel.cs`, `Runtime/Session/MainMenuPanel.cs` (ช่องชื่อ), `Runtime/Session/SessionUi.cs` (`CreateNameField`), `Dev/DevFlowBot.cs` (SUMMARY `name=` และ `names=`), `Tools/m4a-flow-test.ps1`, `Editor/LocalizationSetup.cs`
- Test: `Tests/EditMode/Session/ConnectionPayloadTests.cs`, `Tests/PlayMode/PlayerNameTests.cs`

**Interfaces:**
- Consumes: `PlayerName.Clean/Default` (Task 1) · `GameData.PlayerName` (Task 3) · `LobbyGate.VersionMatches` (M4A)
- Produces:
  - `static class ConnectionPayload`: `string Write(string version, string name)` → `"{version}\n{name}"` · `bool TryRead(string text, out string version, out string name)` (ไม่มี `\n` → ชื่อว่าง เพื่ออ่าน Payload รุ่น M4A ได้) · ชื่อผ่าน `PlayerName.Clean` เสมอ
  - `LobbyGate.VersionMatches` อ่านเวอร์ชันจาก `ConnectionPayload` · `internal static string NameFor(ulong clientId)` (ว่าง → `"ผู้เล่น {Number}"` ตาม Localization `lobby.player_n`) · Host ใช้ `GameData.PlayerName` ของตัวเอง
  - `LobbyMember.Name` (`FixedString64Bytes`) — ตัดให้พอดี 61 ไบต์ UTF-8 โดยไม่ตัดกลางตัวอักษร · `lobby.member` เปลี่ยนเป็น `"{0} — {1}"` (ชื่อ, พร้อม/ยังไม่พร้อม)
  - เมนูหลัก: ช่อง "ชื่อของคุณ" (ไทยพิมพ์ได้ + `EnableThaiShaping`) อยู่บน Host/Join · จบการแก้ → `GameData.PlayerName = text` แล้ว `SaveNow()` · แสดงชื่อที่ Clean แล้วกลับในช่อง
  - Localization: `menu.name_hint` "ชื่อของคุณ", `lobby.player_n` "ผู้เล่น {0}"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
// EditMode
[Test] Payload_RoundTripsVersionAndName()        // Write("0.0.1","บีม") → TryRead → "0.0.1","บีม"
[Test] Payload_OldFormatHasNoName()              // "0.0.1" → true, name ""
[Test] Payload_HostileNameIsCleaned()            // ชื่อ 200 ตัว + "\n" + "|" → ชื่อ ≤20 ตัว ไม่มีอักขระต้องห้าม · Payload ขยะ (ไบต์สุ่ม) → false   (Review Focus 3)
[Test] MemberName_FitsFixedStringWithoutSplitting() // ชื่อไทย 20 ตัว (60 ไบต์) ใส่ได้ · ข้อความ 30 ตัวไทย → ตัดที่ขอบตัวอักษร ไม่ throw
// PlayMode
[UnityTest] Name_TypedInMenuIsSavedAndShownInVan() // พิมพ์ "  ป้า  แจ่ม " → ช่องแสดง "ป้า แจ่ม" · Solo → รถตู้ LastText มี "ป้า แจ่ม" · GameData.Save.playerName ในไฟล์ตรงกัน
[UnityTest] Name_EmptyUsesDefault()                // ช่องว่าง → ใช้ชื่อเริ่มต้น "พนักงาน dddd" เดิมทุกครั้ง
[UnityTest] Gate_NamesTheJoiningClient()           // เรียก LobbyGate.Approve (เปลี่ยนเป็น internal) ด้วย Payload ชื่อ "เพื่อน" (client id 7) → NameFor(7) == "เพื่อน" · Payload ไม่มีชื่อ → "ผู้เล่น N"
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter ConnectionPayloadTests` และ `-Platform PlayMode -Filter PlayerNameTests`
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** · บอท: `-dev-name <ชื่อ>` ตั้งชื่อก่อนเข้า (บอทใช้ชื่อไทย เช่น `บอท1`) · SUMMARY เพิ่ม `name=<ของฉัน>` และ `names=<รายชื่อใน Lobby คั่นด้วย ,>` · สคริปต์: Client ต้องเห็นชื่อ Host และชื่อตัวเองใน `names` · Host ต้องเห็นชื่อ Client
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: player names in the van`

---

### Task 5: นำ Settings ไปใช้ — ภาษา, เมาส์, FOV, เสียง, ปุ่ม

**Files:**
- Create: `Runtime/Settings/SettingsApplier.cs` (static), `Runtime/Settings/GameAudio.cs` (static), `Runtime/Settings/KeyNames.cs` (static)
- Modify: `Runtime/Player/PlayerMotor.cs` (ความไว, กลับแกน Y, FOV ของกล้องหัว), `Runtime/Player/KeyboardMouseInput.cs` (+ Map `Voice`), `Runtime/Player/PlayerInputState.cs` (`TalkHeld`), `Settings/Input/SpiritMovers.inputactions` (Confirm + `<Keyboard>/numpadEnter` · Map `Voice` Action `PushToTalk` = `<Keyboard>/v`), `Runtime/Session/LobbyPanel.cs`, `Runtime/Session/ReturnToLobby.cs`, `Runtime/Ghost/SpiritCamera.cs` (คำแนะนำใช้ชื่อปุ่ม), `Runtime/Bootstrap.cs` (`SettingsApplier.ApplyAll()` หลัง `GameData.Load()`), `Editor/LocalizationSetup.cs`
- Test: `Tests/PlayMode/SettingsApplyTests.cs`

**Interfaces:**
- Consumes: `GameData.Settings`, `SettingsData`, `AudioGroup` (Task 1, 3)
- Produces:
  - `SettingsApplier.ApplyAll()` · `ApplyLanguage()` (`LocalizationSettings.SelectedLocale` = locale ตาม `language`, แล้ว `event Action LanguageChanged`) · `ApplyAudio()` (`AudioListener.volume = master`) · `ApplyBindings(InputActionAsset)` (ล้าง Override เดิมแล้ว `LoadBindingOverridesFromJson`, JSON เสีย → Log Warning + ใช้ค่าเริ่มต้น) · `string CaptureBindings(InputActionAsset)` (`SaveBindingOverridesAsJson`) · `InputActionAsset Actions` (Asset เดียวกับที่ `PlayerMotor` ใช้ — อ้างจาก `GameNetwork` Prefab)
  - `GameAudio.Gain(AudioGroup g)` = `g == Master ? master : master × กลุ่มนั้น` (M4C/M5 ใช้)
  - `PlayerMotor` อ่าน `GameData.Settings` ทุกเฟรม: `LookSensitivity × mouseSensitivity`, `invertY` กลับเครื่องหมายแกน Y · `float Fov` (= `fov`) และกล้องหัว `fieldOfView = Fov` เมื่อมีกล้อง
  - `KeyNames.For(string actionPath)` (`"Gameplay/Ready"`) → ชื่อที่แสดง: แป้นพิมพ์ = `GetBindingDisplayString(0)` · ปุ่มเมาส์ซ้าย/ขวา/กลาง → Localization `key.mouse_left/right/middle`
  - Localization เปลี่ยนเป็นมีช่องปุ่ม: `lobby.hint_ready` "กด {0} เมื่อพร้อม" · `lobby.hint_start` "…หัวหน้ากด {0} เพื่อออกเดินทาง" · `results.back_in` "กลับรถตู้ใน {0} วินาที (หัวหน้ากด {1} เพื่อไปเลย)" · `camera.use` "{0} ส่องกล้องวิญญาณ" · ใหม่ `key.mouse_left` "คลิกซ้าย", `key.mouse_right` "คลิกขวา", `key.mouse_middle` "คลิกกลาง" (+ อังกฤษ)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Language_SwitchesTablesAndMenuText()   // Settings.language "en" → ApplyLanguage → เมนูหลักแสดง "Host a room" · กลับ "th" → "สร้างห้อง"
[UnityTest] Look_UsesSensitivityAndInvertY()       // Solo · ScriptedInput Look (10, 10): ความไว 2 → หันมากกว่าค่า 1 เป็น 2 เท่า · invertY → pitch ไปทางตรงข้าม
[UnityTest] Fov_AppliedToHeadCamera()              // fov 90 → PlayerMotor.Local.Fov == 90 (ค่าที่กล้องหัวใช้ — batchmode ไม่มีกล้อง จึงตรวจที่ค่านี้; ภาพจริงตรวจใน Task 7)
[UnityTest] Master_SetsListenerVolume()            // master 0.25 → AudioListener.volume 0.25 · Gain(Music) = master × music
[UnityTest] Bindings_OverrideSurvivesReload()       // ApplyBindingOverride Ready → <Keyboard>/t · CaptureBindings → เก็บใน Settings → SaveSettingsNow → GameData.Load + ApplyBindings บน Asset ที่รีเซ็ตแล้ว → Ready = t   (Review Focus 5)
[UnityTest] Bindings_CorruptJsonFallsBackToDefaults() // bindingOverrides "{oops" → Ready ยังเป็น r · ไม่ throw
[UnityTest] Hints_ShowCurrentKey()                 // Ready → t → ป้ายรถตู้มี "กด T เมื่อพร้อม" · Use เป็นปุ่มเมาส์ขวา → "คลิกขวา ส่องกล้องวิญญาณ"   (Review Focus 5)
[UnityTest] NumpadEnter_Confirms()                 // Confirm action มี binding <Keyboard>/numpadEnter (ข้อค้าง M4A ข้อเล็ก 8)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter SettingsApplyTests`
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** · หน้าจอที่สร้างข้อความไว้แล้ว (เมนูหลัก, รถตู้, Results, เมนู Esc) ฟัง `LanguageChanged` แล้วสร้างข้อความใหม่
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: apply language, mouse, audio and key settings`

---

### Task 6: หน้า Settings

**Files:**
- Create: `Runtime/Settings/SettingsPanel.cs` (MonoBehaviour บน `GameNetwork` Prefab), `Runtime/Settings/RebindRow.cs` (MonoBehaviour หนึ่งแถว)
- Modify: `Runtime/Session/SessionUi.cs` (`CreateSlider`, `CreateToggle`, `CreateChoice`, `CreateTabs`), `Runtime/Session/MainMenuPanel.cs` (ปุ่ม "ตั้งค่า" ใช้ได้), `Runtime/Session/PauseMenu.cs` (ปุ่ม "ตั้งค่า"), `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`
- Test: `Tests/PlayMode/SettingsPanelTests.cs`

**Interfaces:**
- Consumes: `SettingsApplier`, `KeyNames`, `GameData` (Task 3, 5) · `BindingRules.Conflicts` (Task 1)
- Produces:
  - `SettingsPanel`: `bool IsOpen` · `void Open()` / `void Close()` (ปิด = `CaptureBindings` → `SaveSettingsNow()`) · Esc ขณะเปิด = ปิด (ยกเว้นระหว่างรอกดปุ่ม = ยกเลิกการเปลี่ยน) · เปิดจากเมนู Esc → ปิดแล้วกลับเมนู Esc · `internal` สำหรับเทสต์: `ShowTab(int)`, `SetVolume(AudioGroup, float)`, `SetLanguage(string)`, `SetSensitivity(float)`, `SetInvertY(bool)`, `SetFov(float)`, `SetPushToTalk(bool)`, `SetGhostHearsMic(bool)`, `ResetBindings()`, `RebindRow Row(string actionPath, string part = null)`, `string LastText`
  - `RebindRow`: `void StartRebind()` → `PerformInteractiveRebinding(index).WithCancelingThrough("<Keyboard>/escape").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/scroll").OnMatchWaitForAnother(0.1f)` · แสดง "กดปุ่มใหม่… (Esc ยกเลิก)" ระหว่างรอ · `bool Waiting` · `bool Conflicted` (ป้าย "ซ้ำกับ {0}")
  - Localization: `settings.title`, `settings.tab_general`, `settings.tab_audio`, `settings.tab_keys`, `settings.language`, `settings.sensitivity`, `settings.invert_y`, `settings.fov`, `settings.vol_master/music/sfx/ambience/voice/ui`, `settings.push_to_talk`, `settings.ghost_hears_mic`, `settings.reset_keys`, `settings.press_key`, `settings.conflict`, `settings.close`, `action.*` (ชื่อ Action ภาษาคน) (+ อังกฤษ)

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (คีย์บอร์ดจำลอง: `InputSystem.AddDevice<Keyboard>()` แล้วส่ง `KeyboardState` + `InputSystem.Update()`, ลบอุปกรณ์ใน TearDown)
```csharp
[UnityTest] Open_FromMenuAndPause()                 // ปุ่มตั้งค่าเมนูหลัก → IsOpen · Solo รถตู้ → Esc → เมนู Esc → ตั้งค่า → Esc ปิดกลับเมนู Esc · ผู้เล่นยังเดินไม่ได้จนปิดเมนู Esc
[UnityTest] Changes_ApplyNowAndSaveOnClose()        // SetVolume(Master,.5) → AudioListener .5 ทันที · ไฟล์ settings.json ยังไม่เปลี่ยน · Close → ไฟล์มี master 0.5
[UnityTest] Language_RebuildsPanelText()            // SetLanguage("en") → LastText มี "Settings" · "th" → "ตั้งค่า"
[UnityTest] Rebind_PressedKeyBecomesBinding()       // Row("Gameplay/Ready").StartRebind → กด T → Ready = <Keyboard>/t · ป้ายแถวแสดง "T"   (Review Focus 5)
[UnityTest] Rebind_EscapeCancels()                  // StartRebind → กด Esc → Ready ยังเป็น r · Waiting false · แผงยังเปิด   (Review Focus 5)
[UnityTest] Rebind_MouseMoveIgnored()               // StartRebind → ขยับเมาส์จำลอง → ยัง Waiting · กด T → t
[UnityTest] Rebind_ConflictIsShownOnBothRows()      // Ready → space → Row(Ready).Conflicted และ Row(Jump).Conflicted · LastText มี "ซ้ำกับ"
[UnityTest] ResetBindings_RestoresDefaults()         // หลังเปลี่ยน 2 ปุ่ม → ResetBindings → ค่าเริ่มต้น · Close → settings.json bindingOverrides ว่าง
[UnityTest] Rebind_SurvivesRestart()                // เปลี่ยน Ready → t, Close → TearDown/Boot ใหม่ด้วยโฟลเดอร์เดิม → Ready = t   (Review Focus 5)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter SettingsPanelTests`
- [ ] **Step 3: เขียนโค้ดตาม Interfaces + สร้างฉากใหม่** (`CreateGameScenes`, `LocalizationSetup.Setup`) · ระหว่างแผงเปิด `PlayerMotor.Local.InputEnabled = false` และ `GameCursor.Capture(false)` · ปิดแล้วคืนตามเดิม (ถ้าเมนู Esc ยังเปิดอยู่ ยังไม่ Capture)
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4B: settings page`

---

### Task 7: ตรวจทั้งระบบ — Relay, ภาพหน้าจอ, เอกสาร

**Files:**
- Modify: `Dev/DevFlowBot.cs` (SUMMARY `money=<ยอดก่อน>→<ยอดหลัง>`), `Tools/m4a-flow-test.ps1` (ตรวจเงินและชื่อ), `docs/TESTING.md`, `docs/STATUS.md`, `CHANGELOG.md`, `docs/DECISIONS.md` (ข้อเทคนิค: ไม่มี Audio Mixer ใน M4B), `docs/superpowers/plans/2026-10-31-m4-overview.md` (ผล M4B)
- Create: `docs/screenshots/m4/settings-general-th.png`, `settings-keys-th.png`, `menu-name-money-th.png`, `lobby-names-th.png`

**Interfaces:**
- Consumes: ทุก Task ก่อนหน้า

- [ ] **Step 1: ขยายบอทและสคริปต์ให้ล้มเหลวก่อน** — สคริปต์ต้องการ: Client `money` เพิ่ม = ค่าจ้างงาน 1 + ค่าจ้าง Host ออกในงาน 2 (50 สำหรับ 2 คน), Host เพิ่ม = ค่าจ้างงาน 1, ทุกโปรไฟล์มีไฟล์ `save_v1.json` ของตัวเองใต้ `profiles/<profile>/` · ชื่อ: แต่ละเครื่องเห็นชื่อทุกคนใน Lobby · รันกับ Build เก่า (ก่อน Task นี้) → FAIL เพราะไม่มี `money=`
- [ ] **Step 2: Build ใหม่และรัน** — `Tools\build.ps1 -Development` · `Tools\m4a-flow-test.ps1` (3 รอบ) · `-Clients 2` (1 รอบ) · `-LatencyMs 150` (1 รอบ) · `-VersionMismatch` (1 รอบ) → PASS ทุกกรอบ
- [ ] **Step 3: ภาพหน้าจอจาก Build จริง (หน้าต่าง)** — หน้า Settings แท็บทั่วไป + แท็บปุ่ม (ภาษาไทยไม่ซ้อน/ไม่ม่วง), เมนูที่มีชื่อ+เงินสะสม, รถตู้ที่มีชื่อ 2 คน · ดูภาพจริงก่อนบันทึก · ลองเปลี่ยนปุ่ม 1 ปุ่มในหน้าต่างจริงแล้วเปิดเกมใหม่ว่ายังอยู่
- [ ] **Step 4: เอกสาร** — TESTING (Test ใหม่ + ผล Relay), STATUS, CHANGELOG, DECISIONS (Audio Mixer), m4-overview (ผล M4B + ข้อค้าง)
- [ ] **Step 5: Commit** — `M4B: save and settings over Relay, screenshots and docs`
