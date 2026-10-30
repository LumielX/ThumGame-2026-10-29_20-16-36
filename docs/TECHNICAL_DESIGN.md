# Spirit Movers — Technical Design

> เวอร์ชัน 0.1 (ร่างสำหรับ G5) · 2026-10-30 · อ้างอิง GDD และ D-001–D-027
> เอกสารนี้รวม **การตัดสินใจทางเทคนิคประเภท C** (Claude ตัดสินเองตาม MASTER_PROMPT 2.2/Phase 4) พร้อมเหตุผล — ทุกข้อที่ระบุ **[พิสูจน์ M2]** ต้องยืนยันด้วยของจริงก่อนสร้างระบบต่อยอด
> **ยังไม่ได้ตรวจเวอร์ชันแพ็กเกจหรือ API ใน Editor จริง** — ชื่อ API/แพ็กเกจที่อ้างถึงต้องตรวจกับเอกสารทางการและ Package Manager ของ 6000.3.27f1 ใน M2 ก่อนใช้ (MASTER_PROMPT 10)

---

## 1. หลักการ
1. **ออนไลน์ตั้งแต่แรก** — ทุกระบบเกมเขียนแบบ Host-authoritative ตั้งแต่ Prototype (MASTER_PROMPT 12 M3)
2. **แยกตรรกะออกจาก Unity** — กติกาเกม (ประเมินห้อง ความโกรธ เศรษฐกิจ Save) เป็น C# ล้วนที่ทดสอบได้ด้วย EditMode Test
3. **แยกเลเยอร์ Network และ Voice** หลัง Interface — เปลี่ยนบริการได้โดยไม่เขียนเกมใหม่ (MASTER_PROMPT 9.1, ทางหนี EOS ใน D-008)
4. **ค่าตั้งค่าอยู่นอกโค้ด** — ScriptableObject สำหรับบ้าน ผี เครื่องมือ ระดับงาน ตารางปรับตามจำนวนผู้เล่น
5. **ไม่ฝังข้อความในโค้ด** — ทุกข้อความผ่าน Localization
6. **Cross-platform ราคาต่ำ** — ใช้ `Application.persistentDataPath` ไม่ใช้ API เฉพาะ Windows (MASTER_PROMPT 1.4)
7. **วัดก่อนอ้าง** — Performance และสเปกขั้นต่ำมาจาก Profiler บน Build จริงเท่านั้น

## 2. โครงสร้างโปรเจกต์ (MASTER_PROMPT 0.1)
```
Assets/_Game/
  Scripts/
    Core/            SpiritMovers.Core        (C# ล้วน ไม่อ้าง UnityEngine ยกเว้นชนิดข้อมูลพื้นฐานที่จำเป็น)
    Runtime/         SpiritMovers.Runtime     (MonoBehaviour เกม: Player, Interaction, Ghost, Job, Audio)
    Net/             SpiritMovers.Net         (Adapter ของ NGO + Multiplayer Services)
    Voice/           SpiritMovers.Voice       (Adapter ของ Vivox)
    UI/              SpiritMovers.UI
    Editor/          SpiritMovers.Editor      (Build Script, เครื่องมือ Asset, Screenshot)
    Tests/EditMode/  SpiritMovers.Tests.EditMode
    Tests/PlayMode/  SpiritMovers.Tests.PlayMode
  Scenes/ Prefabs/ Art/ Audio/ UI/ Fonts/ Localization/ Settings/
Assets/ThirdParty/<Source>/   (ไม่แก้ไฟล์)
ArtSource/Blender/            (.blend ผ่าน LFS)
Tools/                        (build.ps1, test.ps1, สคริปต์ Blender Python)
```
**ทิศทางการอ้างอิง Assembly:** `Core ← Runtime ← UI` · `Core ← Net` · `Core ← Voice` · Runtime ใช้ Net/Voice ผ่าน Interface ที่ประกาศใน Core (เช่น `INetworkSession`, `IVoiceService`) — ห้าม Core อ้างแพ็กเกจเครือข่าย/เสียง

