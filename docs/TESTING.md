# TESTING — วิธีทดสอบ Spirit Movers

> อัปเดต: 2026-11-01 (M4A) · สถานะงานตาม MASTER_PROMPT 14: ทดสอบในเครื่องเดียวได้สูงสุด `Tested` · `Verified` ต้องมาจากผู้เล่นจริงบนเครื่องจริง/ผ่านอินเทอร์เน็ต (ระบุใครและเมื่อไร)

## 1. ทดสอบอัตโนมัติ (Claude รันเองได้)
ปิด Unity Editor ก่อน (Batch Mode ใช้โปรเจกต์ที่เปิดอยู่ไม่ได้)

| คำสั่ง | ทำอะไร | ผลลัพธ์ |
|---|---|---|
| `powershell -ExecutionPolicy Bypass -File Tools\test.ps1` | EditMode Tests (`-Filter ชื่อชุด` รันเฉพาะชุด) | `Logs/test-EditMode.xml`, `Logs/test-EditMode.log` |
| `powershell -ExecutionPolicy Bypass -File Tools\test.ps1 -Platform PlayMode` | PlayMode Tests — โหลดฉาก `M3_TestRoom` เป็น Host ในเครื่อง (ไม่ใช้ UGS) | `Logs/test-PlayMode.*` |
| `powershell -ExecutionPolicy Bypass -File Tools\m3b-carry-test.ps1` | Host + Client Headless ผ่าน Relay จริง 6 สถานการณ์ (`carry`, `shared`, `split`, `remote`, `disconnect`, `crash`) · `-LatencyMs 150` จำลองความหน่วง — Build ก่อนด้วย `-executeMethod SpiritMovers.Editor.TestRoomSetup.BuildTestRoom` | PASS/FAIL ต่อสถานการณ์, Log ใน `Logs/m3b/` |
| `powershell -ExecutionPolicy Bypass -File Tools\m3c-job-test.ps1` | Host + Client Headless ผ่าน Relay เล่นจบงาน (Seed 1..10 ค่าเริ่มต้น) · `-Clients 3` = 4 คน · `-FirstSeed N`, `-Seconds N`, `-LatencyMs N` · ผ่านเมื่อทุกเครื่อง `ok=true` และ ending/accepted/teamTotal/stars ตรงกัน · **`-Scenario ghost`** (ใช้ `-Seconds 75`): Client คนแรกส่องกล้อง → ผีล่าแล้วลากเข้าตู้ → Host จัดของ 1 ชิ้นแล้วเปิดตู้ → ทุกคนไปรถ ปิดงาน · ผ่านเมื่อ captured/released/film/ending ตรงกันทุกเครื่อง และภาพซ้อนเห็นเฉพาะคนส่อง — Build ก่อนด้วย `-executeMethod SpiritMovers.Editor.TestRoomSetup.BuildTestHouse` | PASS/FAIL ต่อรอบ, Log ใน `Logs/m3c/` |
| `powershell -ExecutionPolicy Bypass -File Tools\build.ps1` | Build Windows + Zip (ฉาก Boot → MainMenu → Lobby → Job_Greybox) · `-Development` = Development Build สำหรับบอท ไม่ Zip | `Builds/SpiritMovers-v<ver>-Windows(.zip)` / `-Windows-Dev`, `Logs/build.log` |
| `powershell -ExecutionPolicy Bypass -File Tools\m4a-flow-test.ps1` | เส้นทางเต็มผ่าน UI จริงบน Relay (Development Build): Host สร้างห้อง → Client ใส่รหัส → Ready → งาน 2 รอบ (รอบแรกปิดงานที่รถ กลับรถตู้; รอบสอง Host ออกกลางงาน) · Client "มาสาย" ลองเข้าระหว่างงานต้องถูกปฏิเสธ · `-Clients N`, `-LatencyMs N`, `-VersionMismatch` (Client เวอร์ชัน 0.0.0 ต้องได้ error.version) | PASS/FAIL ต่อรอบ, Log ใน `Logs/m4a/` |

