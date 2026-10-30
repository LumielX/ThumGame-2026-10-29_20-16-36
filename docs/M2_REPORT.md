# M2 — Technical Foundation: รายงานผล (ฉบับกลางทาง สำหรับ G6)

> อัปเดต: 2026-10-30 · สถานะตาม MASTER_PROMPT 14 · หลักฐาน: Log ใน `Logs/` (ไม่ขึ้น Git), ภาพใน `docs/screenshots/m2/`
> เกณฑ์ G6 อยู่ใน `docs/PROJECT_PLAN.md` M2

## สรุปสั้น
| หัวข้อ | สถานะ | ผล |
|---|---|---|
| Unity CLI (Build/Test) | **Tested** | Batch Mode เปิดโปรเจกต์ได้โดยไม่ต้อง Login · `Tools/test.ps1` EditMode 12/12 ผ่าน · `Tools/build.ps1` Build Windows สำเร็จ (~1.5 นาที, ~100 MB) · Player เปิดแล้ว Log `[Boot] Spirit Movers v0.0.1` |
| จัดโปรเจกต์/Repo | **Implemented** (Commit แล้ว 2 ชุด, ยังไม่ Push) | โครง `_Game` + asmdef · ลบ Template · ลบแพ็กเกจที่ไม่ใช้ (D-031) · Commit identity noreply (D-030) |
| แพ็กเกจ Network/Voice | **Implemented** | NGO **2.13.3** (รุ่น 3.x ต้องใช้ Unity 6000.7) · Multiplayer Services **2.4.1** · Multiplayer Play Mode **2.0.2** · Multiplayer Tools **2.2.12** · Vivox **16.12.1** (ยังไม่ติดตั้ง) — ตรวจจาก Registry ทางการ 2026-10-30 |
| ต้นแบบ Network ผ่าน Relay | **Tested** (2 Instance ในเครื่องนี้ ผ่านเซิร์ฟเวอร์ Relay จริง) | สร้าง/เข้าห้องด้วย Join Code ได้ · ซิงก์ผู้เล่น + ของฟิสิกส์ 40 ชิ้นตรงกัน · ไม่ต้องตั้งค่า Dashboard เพิ่ม · **ยังไม่ Verified** (ต้องทดสอบจากอีกเครื่อง/อีกเครือข่าย) |
| เลือกวิธียกของ | **Tested** → ตัดสินแล้ว (D-034) | ดูตารางด้านล่าง |
| Bandwidth | **Tested** | ~1–3 KB/s ต่อผู้เล่น (40 ชิ้น + 2 ผู้เล่น) |
| ภาษาไทย | **Tested** (แสดงผล) · พิมพ์ผ่าน IME ยังไม่ทดสอบ | พบปัญหาวรรณยุกต์ใน TMP → ผู้ใช้เลือกทำตัวจัดรูปเอง (D-035) → **แก้แล้ว** ด้วย `ThaiShaper` + Glyph สำรองของฟอนต์ · ตัดบรรทัดด้วย ZWSP ใช้ได้ |
| Voice (Vivox) | **Planned** | ต้องให้ผู้ใช้เปิดบริการ Vivox ใน Unity Dashboard ก่อน |

---

## 1. Network ผ่าน Relay
- ต้นแบบ: `Assets/_Game/Prototypes/NetSync/` (ทิ้งได้) + ชั้นถาวร `Assets/_Game/Scripts/Net/` (`UgsSessionService` — Sessions + ตรวจเวอร์ชัน, `TransportTrafficMeter`)
- Build ต้นแบบ: เมนู Editor `Spirit Movers/Prototypes/NetSync: Create Scene and Build` → `Builds/Proto-NetSync-v0.0.1/`
- ทดสอบอัตโนมัติ: `Tools/proto-netsync-matrix.ps1` (Host + Client แบบ Headless, ใช้ Profile Authentication ต่างกัน)
- เวลาสร้างห้องครั้งแรก ~15 วินาที (QoS + Relay Allocation) · เข้าห้อง ~5 วินาที

### ผลเปรียบเทียบวิธียกของ (กล่องเคลื่อนที่ ~2.4 ม./วินาที, Latency จำลองด้วย Network Simulator)
"ความคลาดเคลื่อน" = ระยะเฉลี่ยระหว่างของที่ถืออยู่กับตำแหน่งมือที่ควรเป็น