## 3. สถานะของแอป (State Machine)
```
Boot → (ตรวจ Save/Settings/Localization) → MainMenu
MainMenu → Lobby (Host สร้าง / Client เข้าด้วยรหัส / Solo = Host ที่ไม่เปิดรับคนอื่น)
Lobby → Loading → Job → Results → Lobby
Job: Briefing → Exploring ↔ (ทุกห้องผีรับ) → Farewell → Exit | Evicted | LeftEarly
Error/Disconnected → MainMenu (พร้อมข้อความและบันทึกค่าจ้างที่ได้แล้ว)
```
- Solo ใช้เส้นทางเดียวกับ Host (Session ไม่เปิดรับ) → ระบบเดียวกันทั้งหมด ลดโค้ดซ้ำ

## 4. Puzzle Framework (D-004)
### 4.1 ข้อมูล
- `HouseDefinition` (ScriptableObject): ห้อง, `MemorySlot[]` (ของ, ตำแหน่ง, ทิศ, ระยะคลาดเคลื่อนตำแหน่ง/มุม, ห้องเริ่มต้นที่เป็นไปได้, ประเภทปัญหาที่รองรับ), จุดซ่อน, กริ่ง, จุดเกิดผี
- `GhostDefinition`: บุคลิก (ขี้อาย/หูไว), ชุดการแกล้ง, ค่าความโกรธ, ข้อความรีวิว (Localization keys), หน้าอัลบั้ม, เนื้อหาความทรงจำต่อบ้าน
- `JobDifficulty` + `PlayerCountScaling` (ตารางคูณ 1–6 คน)

### 4.2 ตรรกะ (Core, ทดสอบได้)
- `JobGenerator(seed, house, ghost, difficulty, playerCount)` → เลือกช่อง + ประเภทปัญหา + ตำแหน่งรื้อ — **Deterministic จาก Seed** (Host สร้าง Seed แล้วซิงก์ให้ Client เพื่อ Debug/ทำซ้ำบั๊กได้)
- `RoomEvaluator.Evaluate(slotStates)` → จำนวนของที่ถูก (ใช้กับผีพยักหน้า) + ผ่านเกณฑ์รับห้องหรือไม่ · ของที่ซ่อมด้วยกาวนับ 0.5
- `AngerModel` → ขั้น 0–4 จาก Event (แตก, วางผิด, เสียง, ใช้กล้อง/กริ่ง, เวลา) และการลด (รับห้อง, ไหว้, ความเงียบ) · ค่าทั้งหมดจาก Config
- `PayoutCalculator` → ค่าจ้างตามผลจบงาน หัก Hint แบ่งเท่ากัน

### 4.3 Runtime
- ประเมินห้อง **บน Host เท่านั้น** เมื่อของในห้องหยุดนิ่ง (Event-driven + ตรวจเป็นระยะ ไม่ตรวจทุกเฟรม)
- ผลลัพธ์ (จำนวนถูก, ห้องที่รับ, `Restoration` ของห้อง) ซิงก์ด้วย NetworkVariable

## 5. Ghost AI (D-005, D-019)
- ทำงานบน **Host เท่านั้น** · Client เห็นผ่านตำแหน่ง/สถานะที่ซิงก์
- State Machine: `Idle(ฮัม) → Prank → Disturb → Hunt → Drag → Evict` ผูกกับขั้นความโกรธ
- การนำทาง: `com.unity.ai.navigation` (มีใน manifest แล้ว 2.0.16 — ตรวจ API ใน M2) + การลอยผ่านเฟอร์นิเจอร์แบบ Custom
- **การรับรู้:**
  - *ขี้อาย:* ถือว่า "ถูกมอง" เมื่อผีอยู่ในมุมกล้องผู้เล่นคนใดและไม่มีสิ่งบัง (Raycast) — Client ส่งเฉพาะทิศกล้อง (ซิงก์อยู่แล้วกับตัวละคร) Host คำนวณเอง
  - *หูไว:* ระบบ `NoiseEvent(position, loudness, source)` จากฝีเท้า ของตก ของแตก วิทยุ และ **เสียงไมค์** (ข้อ 8.3)
- Jumpscare ผ่าน Event ที่เช็กตัวเลือก Accessibility ฝั่ง Client