- ใช้ Editor ตัวอื่น: ตั้ง `UNITY_EXE` เป็น Path ของ `Unity.exe`
- **Build ห้ามใช้ `-nographics`** — Material ที่สคริปต์สร้างจะเป็นสีม่วงแดงใน Build (พบใน M3B) · Test และการรัน Player แบบ Headless ใช้ได้ตามปกติ
- **ห้ามลบแล้วสร้าง Material ใหม่ใน Session ที่ Build** (ของเป็นสีม่วงแดง) · **MonoBehaviour ต้องอยู่ไฟล์ละคลาสชื่อตรงกัน** (ไม่งั้นฉาก level0 ใน Build เสีย Player แครชตอนโหลด) — พบใน M3C · Test ผ่านทั้งที่ Build เสีย จึงต้องเปิด Build แบบมีหน้าต่างดูภาพทุกครั้งที่เพิ่ม Material/สคริปต์ และค้น Log หา `is missing or no valid script`
- ทุกครั้งหลัง Compile/Test/Build ต้องอ่าน Log (MASTER_PROMPT 3.4)
- Smoke test ของ Build: เปิด `SpiritMovers.exe -batchmode -nographics -logFile <path>` แล้วตรวจบรรทัด `[Boot] Spirit Movers v<ver>`