| วิธี | Latency | คนถือเห็นของห่างมือ | คนอื่น (Host) เห็นของห่าง | รับข้อมูล (KB/s) Host / Client |
|---|---|---|---|---|
| A — Host จำลอง | 0 ms | 1.30 ม. | 1.00 ม. | 1.1 / 2.9 |
| A | 100 ms | 2.29 ม. | 2.02 ม. | 1.0 / 2.4 |
| A | 200 ms | 3.26 ม. | 3.02 ม. | 1.0 / 2.3 |
| **B — โอนสิทธิ์ให้คนถือ** | 0 ms | **0.22 ม.** | 1.05 ม. | 1.4 / 1.8 |
| B | 100 ms | **0.22 ม.** | 2.05 ม. | 1.4 / 2.0 |
| B | 200 ms | **0.22 ม.** | 3.06 ม. | 1.4 / 1.9 |

- **B ทำให้คนถือรู้สึกว่าของอยู่ในมือทันทีทุกระดับ Latency** ส่วนคนอื่นเห็นของตามหลังเท่ากันทั้งสองวิธี (เป็นธรรมชาติของเครือข่าย)
- ค่า ~1 ม. ที่ Latency 0 มาจาก Interpolation ของ NGO + การเดินทางผ่าน Relay — ปรับลดได้ใน M3
- **Bandwidth:** ~1–3 KB/s ต่อผู้เล่น → รอบละ 20 นาที ≈ 1.2–3.6 MB ต่อผู้เล่น — ต่ำกว่า Free Tier ของ Relay (150 GiB/เดือน) หลายพันเท่า (เป็นค่า Payload ไม่รวม Header ของ UDP/Relay)

### ปัญหาที่พบและแก้แล้ว / ต้องแก้ใน M3
- ✅ วิธี B: คนถือหลุด → ของหายไปด้วย → แก้ด้วย `NetworkObject.DontDestroyWithOwner = true`
- ⏳ เมื่อคนถือหลุด สถานะ "กำลังถือ" ไม่ถูกล้าง → M3 ต้องล้างใน `OnClientDisconnect` บน Host

## 2. ภาษาไทย
หลักฐาน: `docs/screenshots/m2/thai-text-test.png`, `thai-font-comparison.png`, `thai-uitk-generators.png`

| ทดสอบ | ผล |
|---|---|
| TextMeshPro + ฟอนต์ Mali (OFL) | สระ/วรรณยุกต์ส่วนใหญ่ถูก (ผู้ใหญ่, ปั้น, น้ำ, ไม่, ค่า, ฟุ้ง) **แต่ไม้เอกที่ตามสระบน/ล่างหรือพยัญชนะหางสูงหายไป** (กี่ → กี, ญี่ปุ่น → ญีปุน, ปู่, ฝั่ง, ปิ่น, ที่) |
| สาเหตุ (ยืนยันจาก Layout ของ TMP) | ไม้เอกถูกวางที่ตำแหน่งปกติ → ทับสระอี/หางของ ป จนมองไม่เห็น · ฟอนต์ไทยใช้การแทนที่ Glyph ตามบริบท (GSUB) เพื่อยก/เลื่อนวรรณยุกต์ ซึ่ง TMP ไม่รองรับ · เป็นเหมือนกันทั้ง Mali, Kanit, Sarabun และแม้ปิด Font Features → **ไม่ใช่ปัญหาของฟอนต์** |
| ตัดบรรทัด | ไม่มีตัวแบ่ง → ตัดกลางคำ ("ออกจ\|าก") · **แทรก ZWSP ระหว่างคำ → ตัดตรงคำถูกต้อง** |
| UI Toolkit + Advanced Text Generator (HarfBuzz/ICU) | ตั้งค่าแล้วแต่ใน Player ยังแสดงผลเหมือน Standard (ทั้งวรรณยุกต์และตัดบรรทัด) — ยังไม่พบวิธีเปิดใช้ที่ได้ผล |
| พิมพ์ไทยผ่าน IME | ยังไม่ได้ทดสอบ (ต้องเปิดเกมแบบมีหน้าจอ — ให้ผู้ใช้ทดสอบ) |

