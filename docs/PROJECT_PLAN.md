# Spirit Movers — Project Plan

> เวอร์ชัน 0.1 (ร่างสำหรับ G5) · 2026-10-30 · โครง Milestone ตาม MASTER_PROMPT 12 · ขอบเขต v0.1 ตาม D-025–D-027
> ไม่มี Deadline (MASTER_PROMPT 0) — แผนนี้กำหนด **ลำดับและเกณฑ์ผ่าน** ไม่ใช่วันที่ · ห้ามเริ่ม Milestone เสี่ยงสูงถ้า Milestone ก่อนหน้ายังไม่ผ่านเกณฑ์สำคัญ
> สถานะงานใช้: Planned / In Progress / Implemented / Tested / Verified / Blocked (MASTER_PROMPT 14)

## ภาพรวม
| Milestone | เป้าหมายหลัก | Gate | สถานะ |
|---|---|---|---|
| M0 Discovery | แนวเกม ชื่อ ระบบ ส่วนประกอบ ขอบเขต | G1–G4 | ✅ เสร็จ (2026-10-30) |
| M1 Design | GDD, Art Bible, Technical Design, แผนนี้ | G5 | In Progress (รออนุมัติ) |
| M2 Technical Foundation | จัดโปรเจกต์ + พิสูจน์ Network/Voice/ภาษาไทย | G6 | Planned |
| M3 Playable Prototype | ห้องเดียวเล่นได้ออนไลน์ครบ Loop | — | Planned |
| M4 Multiplayer Foundation | Lobby/Session/หลุด/Host ออก + Playtest ออนไลน์จริงครั้งแรก | — | Planned |
| M5 Vertical Slice | บ้าน 1 หลังที่ภาพ เสียง UI ใกล้เป้าหมาย | G7 | Planned |
| M6 Content Production | เนื้อหาครบขอบเขต v0.1 | — | Planned |
| M7 Alpha | ระบบครบ เล่นต้นจนจบ | — | Planned |
| M8 Beta & Polish | ความเสถียร สมดุล ขัดเกลา | — | Planned |
| M9 Release Preparation | Build, License, เอกสาร, Release | G8 | Planned |

---

## M1 — Design
- **เป้าหมาย:** รวมทุกการตัดสินใจเป็นเอกสารที่ใช้สร้างเกมได้
- **ขอบเขต:** `GDD.md`, `ART_BIBLE.md`, `TECHNICAL_DESIGN.md`, `PROJECT_PLAN.md`
- **ไม่รวม:** โค้ด, Asset ในโปรเจกต์, Commit
- **เกณฑ์ตรวจรับ:** ผู้ใช้อนุมัติที่ G5
- **ผลลัพธ์:** เอกสาร 4 ฉบับใน `docs/`