### Test ที่มีตอนนี้
| ชุด | ครอบคลุม | ผลล่าสุด |
|---|---|---|
| `GameVersionTests` (EditMode) | Parse/TryParse, การตรวจเวอร์ชันตรงกันสำหรับเข้าห้อง | ผ่าน (2026-10-30) |
| `ThaiShaperTests` (EditMode) | กฎจัดรูปวรรณยุกต์/สระไทย (D-035) | ผ่าน (2026-10-30) |
| `TrueTypeGlyphNamesTests` (EditMode) | อ่านชื่อ Glyph สำรองจากฟอนต์ Mali | ผ่าน (2026-10-30) |
| `ThaiFontSupportTests` (EditMode) | Register บนฟอนต์ที่ยังไม่ถูกใช้ (กันถอยหลัง) · `ThaiShaper.Unshape` | ผ่าน (2026-10-30) |
| `LocalizationTableTests` (EditMode) | ทุกคีย์ในตาราง `UI` มีทั้งไทยและอังกฤษ | ผ่าน (2026-10-31) |
| `RoomEvaluatorTests` (EditMode) | วางของถูกตำแหน่ง/มุม/ห้อง (รวม q กับ −q) · ประเมินห้อง กาวนับ 0.5 · เกณฑ์รับห้อง 80% | ผ่าน (2026-10-31) |
| `AngerModelTests` (EditMode) | ความโกรธ 5 ขั้น · ตัวคูณ · ไหว้ห้องละครั้ง · ความเงียบ · ไม่เปลี่ยนหลังไล่ออก | ผ่าน (2026-10-31) |
| `JobGeneratorTests` (EditMode) | สุ่มงานจาก Seed แบบ Deterministic · จำนวนห้อง/ของตามระดับและจำนวนคน · ประเภทปัญหา · บ้านช่องไม่พอ | ผ่าน (2026-10-31) |
| `PayoutCalculatorTests` (EditMode) | ค่าจ้าง 3 แบบจบ · โบนัสดาว · หัก Hint ไม่ติดลบ · แบ่งคน | ผ่าน (2026-10-31) |
| `JobProgressTests` (EditMode) | สถานะงาน Briefing → Exploring → Farewell → Ended · คำสั่งซ้ำ/ผิดลำดับถูกเพิกเฉย · กลับก่อนตั้งแต่ Briefing · ปิดงานตอนผีอำลาไม่มีผล | ผ่าน (2026-10-31) |
| `MovementRulesTests`, `CarryRulesTests` (EditMode) | ความเร็ว/Stamina/ของหนัก/เสียงฝีเท้า · จุดถือไม่ขึ้นกับ Pitch · จำกัดความเร็วตามมือ · ตรวจของหลุดมือ | ผ่าน — EditMode รวม 141/141 (2026-10-31) |
| `PlayerMotorTests` (PlayMode) | เดิน/วิ่ง/ย่อ/Stamina หมด/กระโดด/ปิด Input/ขึ้นบันได | ผ่าน (2026-10-31) |
| `CarryTests`, `CarryAuthorityTests` (PlayMode) | ยก/วาง/หมุน/โยน · ของหนักช้าและโยนไม่ได้ · เอื้อมไม่ถึง · แย่งของ · ปิด Input ขณะถือ · Host บังคับปล่อย · คนถือหลุด · Host ใช้ทิศที่ผู้ยกหันจริง | ผ่าน (2026-10-31) |
| `JobStartTests`, `RoomAcceptanceTests`, `BellTests`, `JobEndingTests` (PlayMode, ฉาก `M3_TestHouse`) | เริ่มงานจาก Seed (ครั้งเดียวต่อ Session, ปล่อยของที่ถืออยู่) · Client ตรวจแผนตรงกับ Host ทั้งบ้านและกติกา · ผีรับห้อง/สีกลับ · กริ่งห้องละครั้ง · จบงาน 3 แบบ + ค่าจ้าง · ปิดงานที่รถต้องอยู่ครบ/ไม่ใช่ตอนผีอำลา · ใบสั่งงาน/สรุปงานภาษาไทย | ผ่าน — PlayMode รวม 54/54 (2026-10-31, หลังแก้ตามรีวิว) |
| `Tools/m3b-carry-test.ps1` (2 Instance ผ่าน Relay) | ยกคนเดียว (ตำแหน่งตรงกัน 0.006 ม.) · ยก 2 คนแล้วส่งต่อให้คนเดียว · ยก 2 คนแล้วเดินแยก (ปล่อยเอง) · Client หมุน+โยน · ปิดเกม/เกมแครชขณะถือ (Host ยกต่อได้) | PASS 6/6 ที่ 0 ms และ 150 ms ต่อทาง · รันซ้ำหลังแก้ Build และแก้ทิศหันตอนยก M3C PASS 6/6 ที่ 0 ms · หลัง M3D PASS 6/6 (2026-10-31) |
| `Tools/m3c-job-test.ps1` (หลาย Instance ผ่าน Relay) | Host + 1 Client เล่นจบงาน Seed 1–10 · Host + 3 Client Seed 11 · Client คนแรกยกของ 1 ชิ้นด้วยมือจริงไปวางที่เดิม (Ownership ไป-กลับ) ทุกเครื่องเห็นวางถูก · ทุกเครื่องได้ FullSuccess, รับ 3/3 ห้อง, 390, 3 ดาว ตรงกัน (ไม่ Desync) · ภาพ `docs/screenshots/m3/job-sheet-th.png`, `job-results-th.png` | PASS 10/10 + 1/1 (2026-10-31, หลังแก้ตามรีวิว) · รันซ้ำหลังใส่ผี M3D PASS 10/10 + 1/1 |
| `GhostRulesTests`, `NoiseTests`, `CabinetRulesTests` (EditMode) | ผีถูกมอง (กรวยสายตา/ระยะ/สิ่งบัง) · เลือกเป้าข้ามคนในตู้/คนที่เพิ่งออก · เลือกของแกล้งไม่แตะของที่ถือ/ห้องที่รับแล้ว · เสียงดังไม่เกิน 1 ครั้ง/5 วินาที · เปิดตู้ได้เมื่อของถูกเพิ่ม 1 ชิ้นหรือกล้อง · จำนวนกล้องตามผู้เล่น | ผ่าน (2026-10-31) |
| `PlayerSyncTests`, `NoiseHubTests`, `GhostTests`, `CabinetTests`, `SpiritCameraTests` (PlayMode, บ้าน) | ท่าย่อ/มุมก้มซิงก์ · ไหว้ผีห้องละครั้ง · ฝีเท้า/ของกระแทก/ความเงียบ · ผีถูกมองแล้วหยุด, มองผ่านกำแพงไม่นับ, ล่าขึ้นชั้นลอยได้, แกล้ง/ป่วน, สีตามขั้น · ถูกจับ/ของหลุดมือ/ตู้ล็อก/เพื่อนเปิด/หลุดเองหักเงิน/เล่นคนเดียว/คุ้มกัน/จบงาน/หลุดออกจากเกม · กล้อง: ฟิล์ม, ภาพซ้อนเฉพาะช่องที่ยังไม่ถูกในระยะ, หายใน 3 วินาที, ฟิล์มหมด/ไม่ได้ถือ, ส่องใส่ตู้, Shader | ผ่าน — PlayMode รวม 93/93 (2026-10-31, หลังแก้ตามรีวิว: ถือของแล้วยังมองผีได้ · ไม่จับทะลุกำแพง · ตู้เต็มแล้วผีไม่วนอยู่) |
| `SessionRulesTests` (EditMode) | เข้าห้องได้ไหม (เวอร์ชัน → เต็ม → กำลังทำงาน) · เริ่มงานเมื่อทุกคนพร้อม · ป้าย 1–4 / 5–6 ทดลอง · ค่าจ้างตอน Host ออก · จัดรูปแบบรหัสห้อง | ผ่าน — EditMode รวม 149/149 (2026-11-01) |
| `GameSessionTests`, `MainMenuTests`, `LobbyTests`, `JobCycleTests`, `SessionLeaveTests` (PlayMode, เริ่มจากฉาก Boot จริง) | เมนู → รถตู้ → งาน → Results → รถตู้ · Solo · ข้อความผิดพลาดแปลแล้ว (รหัสผิด, เน็ตล่ม, เวอร์ชันไม่ตรงพร้อมทั้งสองเวอร์ชัน, Host ปฏิเสธระหว่างเชื่อมต่อ, ล้มนอกเมนูกลับเมนู, เหตุผลปฏิเสธเก่าไม่ทับข้อความใหม่) · Ready/นับถอยหลัง/ยกเลิก · ห้องล็อกระหว่างงาน · งานที่สองเริ่มใหม่หมด (ของ กล้อง ตู้ ความโกรธ, เมาส์กลับมาล็อก) · ตู้ถูกปิดฉากแล้วคนในตู้เดินได้ · เน็ตหลุด (NGO หยุดเอง) กลับเมนูพร้อมข้อความ · จอโหลด · เมนู Esc · ข้อความ Host ออกตามช่วง | ผ่าน — PlayMode รวม 128/128 (2026-11-01, หลังแก้ตามรีวิว) |
| `SaveRulesTests`, `JsonFileStoreTests`, `ConnectionPayloadTests` (EditMode) | เงินไม่ติดลบ/ไม่ล้น · จัดชื่อ (ช่องว่าง, อักขระแปลก, อีโมจิ, 20 ตัวไม่ตัดกลางสระ/วรรณยุกต์) · จำกัดค่า Settings · ปุ่มซ้ำ (ยกเว้นโยน/ใช้) · ไฟล์ Atomic + สำรอง 1 รุ่น · ไฟล์เสีย/ว่าง/ค้างครึ่ง → สำรอง · เสียทั้งคู่ → เริ่มใหม่เก็บไฟล์เสีย · Migration ทีละขั้น · ไฟล์รุ่นใหม่กว่าไม่ถูกเขียนทับ · Payload เวอร์ชัน+ชื่อ | ผ่าน — EditMode รวม 177/177 (2026-11-01, หลังแก้ตามรีวิว: ไฟล์ถูกล็อก, เหลือแต่ไฟล์สำรอง, ปุ่มเดียวกันสองครั้งไม่นับว่าซ้ำ) |
| `GameDataTests`, `PlayerNameTests`, `SettingsApplyTests`, `SettingsPanelTests` (PlayMode, โฟลเดอร์ข้อมูลชั่วคราวต่อ Test) | เงินเข้าครั้งเดียวต่องาน (Results, Host ออก, เน็ตหลุดตอน Results, งานที่สอง) · ข้อความไฟล์เสีย · ชื่อจากเมนูถึงรถตู้ · Host อ่านชื่อจาก Payload · ภาษา/ความไว/กลับแกน/FOV/เสียงรวม · ปุ่มที่เปลี่ยนอยู่หลังโหลดใหม่ · JSON ปุ่มเสียใช้ค่าเริ่มต้น · คำแนะนำแสดงปุ่มจริง · หน้า Settings: เปิดจากเมนู/เมนู Esc, มีผลทันที บันทึกตอนปิด, เปลี่ยนปุ่มด้วยคีย์บอร์ดจำลอง, Esc ยกเลิก, ขยับเมาส์ไม่นับ, ปุ่มซ้ำ, คืนค่า, ปุ่มกว้างพออ่าน | ผ่าน — PlayMode รวม 160/160 (2026-11-01, หลังแก้ตามรีวิว: ซ้ำกับปุ่มที่สองของ Action อื่น, ชื่อปุ่มไม่ตามแป้นพิมพ์ไทย, เปลี่ยนฉากตอนหน้า Settings เปิด) |
| `VoiceRulesTests` (EditMode) | ช่องเสียงตามที่อยู่ (รถตู้/บ้าน, Positional ก่อน) · ส่งเสียงไปไหน (วิทยุ = บ้าน + วิทยุ) · จำนวนวิทยุ · ไมค์ → ผี ≤4/วินาที เฉพาะเปิดตัวเลือก · ผีรบกวนวิทยุตามขั้น · จัดข้อความแชต · กติกา Ping | ผ่าน — EditMode รวม 188/188 (2026-11-01) |
| `FakeVoiceServiceTests`, `VoiceDirectorTests`, `WalkieTests`, `MicNoiseTests`, `VoicePeopleTests`, `ChatTests`, `PingTests` (PlayMode, เสียงปลอม ไม่ใช้เน็ต/ไมค์) | เข้า/ออกช่องตามฉาก · ออกห้องแล้ว Logout · Solo ไม่แตะเสียง · Push-to-Talk · Login ไม่ผ่าน/หลุดกลางงาน เกมเล่นต่อ + เชื่อมใหม่ + ยอมแพ้หลัง 12 ครั้ง · เลือกอุปกรณ์ · วิทยุ: จำนวน, ส่งเฉพาะถือ+กด, ทิ้งแล้วหยุด, ได้ยินเฉพาะคนถือ, ไม่มี Tap ค้าง, ผีรบกวน · ไมค์ → ผี (ปิดแล้วไม่ส่งเลย, Host จำกัดอัตรา) · ใครกำลังพูด · ปิด/ปรับเสียงรายคนข้ามฉาก · แชตไทยถูกช่อง, Esc ยกเลิก, จำกัดอัตรา, เสียงหลุดแชตใช้ไม่ได้ · Ping/ข้อความด่วน (จำกัดที่ Host, ใช้ได้แม้ไม่มีเสียง) | ผ่าน — PlayMode รวม 207/207 (2026-11-01, หลังแก้ตามรีวิว: คลิกขวาถือวิทยุพูดได้ไม่ถูกโยน, Enter ส่งแชตไม่ไปกดยืนยัน, ออกห้องแล้วลืมการปิดเสียง, ปิดเสียง/ระดับเสียงมีผลกับวิทยุ, แชตไม่อ่านคำสั่งจัดรูปแบบ) |
| `Tools/m4c-voice-test.ps1` (Relay + Vivox จริง, Development Build, Host พูดจาก WAV ที่สคริปต์สร้างเอง) | รถตู้ได้ยิน · บ้านใกล้ได้ยิน · 40 ม. ไม่ได้ยิน (วัดหลังเดินออกไป 10 วินาที) · วิทยุผ่าน Tap · ทิ้งวิทยุแล้วเงียบ · แชตไทยสองทาง · ไมค์ Host ถึงผี ≤4/วินาที · Ping + ข้อความด่วน · `-VoiceFail` Client เสียงใช้ไม่ได้ยังเล่นงานได้ · ภาพ `chat-thai.png`, `voice-hud-th.png`, `radio-held-th.png`, `ping-quick-th.png` | 3/3 + 150 ms 1/1 + VoiceFail 1/1 PASS (2026-11-01) · **ยังต้องคนฟังจริง:** ความดังตามระยะ 2–25 ม., เสียงวิทยุ, ความไวไมค์ต่อผี |
| `Tools/m4a-flow-test.ps1` (หลาย Instance ผ่าน Relay, UI จริง) | 3 รอบ (2 คน) · 1 รอบ 3 คน · 2 รอบหน่วง 150 ms · เวอร์ชันไม่ตรง · ทุกรอบ: งาน 2 รอบ, Host ออกกลางงานแล้ว Client กลับเมนูพร้อมค่าจ้างห้องที่รับ (50 / 33), คนมาสายถูกปฏิเสธ error.in_job · ภาพ `docs/screenshots/m4/main-menu-th.png`, `lobby-th.png`, `host-left-th.png` | PASS ทั้งหมด (2026-11-01) · รันซ้ำหลังแก้ตามรีวิว: 2/2 + 150 ms 1/1 + เวอร์ชันไม่ตรง 1/1 PASS · **M4B:** ตรวจชื่อในรถตู้ทุกเครื่อง + เงินที่บันทึกเพิ่มตรงค่าจ้าง (Host +ค่าจ้างงาน 1, Client +งาน 1 + ค่าจ้างตอน Host ออก, คนมาสาย +0) + บอทแต่ละตัวมีโฟลเดอร์ `profiles/` ของตัวเอง — 3/3, 3 คน 1/1, 150 ms 1/1, เวอร์ชันไม่ตรง 1/1 PASS · หน้าต่างจริง: เปลี่ยนปุ่ม Ready → T ผ่านหน้า Settings แล้วเปิดเกมใหม่ยังเป็น T และชื่อไทยยังอยู่ (การกดปุ่มจริงด้วยมือยังไม่ได้ลอง — PlayMode ใช้คีย์บอร์ดจำลอง) |
| `Tools/m3c-job-test.ps1 -Scenario ghost` (หลาย Instance ผ่าน Relay) | Seed 1–5 (2 คน) · Seed 11 (4 คน, กล้อง 2 ตัว) · Seed 21–22 จำลอง 150 ms ต่อทาง · Client ย่อตัวจ้องผีขั้นล่าแล้วผีหยุดนิ่ง (Host วัดได้ขยับ 0.00 ม. ทุกรอบ — มองจากเครื่องอื่นใช้ได้จริง) · ทุกเครื่องเห็นถูกจับและออกจากตู้ (เพื่อนช่วย) ตรงกัน ฟิล์มเหลือ 5 · ภาพซ้อนมีเฉพาะเครื่องที่ส่อง (2–4 ชิ้น) เครื่องอื่น 0 · ภาพ `docs/screenshots/m3/spirit-camera.png` (เทียบ `spirit-camera-before.png`), `ghost-hunt.png` จาก Build จริง ไม่มีสีม่วงแดง | PASS 5/5 + 1/1 + 2/2 (2026-10-31) |
| ต้นแบบ Network (`Tools/proto-netsync-matrix.ps1`) | Host+Client ผ่าน Relay, วิธียกของ A/B × Latency 0/100/200 ms | ผ่าน — ผลใน `docs/M2_REPORT.md` |

