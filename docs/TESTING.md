# TESTING — วิธีทดสอบ Spirit Movers

> อัปเดต: 2026-10-30 (M2) · สถานะงานตาม MASTER_PROMPT 14: ทดสอบในเครื่องเดียวได้สูงสุด `Tested` · `Verified` ต้องมาจากผู้เล่นจริงบนเครื่องจริง/ผ่านอินเทอร์เน็ต (ระบุใครและเมื่อไร)

## 1. ทดสอบอัตโนมัติ (Claude รันเองได้)
ปิด Unity Editor ก่อน (Batch Mode ใช้โปรเจกต์ที่เปิดอยู่ไม่ได้)

| คำสั่ง | ทำอะไร | ผลลัพธ์ |
|---|---|---|
| `powershell -ExecutionPolicy Bypass -File Tools\test.ps1` | EditMode Tests | `Logs/test-EditMode.xml`, `Logs/test-EditMode.log` |
| `powershell -ExecutionPolicy Bypass -File Tools\test.ps1 -Platform PlayMode` | PlayMode Tests (เมื่อมี) | `Logs/test-PlayMode.*` |
| `powershell -ExecutionPolicy Bypass -File Tools\build.ps1` | Build Windows + Zip | `Builds/SpiritMovers-v<ver>-Windows(.zip)`, `Logs/build.log` |

- ใช้ Editor ตัวอื่น: ตั้ง `UNITY_EXE` เป็น Path ของ `Unity.exe`
- ทุกครั้งหลัง Compile/Test/Build ต้องอ่าน Log (MASTER_PROMPT 3.4)
- Smoke test ของ Build: เปิด `SpiritMovers.exe -batchmode -nographics -logFile <path>` แล้วตรวจบรรทัด `[Boot] Spirit Movers v<ver>`

### Test ที่มีตอนนี้
| ชุด | ครอบคลุม | ผลล่าสุด |
|---|---|---|
| `GameVersionTests` (EditMode) | Parse/TryParse, การตรวจเวอร์ชันตรงกันสำหรับเข้าห้อง | ผ่าน (2026-10-30) |
| `ThaiShaperTests` (EditMode) | กฎจัดรูปวรรณยุกต์/สระไทย (D-035) | ผ่าน (2026-10-30) |
| `TrueTypeGlyphNamesTests` (EditMode) | อ่านชื่อ Glyph สำรองจากฟอนต์ Mali | ผ่าน — รวม 26/26 (2026-10-30) |
| ต้นแบบ Network (`Tools/proto-netsync-matrix.ps1`) | Host+Client ผ่าน Relay, วิธียกของ A/B × Latency 0/100/200 ms | ผ่าน — ผลใน `docs/M2_REPORT.md` |

## 2. ทดสอบหลายผู้เล่นในเครื่องเดียว (วางแผน — M2/M3)
- Multiplayer Play Mode (แพ็กเกจทางการ — ตรวจเวอร์ชันก่อนติดตั้ง) หรือเปิด Build หลาย Instance
- จำลอง Latency/Packet Loss ด้วย Network Simulator ของ Unity Transport (ค่า 50/100/200 ms, Loss 1–5%)
- ผลสูงสุด: `Tested`

## 3. ทดสอบออนไลน์จริง (ผู้ใช้ + เพื่อน)
จะเพิ่ม **Playtest Checklist**, ตำแหน่งไฟล์ Log ที่ต้องส่งกลับ และแบบฟอร์มรายงานผล เมื่อมีต้นแบบ Network (M2) — ดู MASTER_PROMPT 9.4

### ตำแหน่ง Log ของ Player บน Windows
Unity เขียน `Player.log` ไว้ที่ `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\` → สำหรับเกมนี้คือ `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\Player.log` (ตรวจยืนยันกับ Build จริงก่อนแจกเพื่อน)