## M2 — Technical Foundation (→ G6)
- **เป้าหมาย:** พิสูจน์ความเสี่ยงทางเทคนิคสูงสุดด้วยของจริงก่อนสร้างเกม (MASTER_PROMPT 9, 1.3)
- **ขอบเขต:**
  1. **จัดเรื่องค้าง (MASTER_PROMPT 0.2):** สรุปการเปลี่ยนแปลงที่ยังไม่ Commit ให้ผู้ใช้ → ถามเรื่อง `organizationId`/`cloudProjectId` ใน Repo Public → **Commit แรก** (หลังผู้ใช้อนุมัติ) · ตรวจแพ็กเกจ AI/experimental/collab-proxy (ค่าใช้จ่าย/เครดิต) แล้วเสนอเก็บ/เอาออก · ถามเรื่องชื่อ/Visibility ของ Repo · ลบไฟล์ Template (แจ้งก่อน) · ตรวจ `.gitattributes` (eol ของ md/json) · สร้างโครงโฟลเดอร์ `_Game`, Assembly Definitions, `THIRD_PARTY_ASSETS.md`, `CHANGELOG.md`, `docs/TESTING.md`, `docs/ASSET_TRACKER.md`
  2. **Unity CLI:** Build/Test ผ่าน Batch Mode ได้ · `Tools/build.ps1`, `Tools/test.ps1` · อ่าน Log ทุกครั้ง
  3. **ต้นแบบ Network:** ติดตั้ง NGO 2.x + Multiplayer Services · Host สร้าง Session ได้ Join Code · Client เข้า · ซิงก์ผู้เล่น + ของ 50–100 ชิ้น · ทดลองการยกของแบบ (A)/(B) ภายใต้ Latency 50/100/200 ms · วัด Bandwidth
  4. **ต้นแบบ Voice:** Vivox (ผู้ใช้เปิดบริการใน Unity Dashboard เอง) · Lobby Group + Positional + ช่องวิทยุ + Audio Tap + อ่าน AudioEnergy
  5. **ต้นแบบภาษาไทย:** ฟอนต์ไทย OFL/CC0 · ประโยคทดสอบสระ/วรรณยุกต์ · ตัดบรรทัด (ZWSP + พจนานุกรม) · พิมพ์ไทย IME · Localization TH/EN สลับได้
  6. Shader ร่วมขั้นต้น (Cel + Restoration) — ถ้าเวลาเหลือ (ไม่บังคับใน G6)
- **ไม่รวม:** Gameplay จริง, ผี, งานศิลป์จริง, UI จริง
- **เกณฑ์ตรวจรับ (G6):**
  - Build Windows จากสคริปต์ได้ + Test รันจาก CLI ได้ (`Tested`)
  - 2+ Instance ในเครื่องเดียวเชื่อมผ่าน Relay และซิงก์ของได้ (`Tested`) · **ผู้ใช้ทดสอบกับอีกเครื่องผ่านอินเทอร์เน็ตจริงอย่างน้อย 1 ครั้ง** (`Verified`)
  - Voice ได้ยินกันผ่านอินเทอร์เน็ตจริง (ผู้ใช้ยืนยัน) + Positional ทำงาน
  - ภาษาไทยแสดงผลถูกต้องในภาพหน้าจอ (มีหลักฐานใน `docs/screenshots/`) + พิมพ์ไทยได้
  - รายงานผลวัด: Bandwidth, Latency ที่รู้สึกได้, ทางเลือกการยกของที่เลือก
- **วิธีทดสอบ:** CLI Tests · Multiplayer Play Mode/หลาย Instance · Network Simulator · Playtest Checklist M2 สำหรับผู้ใช้
- **ความเสี่ยง:** แพ็กเกจเป็น pre-release · Relay/Voice ต้องตั้งค่าใน Dashboard (ผู้ใช้ทำ) · ภาษาไทยใน TMP ไม่ผ่าน → เสนอทางเลือกที่ G6
- **ผลลัพธ์:** โปรเจกต์จัดโครงแล้ว · Commit แรก · สคริปต์ Build/Test · ฉากต้นแบบ 3 ชุด · รายงาน G6

## M3 — Playable Prototype (ออนไลน์ตั้งแต่แรก)
- **เป้าหมาย:** ห้องทดสอบ 1–2 ห้อง เล่นครบ Loop ผ่านเครือข่าย
- **ขอบเขต:** ผู้เล่นเดิน/วิ่ง/ย่อ/ยก/วาง/หมุน · Interaction · Puzzle Framework (Memory Slot, JobGenerator, RoomEvaluator) · กล้องวิญญาณ (Shader ขั้นต้น) · กริ่งผีพยักหน้า · ผีรับห้อง (Restoration) · AngerModel + ผีขี้อาย 1 ตัว (Placeholder) · ตู้ความทรงจำ · จบงาน 3 แบบ · ค่าจ้างพื้นฐาน · ใบสั่งงานขั้นต้น
- **ไม่รวม:** Lobby เต็ม, Voice ในเกม, ภาพจริง, เสียงจริง, Progression, Localization ครบ (ใช้คีย์จริงแต่ข้อความชั่วคราวได้)
- **เกณฑ์ตรวจรับ:** 2–4 Instance เล่นจบงานได้ · EditMode Test ของ Core ผ่าน · ไม่มี Desync ของสถานะงานในการทดสอบ 10 รอบ
- **วิธีทดสอบ:** EditMode/PlayMode · Multi-instance + Latency จำลอง
- **ความเสี่ยง:** ความสนุกของ Core Loop ยังไม่พิสูจน์ → ผู้ใช้ลองเล่น Prototype และให้ความเห็น (Playtest ภายใน)
- **ผลลัพธ์:** Build Prototype + บันทึกความเห็นการเล่น

