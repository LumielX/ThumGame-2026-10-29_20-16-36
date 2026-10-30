# Changelog

รูปแบบ: [Keep a Changelog](https://keepachangelog.com/) · เวอร์ชันแบบ major.minor.patch

## [Unreleased]
### Added
- เอกสารออกแบบ: GDD, Art Bible, Technical Design, Project Plan, Backlog
- โครงโปรเจกต์ `Assets/_Game/` พร้อม Assembly Definitions (Core, Runtime, Editor, EditMode Tests)
- `GameVersion` สำหรับตรวจเวอร์ชันตอนเข้าห้อง + EditMode Tests
- Boot scene และ `BuildScript` สำหรับ Build Windows 64-bit
- สคริปต์ `Tools/build.ps1`, `Tools/test.ps1`

### Removed
- แพ็กเกจที่ไม่ใช้จาก Template: AI Assistant, Inference Engine, pipeline (experimental), Collab Proxy, Visual Scripting
- ไฟล์ Template: TutorialInfo, Readme, SampleScene