## 6. ผู้เล่น การโต้ตอบ และการยกของ (D-006, D-015, D-021)
- Character Controller แบบ Kinematic สำหรับการเดินปกติ + สลับเป็น Ragdoll ชั่วคราว (Active Ragdoll แบบเบา) แล้วกลับ
- `IInteractable` (ยก, ใช้, ปลอบ, กดกริ่ง, เปิดตู้) · ระยะ/มุมโต้ตอบตรวจบน Host
- **การยกของ — [พิสูจน์ M2] เปรียบเทียบ 2 แนวทางก่อนเลือก:**
  - (A) **Host จำลองทั้งหมด:** Client ส่ง Input การยก → Host ขยับวัตถุด้วย Joint/แรง → ซิงก์ตำแหน่ง · ถูกต้องที่สุดแต่ผู้ถือรู้สึกหน่วงตามค่า Ping
  - (B) **โอนสิทธิ์ระหว่างถือ:** ผู้ถือเป็น Owner ของวัตถุชั่วคราว (ขยับในเครื่องตัวเองทันที) → Host ตรวจความสมเหตุสมผลและรับสิทธิ์คืนเมื่อปล่อย · ตอบสนองดี แต่การชน/ช่วยกันยกซับซ้อน
  - **ค่าเริ่มต้นที่วางแผน:** (B) สำหรับของที่ถือคนเดียว และ (A) สำหรับของที่ช่วยกันยกหลายคน — ตัดสินจริงจากผลทดสอบ Latency 50/100/200 ms ใน M2 แล้วบันทึกใน `DECISIONS.md`
- วัตถุที่หยุดนิ่ง "หลับ" (Rigidbody sleep) และไม่ส่งข้อมูล — ควบคุม Bandwidth
- ของแตก: แทนที่ด้วย Prefab เศษ (Pool) · เครื่องมือเป็น NetworkObject ประเภทเดียวกับของในบ้าน

## 7. Networking (D-008)
- **Netcode for GameObjects 2.x** + **Multiplayer Services SDK (Sessions)** — สร้าง Session (Lobby + Relay + Join Code) · **[พิสูจน์ M2]** เวอร์ชันที่ใช้ได้จริงใน 6000.3.27f1 (เอกสารแสดง NGO 2.13.3 สถานะ pre-release) และการเชื่อมต่อผ่านอินเทอร์เน็ตโดยไม่เปิดพอร์ต
- **Authentication:** Anonymous Sign-in ของ Unity Authentication (ผู้เล่นไม่ต้องสมัคร)
- **สิ่งที่ซิงก์:**
  | ข้อมูล | วิธี |
  |---|---|
  | สถานะงาน, Seed, ขั้นความโกรธ, ห้องที่รับ, Restoration | NetworkVariable (Host เขียน) |
  | ผู้เล่น (ตำแหน่ง ทิศกล้อง ท่าทาง ของที่ถือ) | NetworkTransform + NetworkVariable |
  | ของในบ้าน/เครื่องมือ | NetworkObject + NetworkTransform/NetworkRigidbody (ตามผลข้อ 6) |
  | เหตุการณ์ (แตก, แกล้ง, ผีพยักหน้า, Emote) | RPC |
- **ตรวจเวอร์ชัน:** เวอร์ชันเกมใส่ใน Session Property + Connection Approval Payload → ไม่ตรง = ปฏิเสธพร้อมเหตุผลที่แปลแล้ว
- **Host ออก:** Client ตรวจจับการหลุด → บันทึกค่าจ้างของห้องที่รับแล้ว (ข้อมูลที่ Client รู้อยู่แล้วจาก NetworkVariable) ลง Save → MainMenu พร้อมข้อความ
- **ผู้เล่นหลุด:** Host ปล่อยของที่ถือ (โอนสิทธิ์คืน) · ทำเครื่องหมายช่องว่าง · ไม่ปรับขนาดงานกลางคัน
- **Interface:** `INetworkSession` (Create/Join/Leave/Events) และ `INetworkGame` (Spawn/Authority helpers) ใน Core → Implement ใน Net
- **งบ Bandwidth/Tick:** ตั้งค่าเริ่มต้นแล้ววัดจริงใน M2 (บันทึก KB/s ต่อผู้เล่น และ GiB ต่อรอบ เทียบ Free Tier)

