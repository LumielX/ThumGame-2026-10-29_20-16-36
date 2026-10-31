# Changelog

รูปแบบ: [Keep a Changelog](https://keepachangelog.com/) · เวอร์ชันแบบ major.minor.patch

## [Unreleased]
### Added
- เอกสารออกแบบ: GDD, Art Bible, Technical Design, Project Plan, Backlog
- โครงโปรเจกต์ `Assets/_Game/` พร้อม Assembly Definitions (Core, Runtime, Editor, EditMode Tests)
- `GameVersion` สำหรับตรวจเวอร์ชันตอนเข้าห้อง + EditMode Tests
- Boot scene และ `BuildScript` สำหรับ Build Windows 64-bit
- สคริปต์ `Tools/build.ps1`, `Tools/test.ps1`
- ชั้น Network `UgsSessionService` (Sessions + Relay + ตรวจเวอร์ชัน) และต้นแบบ Network/Voice (ทิ้งได้)
- ภาษาไทย: `ThaiShaper` (จัดวรรณยุกต์ด้วย Glyph สำรอง), `ThaiFontSupport`, ช่องพิมพ์ไทยแบบ Shape ระหว่างพิมพ์
- Vivox 16.12.1: เสียงตามระยะ, วิทยุ (Audio Tap + ฟิลเตอร์), แชตข้อความ · เครื่องมือดึงค่า Vivox
- Localization 1.5.13: Locale ไทย/อังกฤษ, ตาราง `UI`, `LocalizedThaiText`
- กติกาเกมใน Core (`Core/Job/`): ตรวจของวางถูกและประเมินห้อง, ความโกรธผี 5 ขั้น, สุ่มงานจาก Seed, ค่าจ้างและดาวรีวิว, สถานะงาน
- `Tools/test.ps1 -Filter` รันเฉพาะชุด Test
- ห้องทดสอบ M3 (`M3_TestRoom`): ตัวละครบุคคลที่หนึ่ง เดิน/วิ่ง/ย่อ/กระโดด · ยก/วาง/หมุน/โยนของผ่านเครือข่าย · ของหนักช่วยกันยก · PlayMode Tests + สคริปต์ทดสอบ 2 เครื่องผ่าน Relay
- บ้านทดสอบ M3 (`M3_TestHouse`, Greybox 3 ห้อง): เริ่มงานจาก Seed บน Host · ผีประเมินและรับห้อง · สีห้องกลับมาเมื่อผีรับ · กริ่งเรียกผีมาพยักหน้า · ความโกรธผี · จบงาน 3 แบบ (ส่งผีสำเร็จ/ขึ้นรถกลับก่อน/ถูกไล่ออก) · ค่าจ้างและดาวรีวิว · ใบสั่งงาน (กด Tab) และหน้าสรุปงานภาษาไทย
- `Tools/m3c-job-test.ps1`: Host + Client หลายเครื่องผ่าน Relay เล่นจบงาน ตรวจว่าทุกเครื่องได้ผลตรงกัน

### Fixed
- Build ไม่ใช้ `-nographics` อีก (Material ที่สร้างด้วยสคริปต์เป็นสีม่วงแดงใน Build)
- ของเป็นสีม่วงแดงใน Build เพราะลบแล้วสร้าง Material ใหม่ใน Session เดียวกับที่ Build — ตอนนี้สร้างครั้งเดียวแล้วเปลี่ยนแค่สี
- ฉากใน Build เสีย (level0) เมื่อ MonoBehaviour 2 คลาสอยู่ไฟล์เดียว — แยกเป็นไฟล์ละคลาส · Build ลงโฟลเดอร์ว่างทุกครั้ง
- ค่าจ้างกรณีรับ 1 ใน 3 ห้องได้ 99 แทน 100 (หารก่อนคูณ)
- การยกของ: Host ตรวจระยะ/ทิศ/สิ่งกีดขวางเอง · ตรวจของหลุดมือแยกต่อคนถือ · คนถือออกจากเกมแล้วของถูกปล่อย
- หันไปหาของแล้วกดยกทันทีไม่ติด (Host ใช้ทิศที่หน่วงอยู่) — Client ส่งทิศที่หันจริงมากับคำขอยก
- ปิดงานที่รถ: ต้องอยู่ที่รถครบทุกคน และกดไม่ได้ตอนผีอำลา (เคยทำให้เสียโบนัสดาว)
- เริ่มงานซ้ำ/หลังจบงานถูกเพิกเฉย · ของที่มีคนถืออยู่ถูกปล่อยก่อนจัดบ้าน
- Client ที่สร้างงานไม่ตรงกับ Host (บ้านหรือกติกาต่างกัน) ถูกตัดออกพร้อม Log แทนการเล่นต่อแบบเพี้ยน

### Removed
- แพ็กเกจที่ไม่ใช้จาก Template: AI Assistant, Inference Engine, pipeline (experimental), Collab Proxy, Visual Scripting
- ไฟล์ Template: TutorialInfo, Readme, SampleScene