### ทางเลือกที่เสนอ (ผู้ใช้เลือกข้อ 1 — D-035)
1. **ตัวจัดรูปอักษรไทยของเราเอง + TMP (แนะนำ):** ก่อนแสดงผล แปลงลำดับสระ/วรรณยุกต์ที่ต้องเลื่อนเป็นรหัส Private Use Area (PUA) แล้วผูกรหัสเหล่านั้นกับ Glyph ทางเลือกที่มีอยู่แล้วในฟอนต์ (ตรวจแล้ว: Mali มี `uni0E48–0E4C.small`, `.narrow`, `uni0E31/0E34/0E35.narrow` ฯลฯ) (เทคนิคเดียวกับระบบ Thai PUA ของ Windows/Mac รุ่นเก่า) + แทรก ZWSP สำหรับตัดคำ — กฎภาษาไทยมีจำนวนจำกัดและรู้แน่นอน · ทดสอบอัตโนมัติได้ · ไม่ต้องติดตั้งของภายนอก
2. **สืบ UI Toolkit Advanced Text ต่อ:** อาจแก้ทั้งวรรณยุกต์และตัดคำโดยไม่ต้องเขียนเอง แต่ยังไม่พบว่าทำงานใน Player ได้ — ใช้เวลาไม่แน่นอน และต้องเปลี่ยน UI ไปใช้ UI Toolkit
3. **แพ็กเกจภายนอก** (เช่น UGUI ที่ใช้ Advanced Text Generator ของชุมชน) — ต้องขออนุญาต (ประเภท A) และความเสถียรไม่แน่นอน


### ผลการแก้ (D-035) — Tested 2026-10-30
- `Core/Text/ThaiShaper` แปลงลำดับที่ต้องเลื่อนเป็นรหัส PUA: ไม้เอก–การันต์หลังสระบน → รูปยกสูง (`.small`) · วรรณยุกต์บนพยัญชนะหางสูง (ป ฝ ฟ ฬ) → รูปเลื่อนซ้าย (`.narrow`) · สระบนบนพยัญชนะหางสูง → `.narrow` · สระล่างใต้ ฎ ฏ → `.small`
- `Core/Text/TrueTypeGlyphNames` อ่านชื่อ Glyph จากตาราง `post` ของฟอนต์ · เมนู `Spirit Movers/Text/Build Thai Glyph Maps` สร้าง `ThaiGlyphMap` ให้ฟอนต์ (Mali Regular/Bold พบครบ 20/20)
- `Runtime/Text/ThaiFontSupport.Register(font, map)` ผูกรหัส PUA กับ Glyph สำรองใน TMP Font Asset (ใช้ API internal ของ TMP ผ่าน Reflection จุดเดียว — ถ้า TMP เปลี่ยนจะ Log Error ชัดเจน) · `SetThaiText()` สำหรับตั้งข้อความ
- EditMode Tests 26/26 ผ่าน · หลักฐาน: `docs/screenshots/m2/thai-shaped-before-after.png`, `thai-shaped-closeup.png`
- ข้อจำกัดที่รู้: รูปยกสูงของไม้ตรี/จัตวาอยู่สูงตามแบบฟอนต์ → ต้องเผื่อระยะบรรทัด · ญ ฐ + สระล่าง ยังไม่ตัดเชิง (ฟอนต์ไม่มีรูปสำรอง) · ช่องพิมพ์ (TMP Input Field) ต้องแสดงผลแบบ Shape เพิ่มเติมใน M4 · ยังไม่ได้ทดสอบใน Player แบบ IL2CPP (ตอนนี้ใช้ Mono)

## 3. สิ่งที่ผู้ใช้ต้องทำเพื่อปิด G6
1. **ทดสอบออนไลน์จริง:** เปิด `Builds/Proto-NetSync-v0.0.1/SpiritMovers.exe` บนเครื่องนี้ (กด Host) และให้เพื่อนเปิดไฟล์เดียวกันบนอีกเครื่อง/อีกเครือข่าย (ใส่รหัสห้อง → Join) → เดินและยกกล่อง (E) ลองสลับวิธี (M) และ Latency → ส่ง Log กลับ (ตำแหน่งใน `docs/TESTING.md`)
2. **เปิดบริการ Vivox** ใน Unity Dashboard (Development → Products → Vivox Voice and Text Chat → Onboarding) สำหรับโปรเจกต์ที่ผูกไว้ — ฟรีถึง 5,000 PCU ไม่ต้องผูกบัตร (D-009) · ผมจะติดตั้งแพ็กเกจและทำต้นแบบ Voice หลังจากนั้น
3. **ทดสอบพิมพ์ภาษาไทยในเกม (IME)** — จะใส่ช่องพิมพ์ในต้นแบบให้ลองตอนทำ Voice/Lobby