## 8. Voice (D-009, D-010)
### 8.1 Interface
`IVoiceService`: Initialize, JoinLobbyChannel, JoinHouseChannel(positional), SetRadioTransmit(bool), SetMute, SetInputDevice, SetPlayerVolume, events: ParticipantSpeaking/Energy, ConnectionLost
### 8.2 Vivox — **[พิสูจน์ M2]**
- Lobby: Group channel
- ในบ้าน: Positional channel (อัปเดตตำแหน่ง 2–4 ครั้ง/วินาทีตามคำแนะนำในเอกสาร Vivox) — เข้าก่อน Group channel ตามที่เอกสารระบุ
- **วิทยุ:** Group channel แยก "radio" — ผู้ถือวิทยุกดค้าง = ส่งเสียงเข้าช่องวิทยุ · ผู้ฟังที่มีวิทยุได้ยินผ่าน **Audio Tap** + ฟิลเตอร์เสียงวิทยุ (Band-pass + Distortion) · ผีรบกวน = เพิ่มเสียงซ่า/ตัดเป็นช่วง
- เสียงก้องในบ้าน: Participant Audio Tap → Audio Mixer (Reverb ตามโซนห้อง)
### 8.3 ผีหูไวฟังเสียงพูด
- Client อ่าน `AudioEnergy` ของ **ตัวเอง** (ถ้าเปิดตัวเลือก "ให้ผีได้ยินเสียงไมค์ของฉัน") → เกินเกณฑ์ ⚙ → ส่ง `NoiseEvent` ไป Host แบบจำกัดความถี่ (เช่น ไม่เกิน 4 ครั้ง/วินาที)
- ปิดตัวเลือก = ไม่ส่งเลย (ความเป็นส่วนตัว + ความยุติธรรม)

## 9. Save และ Settings
- **ไฟล์:** `Application.persistentDataPath/save_v{n}.json` และ `settings.json` แยกกัน
- **Schema Version** + ตัวแปลง (Migration) ทดสอบด้วย EditMode Test
- **เขียนแบบ Atomic:** เขียนไฟล์ชั่วคราว → แทนที่ · เก็บสำรอง 1 รุ่น · ไฟล์เสีย → ใช้สำรอง + แจ้งผู้เล่น
- **ข้อมูล:** เงิน, ยศ/XP, ของแต่งตัวที่มี/สวม, อัลบั้มผี, สถิติ, สถานะการสอน · Settings: ภาษา, กราฟิก, เสียง, Voice, Key Bindings (JSON ของ Input System), Accessibility
- ไม่มีการเข้ารหัส/กันโกง (MASTER_PROMPT 0) · ไม่มีข้อมูลส่วนตัวนอกจากชื่อที่ผู้เล่นตั้ง

## 10. Localization และภาษาไทย — ความเสี่ยงสูง **[พิสูจน์ M2 — G6]**
- **ระบบข้อความ:** แพ็กเกจทางการ Unity Localization (String Tables TH/EN, Smart Strings) — ตรวจเวอร์ชันที่รองรับ 6.3 ใน M2
- **ตัวเรนเดอร์ข้อความ:** TextMeshPro (รวมอยู่ใน `com.unity.ugui` ตั้งแต่ Unity 6 — ตรวจใน M2) หรือ UI Toolkit — เลือกจากผลทดสอบภาษาไทย
- **ปัญหาที่ต้องทดสอบ:**
  1. **สระบน/ล่างและวรรณยุกต์ซ้อนผิดตำแหน่ง** (ต้องการการวางตำแหน่ง Mark ของฟอนต์) — ทดสอบด้วยคำที่ยาก เช่น "ปั้น", "ผู้ใหญ่", "กี่", "ญี่ปุ่น", "ฤๅษี"
  2. **การตัดบรรทัด** — ภาษาไทยไม่มีช่องว่างระหว่างคำ → แนวทาง: ตัดคำด้วยพจนานุกรมแล้วแทรก Zero-Width Space (U+200B) ก่อนแสดงผล (ทำตอน Build String Tables สำหรับข้อความคงที่ และ Runtime สำหรับข้อความผู้เล่น/ชื่อ) — แหล่งพจนานุกรมต้องตรวจ License
  3. **พิมพ์ไทยผ่าน IME ในแชต** (D-013)
  4. ความยาว/ความสูงข้อความในทุกหน้าจอ
- **ฟอนต์:** ฟอนต์ไทย License OFL/CC0 ทรงกลมหนาตาม Art Bible (เลือกและบันทึก License ใน M2)
- ถ้าวิธีมาตรฐานไม่ผ่าน → รายงานผู้ใช้ที่ G6 พร้อมทางเลือก ก่อนสร้าง UI ต่อ

