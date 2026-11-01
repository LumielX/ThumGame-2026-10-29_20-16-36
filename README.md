# Spirit Movers

เกม 3D Co-op Horror Comedy 1–6 คน — พนักงานบริษัทขนย้ายรับงาน **จัดบ้านคืนให้ผี** ก่อนผีจะไล่คุณออกจากบ้าน

> สถานะ: **กำลังพัฒนา (M4 — Multiplayer Foundation)** · ยังไม่มีเวอร์ชันให้ดาวน์โหลด
> แพลตฟอร์ม: Windows 64-bit · ภาษา: ไทย / English

## สำหรับผู้เล่น
วิธีดาวน์โหลด ติดตั้ง (รวมคำเตือน Windows SmartScreen และ Firewall) สร้างห้อง เชิญเพื่อน และแก้ปัญหา จะเพิ่มที่นี่เมื่อมี Release แรกใน GitHub Releases

### เสียงพูด แชต และความเป็นส่วนตัว
- **เสียงพูดและแชตข้อความส่งผ่านเซิร์ฟเวอร์ของ Unity (Vivox)** ไปถึงเพื่อนในห้อง — ตัวเกมไม่ได้บันทึกเสียงหรือข้อความไว้
- ในรถตู้ได้ยินทุกคน · ในบ้านได้ยินเฉพาะคนที่อยู่ใกล้ · คุยข้ามห้องด้วยวิทยุสื่อสาร (ถือวิทยุแล้วกดคลิกขวาค้าง)
- ตั้งค่า → เสียง: เลือกไมค์/ลำโพง · กดค้างเพื่อพูด (Push-to-Talk) · **"ให้ผีได้ยินเสียงไมค์ของฉัน"** ปิดได้ — ถ้าเปิด เกมส่งแค่ "ระดับความดัง" ของไมค์ไปที่เครื่องหัวหน้าห้อง (ไม่ใช่ตัวเสียง)
- ปิดเสียงหรือลดเสียงเพื่อนรายคน: กด Esc → ผู้เล่น
- ไม่ใช้ไมค์ก็เล่นได้: ปักหมุด (คลิกกลาง) · วงล้อข้อความด่วน (กด Q ค้าง หรือเลข 1–6) · แชต (กด T)
- **เพื่อนไม่ได้ยินเรา?** Windows → Settings → Privacy & security → Microphone → เปิด "Microphone access" และ "Let apps access your microphone"
- เล่นคนเดียวไม่ใช้เสียงและไม่ต้องใช้อินเทอร์เน็ต
- ไฟล์บันทึกเกมอยู่ที่ `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\` (`save_v1.json`, `settings.json`) — เก็บแค่เงินสะสม ชื่อที่ตั้งเอง และการตั้งค่า

## สำหรับผู้พัฒนา
- Unity **6000.3.27f1** (Unity 6.3 LTS) · URP · Input System
- เอกสารหลัก: [`docs/GDD.md`](docs/GDD.md) · [`docs/TECHNICAL_DESIGN.md`](docs/TECHNICAL_DESIGN.md) · [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) · สถานะล่าสุด [`docs/STATUS.md`](docs/STATUS.md)
- Build: `powershell -ExecutionPolicy Bypass -File Tools\build.ps1`
- Test: `powershell -ExecutionPolicy Bypass -File Tools\test.ps1`
- วิธีทดสอบทั้งหมด: [`docs/TESTING.md`](docs/TESTING.md)

## License ของ Asset
Asset ภายนอกทั้งหมดและใบอนุญาต: [`THIRD_PARTY_ASSETS.md`](THIRD_PARTY_ASSETS.md)