### ต้นแบบ Voice ในเครื่องเดียว (Claude รันเองได้)
`powershell -ExecutionPolicy Bypass -File Tools\proto-voice-test.ps1 -Wav <WAV mono 16-bit 48 kHz>` → Host ฉีดไฟล์เสียงแทนไมค์, Client ยืนห่าง 0/15/40 ม. → พิมพ์ระดับเสียงที่ได้ยิน + แชตไทยถึงหรือไม่ (ผล 2026-10-30 ใน `docs/M2_REPORT.md` หัวข้อ 3)
- ตัวเลือกเพิ่ม: `-proto-voice-tx radio` ส่งทางวิทยุ · `-proto-chat "ข้อความ"` · `-proto-bot-offset M` · `-proto-screenshot PNG` + `-proto-chat-preview "ข้อความ"` (รันแบบมีหน้าต่าง)
- รันอัตโนมัติที่ไม่ฉีด WAV จะปิดไมค์จริงเสมอ

## 2. ทดสอบหลายผู้เล่นในเครื่องเดียว (วางแผน — M2/M3)
- Multiplayer Play Mode (แพ็กเกจทางการ — ตรวจเวอร์ชันก่อนติดตั้ง) หรือเปิด Build หลาย Instance
- จำลอง Latency/Packet Loss ด้วย Network Simulator ของ Unity Transport (ค่า 50/100/200 ms, Loss 1–5%)
- ผลสูงสุด: `Tested`