## 11. Input
- Unity Input System (มีแล้ว 1.20.1 ใน manifest) · Action Map: Gameplay / UI / Voice · Rebinding และเก็บ Override เป็น JSON ใน Settings
- ออกแบบ Action ให้รองรับ Gamepad ภายหลัง (D-012) โดยยังไม่ทำ UI Navigation ครบใน v0.1
- แสดงชื่อปุ่มตาม Binding ปัจจุบันในทุกคำแนะนำ (D-024)

## 12. Rendering และ Performance
- **URP** (มีแล้ว 17.3.0) · Shader ร่วม: Cel Shading 3 ระดับ + พารามิเตอร์ `Restoration` (ซีด ↔ สด) ราย "ห้อง" — ส่งค่าราย Renderer ด้วย MaterialPropertyBlock หรือค่ารายห้องผ่าน Global Array (เลือกตามผลต่อ SRP Batcher/Draw Call ใน Prototype)
- **Outline:** Inverted Hull (ต่อวัตถุ) หรือ Screen-space Edge Detection — เลือกจาก Performance + คุณภาพในห้องแคบ
- **ภาพซ้อนความทรงจำ:** Shader โปร่งแสงอำพัน แสดงเฉพาะในกล้องของผู้ถือกล้องวิญญาณ (Layer/Render Feature เฉพาะ Client นั้น)
- **งบเริ่มต้น ⚙ (ปรับหลังวัด):** สามเหลี่ยมต่อเฟรม ~0.5–1 ล้าน · Draw Call เป้าหมาย < 1,500 · Texture ส่วนใหญ่ใช้ Palette Texture เล็ก · ไฟ Real-time ที่มีเงาพร้อมกันจำกัด · หมอกเฉพาะโซน
- **เป้าหมาย FPS:** ตั้งเป้า 60 FPS ที่ 1080p บนเครื่องพัฒนา (RTX 5050 Laptop) — **ไม่ใช่การรับประกัน** สเปกขั้นต่ำประกาศจากผลวัดเท่านั้น
- Object Pooling สำหรับเศษของแตก, เอฟเฟกต์, เสียง · ลด GC Allocation ในลูปเกม

## 13. Audio
- Audio Mixer 6 กลุ่ม (Master/Music/SFX/Ambience/Voice/UI) ผูกกับ Settings
- Spatial Audio 3D สำหรับเสียงในโลก · เพลงจากวิทยุ/ทีวีในบ้านเป็น AudioSource ในโลก (D-022) และสร้าง `NoiseEvent` เมื่อเปิด
- Sting ผีรับห้อง, เสียงฮัมผี (สงบ/หงุดหงิด/โกรธ) ซิงก์ด้วยสถานะความโกรธ
- คำบรรยายเสียงสำคัญ (D-012): เสียงที่มีความหมายต่อการเล่นลงทะเบียน Caption Key + ทิศทางเทียบผู้ฟัง

## 14. UI
- ค่าเริ่มต้น **UGUI** (เหมาะกับ UI ในโลก เช่น ใบสั่งงาน/กระดานงานในรถตู้) — ยืนยันหลังทดสอบภาษาไทย (ข้อ 10)
- ใบสั่งงานเป็นวัตถุในมือ (World-space Canvas) · HUD เป็น Screen-space ขั้นต่ำ (D-023)
- ทุกข้อความผูก Localization Key · Layout ยืดหยุ่นสำหรับข้อความไทยที่ยาว/สูงกว่า

## 15. Logging, Error Handling, รายงานบั๊ก
- Logger กลางพร้อมหมวด (Net, Voice, Ghost, Job, Save, UI) และระดับ · ไม่กลืน Exception (MASTER_PROMPT 10)
- Log ของ Player อยู่ที่ไฟล์ Log มาตรฐานของ Unity บน Windows (ระบุตำแหน่งจริงใน README หลังตรวจใน M2)
- **ปุ่มสร้างชุดรายงานบั๊ก** (D-013): รวม Player Log ล่าสุด + เวอร์ชันเกม + ข้อมูลเครื่องพื้นฐาน (OS, CPU, GPU, RAM) + Settings → ZIP บน Desktop/โฟลเดอร์ที่ผู้เล่นเลือก · ไม่มีข้อมูลส่วนตัว/Token

