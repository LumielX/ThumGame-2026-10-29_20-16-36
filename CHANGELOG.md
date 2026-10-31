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

### Fixed
- Build ไม่ใช้ `-nographics` อีก (Material ที่สร้างด้วยสคริปต์เป็นสีม่วงแดงใน Build)

### Removed
- แพ็กเกจที่ไม่ใช้จาก Template: AI Assistant, Inference Engine, pipeline (experimental), Collab Proxy, Visual Scripting
- ไฟล์ Template: TutorialInfo, Readme, SampleScene