## 3. ทดสอบออนไลน์จริง (ผู้ใช้ + เพื่อน)

### Playtest M2 — ต้นแบบ Network (ใช้ปิด G6)
**เตรียม:** Zip โฟลเดอร์ `Builds/Proto-NetSync-v0.0.1/` ทั้งโฟลเดอร์ ส่งให้เพื่อนทางช่องทางที่สะดวก (ยังไม่ใช่ GitHub Release — G8) · เพื่อนต้องอยู่**คนละเครือข่าย** (เช่น บ้านอื่น หรือมือถือเปิด Hotspot) ไม่ใช่ Wi-Fi เดียวกัน
**คำเตือน Windows:** ไฟล์ไม่มี Code Signing → SmartScreen อาจขึ้น "Windows protected your PC" → กด **More info → Run anyway** · ถ้า Firewall ถาม ให้กด **Allow** (Private) — ถ้าไม่ขึ้นก็ไม่ต้องทำอะไร

| # | ขั้นตอน | ผลที่ควรเป็น | ผ่าน? |
|---|---|---|---|
| 1 | ผู้ใช้เปิด `SpiritMovers.exe` → กด **Host** | ภายใน ~20 วินาที ขึ้น "Hosting — code XXXXXX" และมีกล่องหล่นบนพื้น | |
| 2 | ส่งรหัส 6 ตัวให้เพื่อน → เพื่อนเปิดเกม พิมพ์รหัส → **Join** | เพื่อนเข้าได้ภายใน ~10 วินาที ไม่ต้องเปิดพอร์ต/ตั้ง Router · ฝั่ง Host ขึ้น Players: 2 | |
| 3 | ทั้งสองคนเดิน (WASD + เมาส์) | เห็นตัวละคร (แคปซูล) ของอีกฝ่ายเคลื่อนที่ลื่น | |
| 4 | เพื่อนเล็งกล่อง กด **E** ยก เดินไปมา กด **E** วาง | เพื่อนรู้สึกว่ากล่องอยู่ในมือทันที · ผู้ใช้เห็นกล่องตามเพื่อน (อาจช้ากว่าเล็กน้อย) | |
| 5 | เพื่อนกด **M** สลับเป็น HostSimulated แล้วลองยกอีกครั้ง | รู้สึกหน่วงกว่าข้อ 4 (เพื่อยืนยันผลวัด D-034) | |
| 6 | ผู้ใช้ยกกล่องบ้าง + ทั้งคู่ผลักกล่องชนกัน | ตำแหน่งกล่องตรงกันทั้งสองจอหลังหยุดนิ่ง | |
| 7 | เพื่อนปิดเกมขณะถือกล่อง | กล่องไม่หายไปจากฝั่งผู้ใช้ | |
| 8 | (ถ้ามีคนที่ 3–4) ให้เข้าห้องเพิ่ม | เข้าได้และเห็นทุกคน | |
| 9 | ทั้งคู่ยืนใกล้กันแล้วพูด → เดินห่างกัน ~25 m | ได้ยินกันชัดตอนใกล้ เสียงค่อย ๆ เบาลงจนเงียบตอนไกล · ชื่อคนพูดมี `>>` | |
| 10 | เพื่อนกด **R** ค้างแล้วพูดตอนอยู่ไกล | ได้ยินผ่าน "วิทยุ" แม้อยู่ไกล — เสียงแหลมแตกแบบวอล์กกี้ทอล์กกี้ · ปล่อย R แล้วกลับเป็นใกล้ได้ยิน (เสียงปกติ) | |
| 11 | กด **V** / ปุ่ม Voice chat: OFF | ปิดไมค์ / ปิดทั้งพูดและฟัง · มิเตอร์ "Ghost hears you" เป็น 0 เมื่อกด "Ghost ignores me" | |
| 12 | กด **Enter** พิมพ์ภาษาไทย (เช่น "ปู่ฟื้นน้ำ ญาติผู้ใหญ่") → Enter | อีกฝ่ายเห็นข้อความ วรรณยุกต์ไม่จมหาย · ระหว่างพิมพ์ตัวละครไม่เดิน | |
| 13 | กด **F2** | ข้อความช่วยเหลือมุมขวาล่างสลับไทย ↔ English ทันที | |

**บันทึกเพิ่ม:** ตัวเลข Received KB/s ที่มุมจอ · ความรู้สึกหน่วง (ไม่หน่วง / พอรับได้ / หน่วงมาก) · ระยะทางโดยประมาณ (เมืองเดียวกัน/ต่างจังหวัด) · อินเทอร์เน็ตแต่ละฝั่ง (บ้าน/มือถือ)

**Log ที่ต้องส่งกลับ (ทั้งสองเครื่อง):** `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\Player.log` (วางใน Explorer Address bar ได้เลย) — ส่งหลังปิดเกม เพราะเปิดเกมใหม่จะเขียนทับ (ไฟล์ก่อนหน้าคือ `Player-prev.log`)

**แบบรายงานสั้น:** วันที่ · ผู้ทดสอบ (ชื่อเล่น) · ผ่านข้อไหน/ไม่ผ่านข้อไหน · อาการที่เจอ · แนบ Log 2 ไฟล์

### ตำแหน่ง Log ของ Player บน Windows
Unity เขียน `Player.log` ไว้ที่ `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\` → สำหรับเกมนี้คือ `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\Player.log` (ตรวจยืนยันกับ Build จริงก่อนแจกเพื่อน)
