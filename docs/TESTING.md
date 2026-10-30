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

**บันทึกเพิ่ม:** ตัวเลข Received KB/s ที่มุมจอ · ความรู้สึกหน่วง (ไม่หน่วง / พอรับได้ / หน่วงมาก) · ระยะทางโดยประมาณ (เมืองเดียวกัน/ต่างจังหวัด) · อินเทอร์เน็ตแต่ละฝั่ง (บ้าน/มือถือ)

**Log ที่ต้องส่งกลับ (ทั้งสองเครื่อง):** `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\Player.log` (วางใน Explorer Address bar ได้เลย) — ส่งหลังปิดเกม เพราะเปิดเกมใหม่จะเขียนทับ (ไฟล์ก่อนหน้าคือ `Player-prev.log`)

**แบบรายงานสั้น:** วันที่ · ผู้ทดสอบ (ชื่อเล่น) · ผ่านข้อไหน/ไม่ผ่านข้อไหน · อาการที่เจอ · แนบ Log 2 ไฟล์

### ตำแหน่ง Log ของ Player บน Windows
Unity เขียน `Player.log` ไว้ที่ `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\` → สำหรับเกมนี้คือ `%USERPROFILE%\AppData\LocalLow\SpiritMovers\Spirit Movers\Player.log` (ตรวจยืนยันกับ Build จริงก่อนแจกเพื่อน)