## M4 — Multiplayer Foundation
- **เป้าหมาย:** ระบบออนไลน์ครบตาม D-008 + Playtest ออนไลน์จริงครั้งแรก
- **ขอบเขต:** Main Menu ขั้นต้น · Lobby ในรถตู้ (Ready, Host เลือกงาน, รหัสห้อง) · ตรวจเวอร์ชัน · ผู้เล่นหลุด/Host ออก/บันทึกค่าจ้าง · Voice ในเกม (Proximity + วิทยุ + Lobby + ผีหูไวฟังเสียง + ปิดได้) · Ping · แชตข้อความ · Solo path · Save/Settings พื้นฐาน · Playtest Checklist + แบบฟอร์มรายงาน + ปุ่มรายงานบั๊ก
- **ไม่รวม:** ภาพ/เสียงจริง, เนื้อหาบ้านจริง
- **เกณฑ์ตรวจรับ:** ผู้ใช้กับเพื่อน 2–4 คนเล่นผ่านอินเทอร์เน็ตจากหลายเครื่องได้ครบ Checklist (`Verified`) · กรณีหลุด/Host ออก/เวอร์ชันไม่ตรงทำงาน
- **ความเสี่ยง:** ปัญหา NAT/Firewall เฉพาะเครื่อง → คู่มือแก้ปัญหา
- **ผลลัพธ์:** Build สำหรับเพื่อน (ยังไม่ Release สาธารณะ — แชร์ไฟล์ตามที่ผู้ใช้กำหนด) + รายงาน Playtest

## M5 — Vertical Slice (→ G7)
- **เป้าหมาย:** บ้านไม้ใต้ถุนสูง + คุณยายขี้อาย ที่ภาพ เสียง UI และ Gameplay ใกล้เป้าหมาย
- **ขอบเขต:** Art Pipeline ตาม Art Bible (Asset CC0 + ของย้อนยุคปั้นเอง) · ตัวละครผู้เล่น CH3 + แอนิเมชัน · ผีคุณยาย (สงบ/โกรธ) · แสง/หมอก/Outline · เสียงบรรยากาศ + วิทยุ + เสียงฮัม · UI ธีมกระดาษ (HUD, ใบสั่งงาน, Results พร้อมรูปเหตุการณ์และรีวิวผี) · Localization TH/EN ในส่วนที่มี · Accessibility หลัก
- **ไม่รวม:** บ้าน/ผีที่สอง, ร้านแต่งตัวเต็ม, Tutorial เต็ม
- **เกณฑ์ตรวจรับ (G7):** Build เล่นได้ · ภาพหน้าจอผ่าน Art Bible · ผู้ใช้ยืนยันเสียง · Playtest Checklist · ผล Profiler เบื้องต้น
- **ผลลัพธ์:** Build Vertical Slice + Playtest Checklist + ภาพหน้าจอ

