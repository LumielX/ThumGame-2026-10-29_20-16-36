# CLAUDE.md — Spirit Movers (โปรเจกต์/Repo ยังชื่อ ThumGame)

เกม 3D Co-op Horror Comedy 1–6 คน แนว "จัดบ้านคืนผี" (D-001, D-002), Unity 6.3 LTS, Windows 64-bit, งบ 0 บาท
**ก่อนเริ่มงานทุก Session ให้อ่านตามลำดับ:** `docs/STATUS.md` → `docs/DECISIONS.md` → `docs/MASTER_PROMPT.md` (ฉบับเต็ม — ถ้าขัดกับไฟล์นี้ ให้ยึด MASTER_PROMPT)
ตอบเป็นภาษาไทย ใช้ศัพท์เทคนิคอังกฤษได้ ห้ามถามซ้ำสิ่งที่ตอบไว้แล้วใน DECISIONS.md

## ข้อเท็จจริง (ตรวจแล้ว 2026-10-29)
- Unity Editor: `X:\TheUnity\Editors\6000.3.27f1\Editor\Unity.exe` (ตรงกับโปรเจกต์ 6000.3.27f1), Unity Hub: `X:\TheUnity\Unity Hub\`
- Blender 5.2: `X:\TheBlender\blender.exe`
- โปรเจกต์ = Git Repo = Working Dir: `X:\PlayGame\ThumGame\` (URP, Input System, Test Framework)
- Remote: `LumielX/ThumGame-2026-10-29_20-16-36` branch `main` — **PUBLIC** (ตรวจด้วย gh)
- เครื่อง: i5-14450HX, RAM 16GB, RTX 5050 Laptop + Intel UHD, Windows 11
- โครงสร้างโฟลเดอร์เป้าหมาย: MASTER_PROMPT หัวข้อ 0.1 (`Assets/_Game/`, `Assets/ThirdParty/`, `ArtSource/Blender/`, `Tools/`)

## สถานะ
- Phase/Milestone ปัจจุบัน: ดู `docs/STATUS.md` (แหล่งข้อมูลหลัก) · M0 Discovery จบ (G4, D-027) · M1 Design จบ (G5, D-029) · เอกสารหลัก: `docs/GDD.md`, `docs/ART_BIBLE.md`, `docs/TECHNICAL_DESIGN.md`, `docs/PROJECT_PLAN.md` · ขอบเขต v0.1: `docs/discovery/05-scope-v0.1.md` · นอกขอบเขต: `docs/BACKLOG.md`

## Gates — ต้องหยุดรอคำตอบจริง
G1 แนวเกม · G2 ชื่อเกม · G3 ทุกหัวข้อ Phase 3–4 (ทีละหัวข้อ) · G4 ขอบเขต v0.1 · G5 GDD · G6 ผลตรวจ Network/Voice/ภาษาไทย (จบ M2) · G7 Vertical Slice · G8 ก่อนเผยแพร่สาธารณะ

## กฎการทำงาน
- ช่วง Discovery (M0–M1) จบแล้วที่ G5 (D-029) — ทำงานในโปรเจกต์ Unity ได้ตาม `docs/PROJECT_PLAN.md` · Commit ต้องมีความหมายและ Compile ผ่าน · **Commit แรก/ทุก Push ต้องได้รับอนุญาต**
- ถามทีละประเด็น ใช้ AskUserQuestion (ตัวเลือกแนะนำอยู่ก่อน) ห้ามถามเรื่องเทคนิคประเภท C
- บันทึกคำตอบทุกข้อลง `docs/DECISIONS.md` ทันที, อัปเดต `docs/STATUS.md` ก่อนจบทุกช่วงงาน
- ประเภท A (ต้องถามก่อน): แนวเกม, ชื่อ, Art Direction, ระบบหลัก, Multiplayer/Voice, ขอบเขตเวอร์ชัน, บริการที่อาจเสียเงิน/ต้องสมัคร, ติดตั้ง Skill/MCP/Package ที่ไม่ใช่ของ Unity ทางการ, Asset นอกรายการ CC0, การเผยแพร่, แก้ไฟล์นอกโปรเจกต์
- ราคา/Free Tier/API/เวอร์ชันแพ็กเกจ: ต้องค้นจากแหล่งทางการ ณ วันที่ทำงาน พร้อมลิงก์และวันที่ ห้ามตอบจากความจำ
- สถานะงาน: Planned / In Progress / Implemented / Tested / Verified / Blocked — ทดสอบในเครื่องเดียวได้สูงสุด `Tested`
- ห้ามอ้างว่าทำสิ่งที่ทำไม่ได้ ห้ามรายงานว่าเสร็จก่อนตรวจผลจริง (Log, Test, ภาพหน้าจอใน `docs/screenshots/`)

## ความปลอดภัย / Git / ลิขสิทธิ์
- ห้าม Force Push, Rewrite History, Push โดยไม่ได้รับอนุญาต — Commit แรกของ Claude อยู่ใน M2 หลังสรุปการเปลี่ยนแปลงค้างให้ผู้ใช้
- Repo เป็น Public: ห้ามใส่ Secret/Token/ข้อมูลส่วนตัวใน Repo (ระวัง `ProjectSettings.asset` มี organizationId/cloudProjectId)
- ห้ามใช้ Unity AI Generators หรือบริการที่อาจใช้เครดิต/เสียเงินโดยไม่ขออนุญาต
- Asset ภายนอก: เฉพาะ CC0 จากแหล่งที่อนุญาต (MASTER_PROMPT 6.1) บันทึก `THIRD_PARTY_ASSETS.md` ทันที ห้ามแก้ไฟล์ใน `ThirdParty/`
- `.blend` อยู่ใน `ArtSource/Blender/` เท่านั้น (ไม่อยู่ใน `Assets/`) แล้ว Export FBX
- ห้ามขอรหัสผ่าน/Token ในแชต ให้บอกขั้นตอนให้ผู้ใช้ทำเอง
- Build อยู่ใน GitHub Releases เท่านั้น ห้าม Commit Build/Library/Temp