## 16. การทดสอบ (รายละเอียดเต็มใน `docs/TESTING.md` — สร้างใน M2)
| ระดับ | สิ่งที่ทดสอบ | ผู้ทำ |
|---|---|---|
| EditMode (Core) | RoomEvaluator, AngerModel, JobGenerator (Deterministic), PayoutCalculator, Save Migration, ตัดคำไทย | Claude (อัตโนมัติผ่าน CLI `-runTests`) |
| PlayMode | Interaction, ผีรับห้อง, ตู้ความทรงจำ, State Machine งาน | Claude |
| Multi-instance ในเครื่องเดียว | Multiplayer Play Mode (แพ็กเกจทางการ — ตรวจใน M2) + Network Simulator ของ Unity Transport (Latency/Packet Loss) | Claude → สถานะสูงสุด `Tested` |
| ออนไลน์จริงหลายเครื่อง + Voice | ตาม Playtest Checklist | ผู้ใช้ + เพื่อน → `Verified` |
| Performance | Profiler จาก Development Build | Claude (เครื่องนี้) + ผู้ใช้ (เครื่องเพื่อน) |

## 17. Build Pipeline
- `Assets/_Game/Scripts/Editor/BuildScript.cs` → `BuildWindows()` เรียกได้จาก `-executeMethod`
- `Tools/build.ps1`: เรียก Unity Batch Mode (`-batchmode -quit -projectPath -executeMethod -logFile`) → ตรวจ Exit Code + อ่าน Log → Zip เป็น `SpiritMovers-v<version>-Windows.zip` ใน `Builds/` (อยู่ใน `.gitignore`)
- `Tools/test.ps1`: `-runTests` EditMode/PlayMode → ผลเป็น XML + สรุป
- เวอร์ชันเกม: `bundleVersion` ใน Player Settings + ใช้ตรวจเวอร์ชันตอนเข้าห้อง
- **[พิสูจน์ M2]** Unity CLI ทำงานโดยไม่ต้อง Login เพิ่ม (พบไฟล์ License แล้ว แต่ยังไม่ได้รันจริง)

## 18. แพ็กเกจ
| แพ็กเกจ | สถานะ | แผน |
|---|---|---|
| URP, Input System, Test Framework, AI Navigation, uGUI, Timeline | มีแล้ว (ทางการ) | ใช้ |
| Netcode for GameObjects 2.x, Multiplayer Services, Vivox, Localization, Multiplayer Play Mode | ยังไม่ติดตั้ง (ทางการ) | ติดตั้งใน M2 หลังตรวจเวอร์ชันที่รองรับ 6000.3 |
| `com.unity.ai.assistant` (pre-release), `com.unity.ai.inference`, `com.unity.pipeline` (experimental), `com.unity.collab-proxy`, `com.unity.visualscripting` | มีจาก Template | ตรวจค่าใช้จ่าย/เครดิตใน M2 แล้ว **เสนอผู้ใช้** เก็บ/เอาออก (MASTER_PROMPT 0.2) |
| แพ็กเกจที่ไม่ใช่ของ Unity ทางการ | — | ต้องขออนุญาตก่อน (ประเภท A) |

## 19. ความปลอดภัยและความเป็นส่วนตัว
- Repo เป็น **Public** → ไม่มี Secret/Token ใน Repo · Unity Services ของ Client ใช้ Project ID (ไม่ใช่ความลับ) แต่ `organizationId` ใน ProjectSettings เป็นข้อมูลบัญชี → ถามผู้ใช้ใน M2 ก่อน Commit แรก
- ไม่ส่งข้อมูลส่วนตัวไปที่ใด นอกจากที่บริการ Multiplayer/Voice ต้องใช้ (ระบุใน README)

## 20. คำถามเทคนิคที่ต้องปิดใน M2 (→ G6)
1. NGO/Multiplayer Services/Vivox/Localization/Multiplayer Play Mode เวอร์ชันใดใช้ได้กับ 6000.3.27f1 (Released หรือ pre-release)
2. Relay เชื่อมต่อผ่านอินเทอร์เน็ตจริงโดยไม่เปิดพอร์ต (ต้องให้ผู้ใช้ทดสอบกับอีกเครื่อง/อีกเครือข่าย)
3. การยกของแบบ (A) vs (B) ภายใต้ Latency จำลอง
4. Bandwidth ต่อรอบเทียบ Free Tier
5. ภาษาไทย: สระ/วรรณยุกต์, การตัดบรรทัด, IME, ฟอนต์
6. Vivox Positional + Group + Audio Tap + AudioEnergy ทำงานร่วมกับ NGO Session
7. Unity CLI Build/Test ทำงานอัตโนมัติ