## M6 — Content Production
- **ขอบเขต:** ทาวน์เฮาส์เก่า (มีบันได) · ลุงหูไว · ความทรงจำ 4 คู่บ้าน×ผี · ระดับงานยาก · งานฝึกงาน + ป้าแจ่มจันทร์ · ร้านแต่งตัว 12 ชิ้น + Emote 4 · ยศ 3 ขั้น · อัลบั้มผี 3 หน้า · รางวัลพนักงาน · ของหลอก (ถ้าทัน) · มุมฝึกใน Lobby (ถ้าทัน)
- **ไม่รวม:** รายการใน `BACKLOG.md`
- **เกณฑ์ตรวจรับ:** เนื้อหาครบขอบเขต v0.1 · ทุก Asset บันทึก License และ Asset Tracker
- **ผลลัพธ์:** เนื้อหาครบใน Build

## M7 — Alpha
- **เป้าหมาย:** ระบบครบ เล่นได้ตั้งแต่ต้นจนจบ (เมนู → สอนเล่น → งาน → ความก้าวหน้า)
- **เกณฑ์ตรวจรับ:** ผ่านทุกข้อ Gameplay/Multiplayer ใน GDD 24 แบบ `Tested` · Playtest ออนไลน์ 4 คน (`Verified`) · Known Issues บันทึกครบ
- **ผลลัพธ์:** Build Alpha + รายการบั๊ก

## M8 — Beta & Polish
- **ขอบเขต:** แก้บั๊ก · สมดุล (ค่า ⚙ ทั้งหมด) · ภาพ/เสียง/UI ขัดเกลา · Performance (Profiler บน Build) · Localization ตรวจทุกหน้าจอ · Placeholder = 0 ใน Asset Tracker · ทดลอง 5–6 คน
- **เกณฑ์ตรวจรับ:** ไม่มีบั๊กร้ายแรงค้าง · สเปกขั้นต่ำได้จากการวัด
- **ผลลัพธ์:** Release Candidate

## M9 — Release Preparation (→ G8)
- **ขอบเขต:** Build ผ่านสคริปต์ · ตรวจ License ครบ (`THIRD_PARTY_ASSETS.md`) · ตรวจไม่มี Secret ใน Repo · README (ดาวน์โหลด ติดตั้ง SmartScreen/Firewall สร้างห้อง แก้ปัญหา + แจ้งเรื่อง Voice ผ่านเซิร์ฟเวอร์) · CHANGELOG · Release Notes (ฟีเจอร์ สเปกขั้นต่ำจากการวัด Known Issues วิธีรายงานบั๊ก)
- **เกณฑ์ตรวจรับ (G8):** ผู้ใช้อนุมัติรายละเอียด Repository, Visibility และไฟล์ทั้งหมดที่จะอัปโหลด **ก่อน** เผยแพร่ · เพื่อนที่ไม่รู้เรื่องพัฒนาเกมทำตาม README จนเล่นได้ (`Verified`)
- **ผลลัพธ์:** GitHub Release `SpiritMovers-v0.1.0-Windows.zip` (เมื่อผู้ใช้อนุมัติเท่านั้น)

---

## ความเสี่ยงระดับโครงการ
| ความเสี่ยง | Milestone ที่จัดการ | แผน |
|---|---|---|
| Network/ฟิสิกส์ซิงก์ไม่ดี | M2 | พิสูจน์ก่อน · ถ้าไม่ผ่าน → ลดฟิสิกส์ (ของหนักแบบ Kinematic) และแจ้งผู้ใช้ |
| ภาษาไทยไม่ผ่าน | M2 | ทางเลือกฟอนต์/ตัวเรนเดอร์ → G6 |
| ความสนุกของ Core Loop | M3 | Playtest ภายในเร็วที่สุด |
| งานศิลป์บานปลาย | M5–M6 | Art Bible + Pipeline ทำซ้ำได้ · ขอบเขต V-M |
| นัดเพื่อนทดสอบยาก | M4, M7, M9 | Checklist สั้น ชัด · รับรอง 4 คน (D-026) |
| เปลี่ยนนโยบายบริการ | ทุก Milestone | แยกเลเยอร์ · ทางหนี EOS |
