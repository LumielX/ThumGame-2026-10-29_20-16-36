# STATUS — สถานะโปรเจกต์

อัปเดตล่าสุด: 2026-10-30

## ตอนนี้
- **Milestone:** M0 ✅ · M1 ✅ (G5, D-029) · **M2 — Technical Foundation: กำลังทำ — รายงานกลางทาง `docs/M2_REPORT.md`** (2026-10-30)
- **ชื่อเกม:** **Spirit Movers** (D-002) · แนวเกม: จัดบ้านคืนผี (D-001)
- **ขอบเขต v0.1:** V-M (D-025) · รับรอง 1–4 คน + 5–6 คนทดลอง (D-026) · อนุมัติ G4 (D-027) → `docs/discovery/05-scope-v0.1.md` · งานนอกขอบเขต → `docs/BACKLOG.md`
- **Phase:** Phase 3 ✅ (สรุปต้น `docs/discovery/03-systems.md`) · Phase 4 ✅ (สรุปต้น `docs/discovery/04-components.md`) · Phase 5 ✅ · Phase 4 ที่ผ่าน: 4.1 มุมกล้อง = P3 (D-014) · 4.2 การเคลื่อนไหว = M1 (D-015) · 4.3 Art Direction = ผสม AD1+AD2 "บ้านซีด → ผีพอใจ → สีกลับมา" แบบหม่นมืดขึ้น (D-016 สิทธิ์ Render, D-017) · 4.4 ตัวละคร = CH3 ร่าง CC0 + หัว/ชุดเอง (D-018), ผี = GH2 อดีตเจ้าของบ้าน (D-019) · 4.5 ฉาก = L3 ไทย/เอเชียยุค 80–90 เมืองสมมติ (D-020) · 4.6 ไอเทม = IT1 มือ 2 ข้าง + เข็มขัด + กล่องทีม (D-021) · 4.7 เสียง = S1 เงียบ + ดนตรีจากของในบ้าน (D-022) · 4.8 UI = U1 HUD น้อย + ใบสั่งงาน + Wireframe (D-023) · 4.9 Tutorial = T1 งานฝึกงาน + ผีครูฝึก (D-024)
- **Gate ล่าสุดที่ผ่าน:** **G4 ขอบเขต v0.1** (2026-10-30, D-027) · ก่อนหน้า: G3 ครบทุกหัวข้อ Phase 3–4 — Phase 3: A Core Gameplay = A1 งานเดี่ยว (D-003) · B Puzzle = B3 กล้องวิญญาณ (D-004) · C Ghost = C1 ผีเจ้าบ้านคือภัยเอง (D-005) · D Humor = D2 ฟิสิกส์ + ผีแกล้ง (D-006) · E Progression = E3 ยศ + ของแต่งตัว + อัลบั้มผี (D-007) · F Multiplayer = F1 Unity NGO + Multiplayer Services (D-008) · G Voice = Vivox (D-009) + ใกล้ได้ยิน/วิทยุ/ผีหูไวฟังเสียงพูด (D-010) · H Solo = H1 ปรับกลไกเฉพาะ Solo (D-011) · I Accessibility = I1 ชุดพื้นฐานครบ (D-012) · J ระบบเสริม = รูปเหตุการณ์ + รีวิวจากผี + แชตข้อความ (D-013)
- **กำลังรอ (ผู้ใช้):** (1) ตัดสินวิธีแก้ภาษาไทย (`docs/M2_REPORT.md` ข้อ 2) · (2) ทดสอบต้นแบบ Network กับเพื่อนผ่านอินเทอร์เน็ต · (3) เปิดบริการ Vivox ใน Unity Dashboard · **Gate ล่าสุด: G5 (D-029)** · ถัดไป G6
- **ไฟล์ช่วง 4.3:** ภาพ PNG 6 ไฟล์ใน `docs/screenshots/art-direction/` (ยังไม่ Commit) · Asset Kenney Furniture Kit 2.0 (CC0) + สคริปต์ Render อยู่นอก Repo ที่ `%TEMP%\spm\` (ชั่วคราว ไม่ได้นำเข้า Unity)
- **หัวข้อ Phase 4 (ถามเฉพาะที่ผู้ใช้ต้องตัดสิน, MASTER_PROMPT 5):** มุมกล้อง · การเคลื่อนไหว · Art Direction (3 แบบ + ภาพตัวอย่างถ้าทำได้, หัวข้อ 6.2) · ตัวละครและผี · ฉากและสถานที่ · ระบบไอเทม (รวมวิทยุสื่อสาร D-010, รถเข็น D-011, เครื่องมือป่วน D-006) · เสียงและดนตรี · UI/UX · Tutorial · ผลลัพธ์ → `docs/discovery/04-components.md`

## คำถามที่ค้างอยู่
- ไม่มี (เรื่อง Repo Public, ชื่อ Repo และ `organizationId` ใน ProjectSettings จะถามใน M2)

## ข้อสังเกตสำหรับ Phase 3
- พบเกมที่ธีมใกล้แนว A (Haunted Home Designer, Ghostly Memories, Haunted House Renovator — รายละเอียดใน `02-names.md`) ซึ่งเป็นเกมเล่นคนเดียวทั้งหมด จุดต่างของเราต้องอยู่ที่ Co-op + ฟิสิกส์ + ผีที่โกรธและไล่ล่า
- ชื่อ *Spirit Movers* สื่อถึง "ขนของออก" → ต้องออกแบบให้ชัดว่าเป็นบริษัทขนย้ายที่จัดบ้าน**คืน**ให้ผี ไม่ใช่ขนของไปขาย (D-002)

## ผลตรวจสภาพแวดล้อม (2026-10-29, อ่านอย่างเดียว)
| รายการ | ผล |
|---|---|
| Unity Editor | `X:\TheUnity\Editors\6000.3.27f1\Editor\Unity.exe` เวอร์ชัน 6000.3.27f1 (672d562a42a7) ตรงกับ `ProjectVersion.txt` · Unity Hub อยู่ `X:\TheUnity\Unity Hub\` · พบไฟล์ License `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml` (ยังไม่ได้ทดสอบ CLI — ทำใน M2) |
| Blender | `X:\TheBlender\blender.exe` เวอร์ชัน 5.2 (เป็นตัวโปรแกรมจริง) |
| เครื่อง | Windows 11 Home 10.0.26300 · i5-14450HX 10C/16T · RAM 15.6 GB · RTX 5050 Laptop (+ Intel UHD) · X: ว่าง 203 GB, C: ว่าง 73 GB |
| Git | Branch `main`, 1 Commit (`b887511 Initial check-in`) · LFS 3.8.1 ติดตั้งแล้ว · `core.autocrlf=true` |
| GitHub | `gh` 2.103.0 Login เป็น `LumielX` · Repo **PUBLIC** |
| `.gitignore` | ครอบคลุม `Library/ Temp/ Build/ Builds/ Logs/ UserSettings/`, `*.csproj`, `*.slnx` แล้ว |
| `.gitattributes` | `*.blend` → LFS แล้ว · Unity YAML ใช้ `eol=lf` · ไฟล์ข้อความทั่วไป (`*.md`, `*.json` นอก Unity) ยังไม่กำหนด eol — ตรวจ/ปรับใน M2 |

### การเปลี่ยนแปลงที่ยังไม่ Commit (สรุปไว้สำหรับ M2 — ยังไม่แตะ)
- ลบ `Assets/Editor/HubForceResolve.cs` (+ .meta, `Assets/Editor.meta`) — Script ชั่วคราวจาก Unity Hub
- `PC_RPAsset.asset`, `URPProjectSettings.asset`, `ShaderGraphSettings.asset` — Unity อัปเกรดรูปแบบ Settings อัตโนมัติ (เพิ่ม field ใหม่ของ URP 17.3)
- `EditorBuildSettings.asset` — เพิ่ม config ของ `com.unity.dt.app-ui` (มาจาก Unity AI Assistant)
- `ProjectSettings.asset` — เพิ่ม Define `APP_UI_EDITOR_ONLY`, ผูก `cloudProjectId`, `projectName: ThumGame`, `organizationId` (**ข้อมูลบัญชี Unity — Repo เป็น Public ต้องตัดสินใจก่อน Commit**)
- `Assets/TutorialInfo/Icons/URP.png` — ไฟล์ถูก Commit แบบไม่ผ่าน LFS แต่ตอนนี้ `.gitattributes` บังคับ LFS จึงแสดงว่าเปลี่ยน (เนื้อไฟล์เดิม — จริง ๆ เป็น JPEG ที่ใช้นามสกุล .png) · จะลบพร้อมไฟล์ Template ใน M2
- ไฟล์ใหม่ `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json` — ค่าตั้งของ AI Assistant (ไม่มีข้อมูลลับ)
- ไฟล์ใหม่ `docs/`, `CLAUDE.md` (จาก Session นี้)

### เรื่องที่ต้องพิสูจน์ใน M2 จากการตัดสินใจ Phase 3
- (D-008) เชื่อมต่อผ่านอินเทอร์เน็ตด้วย Relay โดยไม่เปิดพอร์ต · ซิงก์ฟิสิกส์การยกของ · วัด Bandwidth ต่อรอบ · ตรวจเวอร์ชัน NGO 2.x / Multiplayer Services ที่ใช้ได้ใน 6000.3.27f1 (Released หรือ pre-release)
- (D-004) Shader ภาพซ้อนของกล้องวิญญาณ (ช่วง Art/Prototype)
- (D-013) พิมพ์ภาษาไทยผ่าน IME ใน Unity UI (แชตข้อความ) — ทดสอบพร้อมการแสดงผลภาษาไทย
- (D-009/D-010) Vivox: เปิดบริการใน Unity Dashboard (ผู้ใช้ต้องทำเอง/อนุญาต) · Positional + Group channel · `AudioEnergy` ส่งให้ Host สำหรับผีหูไว · Audio Taps ใส่เสียงก้อง/เสียงวิทยุ

### เรื่องที่ต้องจัดการใน M2 (MASTER_PROMPT 0.2)
- ตรวจแพ็กเกจ `com.unity.ai.assistant 2.21.0-pre.2`, `com.unity.ai.inference 2.6.1`, `com.unity.pipeline 0.9.1-exp.1`, `com.unity.collab-proxy 2.13.6`, `com.unity.visualscripting` ว่าใช้เครดิต/เสียเงินหรือไม่ แล้วเสนอเก็บ/เอาออก (ต้องค้นเอกสารทางการ ณ วันนั้น)
- ชื่อ Repo มีวันเวลาต่อท้าย + Visibility Public → ถามผู้ใช้ก่อนเปลี่ยน
- ตรวจ Unity CLI (`-batchmode`) และไฟล์ Template ที่จะลบ

## งานถัดไป
1. **M2 ที่เหลือ:** ทำตามวิธีแก้ภาษาไทยที่ผู้ใช้เลือก · ติดตั้ง Vivox 16.12.1 + ต้นแบบ Voice (หลังผู้ใช้เปิดบริการ) · รับผลทดสอบออนไลน์จริง → อัปเดต `docs/M2_REPORT.md` → **G6**
   - เสร็จแล้วใน M2: Commit แรก (D-032) · ลบแพ็กเกจ (D-031) · โครง `_Game`/asmdef/Build/Test CLI · ต้นแบบ Network ผ่าน Relay (`Tested`) · เลือกวิธียกของ B (D-034) · ทดสอบภาษาไทย (พบปัญหา) · ยังไม่ Push (จัดการเรื่องค้างใน "เรื่องที่ต้องจัดการใน M2" ด้านบน + ต้นแบบ Network/Voice/ภาษาไทย → G6)
- หมายเหตุสิทธิ์: งานที่ไม่ใช่ Markdown ช่วง Discovery ต้องขออนุญาตก่อน (ตัวอย่าง D-016 สำหรับ Test Render)
- หมายเหตุวิธีทำงาน: ใช้แนวทาง grilling (เสนอคำตอบแนะนำทุกคำถาม) แต่ถามทีละประเด็นตาม MASTER_PROMPT 3.2 ไม่ยิงทั้งชุด
