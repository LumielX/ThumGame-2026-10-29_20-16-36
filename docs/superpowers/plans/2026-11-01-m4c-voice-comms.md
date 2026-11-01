# M4C Voice & Comms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ทีมคุยกันได้ทั้งเสียงและไม่ใช้เสียง — รถตู้ได้ยินทุกคน · ในบ้านได้ยินเฉพาะคนใกล้ · วิทยุสื่อสาร (เครื่องมือจริง กดค้างพูด เสียงซ่า ผีรบกวน) · ปิด/ปรับเสียงรายคน · ไอคอนกำลังพูด · ไมค์ดังทำให้ผีหูไวได้ยิน (เลือกปิดได้) · เสียงหลุดเกมเล่นต่อและเชื่อมใหม่เอง · แชตข้อความไทย · Ping + ข้อความด่วน

**Architecture:** กติกาที่ไม่ขึ้นกับ Vivox (ชื่อช่องตามช่วงเกม, จำนวนวิทยุ, ประตูเสียงไมค์ 4 ครั้ง/วินาที, การรบกวนวิทยุตามความโกรธ, จัดข้อความแชต, กติกา Ping) อยู่ใน Core (`Core/Voice/`, EditMode) · `IVoiceService` (ตาม TECHNICAL_DESIGN §8.1) อยู่ใน Net ข้าง `UgsSessionService` มีตัวจริง `VivoxVoiceService` และตัวปลอม `FakeVoiceService` สำหรับ PlayMode (ไม่ใช้เน็ต) · Runtime มี `VoiceDirector` (บน GameNetwork: เข้า/ออกช่องตามฉาก, Push-to-Talk, เชื่อมใหม่) และส่วนในเกม (วิทยุ, ไมค์ → ผี, HUD ผู้พูด, แชต, Ping) · Ping/ข้อความด่วนส่งผ่าน NGO (ใช้ได้แม้เสียงหลุด) · ส่วนที่ต้องมี Vivox จริงตรวจด้วยสคริปต์หลาย Instance ที่ฉีดไฟล์เสียงแทนไมค์ (แบบ M2)

**Tech Stack:** Unity 6000.3.27f1 · Vivox 16.12.1 (`com.unity.services.vivox`, ติดตั้งแล้ว — Free Tier 5,000 PCU ตรวจ 2026-10-30, D-009) · NGO 2.13.3 · Multiplayer Services 2.4.1 (Authentication เดียวกัน) · Input System 1.20.1 · UGUI/TMP + `ThaiShaper` · Unity Test Framework · `Tools/test.ps1`, Relay + บอท

**Spec:** `docs/GDD.md` §8.3, §10, §16, §18 · `docs/TECHNICAL_DESIGN.md` §8, §11, §13, §19 · D-009, D-010, D-013 (J3) · `docs/M2_REPORT.md` หัวข้อ 3 (ผลต้นแบบ Vivox) · ภาพรวม + ข้อค้าง: `docs/superpowers/plans/2026-10-31-m4-overview.md`

## Global Constraints
- **Vivox** ใช้ Sign-in เดียวกับ Multiplayer (D-009) · ไม่เก็บ Secret Key (Token จาก Unity Authentication, M2) · README ต้องแจ้งว่าเสียงพูดผ่านเซิร์ฟเวอร์ Unity/Vivox (D-009)
- **รถตู้:** ได้ยินทุกคน (Group) · **ในบ้าน:** Positional — **เข้าก่อน Group** ตามเอกสาร Vivox · อัปเดตตำแหน่ง **2–4 ครั้ง/วินาที** (TECHNICAL_DESIGN §8.2) · ค่าต้นแบบ M2: ได้ยินเต็ม 2 ม., เงียบที่ 25 ม., InverseByDistance
- **วิทยุ:** Group channel แยก "radio" · ผู้ถือวิทยุกดค้าง = ส่งเข้าช่องวิทยุ · ผู้ฟังที่**ถือวิทยุ**ได้ยินผ่าน **Participant Audio Tap** + ฟิลเตอร์ (High-pass 400 Hz → Low-pass 3,200 Hz → Distortion 0.35, ค่าจาก M2) · Channel Tap ใช้ได้กับช่องแรกเท่านั้น (M2) · ผีรบกวน = เสียงซ่า/ตัดเป็นช่วงเมื่อโกรธ (GDD §6.2 ขั้น 2 "วิทยุซ่า")
- **จำนวนวิทยุ (GDD §8.3):** 1 คน = 0 · 2–3 คน = 2 · 4–6 คน = 2–3 (⚙ 4 คน = 2, 5–6 คน = 3) · เป็นวัตถุจริง วาง/โยน/ส่งต่อได้ เหมือนกล้องวิญญาณ
- **ผีหูไว (TECHNICAL_DESIGN §8.3, D-010):** Client อ่าน `AudioEnergy` ของ**ตัวเอง** เฉพาะเมื่อเปิด "ให้ผีได้ยินเสียงไมค์ของฉัน" → เกินเกณฑ์ ⚙ → `NoiseEvent` (ชนิด `Voice`) ไป Host **ไม่เกิน 4 ครั้ง/วินาที** · ปิดตัวเลือก = ไม่ส่งเลย
- **เลือกไมค์/ลำโพง · Push-to-Talk หรือพูดแล้วส่ง (ค่าเริ่มต้น: พูดแล้วส่ง) · ปรับ/ปิดเสียงรายคน · ไอคอนกำลังพูด · แนะนำวิธีเปิดสิทธิ์ไมค์ Windows · Voice หลุด เกมเล่นต่อได้ + เชื่อมใหม่อัตโนมัติ · ผู้ไม่ใช้ไมค์เล่นได้ด้วย Ping/ข้อความด่วน** (D-009)
- **แชตข้อความ** ใน Lobby และในเกม ผ่าน **Vivox Text** · พิมพ์ไทยได้ (D-013 J3) · ช่องพิมพ์ใช้ `EnableThaiShaping` (M2)
- **ห้ามเปิดไมค์จริงในการรันอัตโนมัติ** (บทเรียน M2 ข้อ 5): บอท/เทสต์ปิดไมค์เสมอ ยกเว้นฉีดไฟล์ WAV · PlayMode ใช้ `FakeVoiceService` (ไม่แตะ Vivox/เน็ต)
- Solo ไม่ใช้ Voice (ไม่มีเพื่อน ไม่ต้องใช้เน็ต) · Ping/ข้อความด่วนใช้ได้ทุกโหมด
- ข้อความทุกอันผ่าน Localization + ThaiShaper · ปุ่มใหม่เปลี่ยนได้ในหน้า Settings · Core ไม่อ้าง UnityEngine · MonoBehaviour ไฟล์ละคลาส · Build ห้าม `-nographics` · ฉากใช้ `SaveWithSceneIds`
- **การตัดสินใจประเภท C ในแผนนี้:**
  - ชื่อช่อง: `van-{code}` (Group, TextAndAudio) · `house-{code}` (Positional, AudioOnly) · `radio-{code}` (Group, TextAndAudio — แชตในงานใช้ช่องนี้) · `{code}` = รหัสห้อง
  - ส่งเสียง: รถตู้ → `van` · บ้าน → `house` · กดวิทยุ → `house` + `radio` พร้อมกัน (คนใกล้ได้ยินเสียงจริงด้วย)
  - เกณฑ์ไมค์ดัง ⚙ `AudioEnergy ≥ 0.6` (ต้นแบบ M2 ได้ 0.6–0.7 ตอนพูดปกติใกล้ไมค์ — ปรับหลังคนจริงทดสอบ) · ความดังของ NoiseEvent = energy
  - ผีรบกวนวิทยุ: ขั้น 0–1 ไม่มี · ขั้น 2 ซ่า 0.3 · ขั้น 3 ซ่า 0.5 + ตัดเสียง 25% ของช่วง 0.4 วินาที · ขั้น 4 ซ่า 0.7 + ตัด 50%
  - ปุ่มใหม่ ⚙: แชต `T` · Ping คลิกกลาง · วงล้อข้อความด่วนกดค้าง `Q` (เลือกด้วยทิศเมาส์หรือเลข 1–6) · พูดทางวิทยุ = กดค้าง Use (คลิกขวา) ขณะถือวิทยุ
  - ข้อความด่วน 6 ข้อ: "มาช่วยยกหน่อย", "ผีอยู่ตรงนี้!", "ระวัง!", "ไปที่รถตู้", "โอเค", "ไม่"
  - Ping: อยู่ 6 วินาที · คนละไม่เกิน 3 อันพร้อมกัน · ห่างกันอย่างน้อย 0.5 วินาที (Host บังคับ) · แชต: ยาวไม่เกิน 120 ตัว, ห่างกัน 0.5 วินาที
  - ผู้พูดจับคู่กับผู้เล่นด้วย UGS PlayerId (ส่งมาใน Connection Payload บรรทัดที่สาม → `LobbyMember.VoiceId`) ไม่ใช้ DisplayName ของ Vivox
  - ปิดเสียงรายคน/ระดับเสียงรายคน จำแค่ใน Session (ไม่บันทึกลงไฟล์)
  - เชื่อมใหม่: เมื่อหลุดลองเข้าช่องของฉากปัจจุบันทุก 5 วินาที ไม่เกิน 12 ครั้ง แล้วแจ้ง "เสียงใช้ไม่ได้ — เล่นต่อได้ ใช้ Ping/แชตแทน" (แชตก็ใช้ไม่ได้ช่วงนั้น)
- **ข้อค้างที่รับ:** เลือกอุปกรณ์ไมค์/ลำโพง (M2/M4B → M4C) · ใช้ค่า `pushToTalk`, `ghostHearsMic`, `GameAudio.Gain(Voice)` จาก M4B
- **นอกขอบเขต M4C:** เสียงก้องตามห้อง (Audio Mixer + Reverb, M5 — D-047) · คำใบ้จากวิทยุหัวหน้า (GDD §5.4, M5+) · ไอคอนพูดเหนือหัวตัวละคร (M5 HUD จริง — M4C ทำรายชื่อผู้พูดบนจอ) · Speech-to-text · Gamepad

## Review Focus
1. **Voice ใช้ไม่ได้ (ไม่มีเน็ต, Vivox ล่ม, Login ไม่ผ่าน, หลุดกลางงาน)** — เกมเล่นต่อเหมือนเดิม มีข้อความ ลองใหม่เอง ไม่มี Exception ขวางเมนู/รถตู้/งาน (Task 3: `Voice_*Fails*`, `Reconnects*`)
2. **ไมค์จริงต้องไม่เปิดเองโดยไม่ตั้งใจ** — บอท/เทสต์ปิดไมค์เสมอ · Push-to-Talk เปิดอยู่แต่ไม่ได้กด = ปิดไมค์ · ปิด "ผีได้ยินไมค์" = ไม่ส่งอะไรเลย (Task 3: `PushToTalk_*`, Task 5: `MicNoise_Off*`)
3. **สแปม** — Ping/ข้อความด่วน/แชต/รายงานเสียงไมค์ถูกจำกัดที่ Host · ข้อความแชตว่าง/ยาว/มีอักขระควบคุมถูกจัด (Task 1: `ChatText_*`, `PingRules_*`, Task 5/7/8: `*_RateLimited*`)
4. **วิทยุ** — วาง/โยน/ส่งต่อวิทยุระหว่างกดพูด = หยุดส่งทันที · คนถือออกจากเกม · คนไม่ได้ถือวิทยุไม่ได้ยินวิทยุ (Task 4: `Radio_*`)
5. **เปลี่ยนฉาก** (รถตู้ → งาน → รถตู้ → เมนู, Host ออก, เน็ตหลุด) — ออก/เข้าช่องถูกครั้ง ไม่มี Tap/Subscription ซ้ำ ออกจากห้องแล้ว Logout (Task 3: `Channels_*`, Task 4: `Radio_TapsCleanedUp*`)

---

### Task 1: Core — กติกาเสียงและการสื่อสาร

**Files:**
- Create: `Core/Voice/VoiceRules.cs`, `Core/Voice/MicNoiseGate.cs`, `Core/Voice/RadioInterference.cs`, `Core/Voice/ChatText.cs`, `Core/Voice/PingRules.cs`
- Modify: `Core/Ghost/ToolConfig.cs` (`Radios(int players)`)
- Test: `Tests/EditMode/Voice/VoiceRulesTests.cs`

**Interfaces:**
- Produces (namespace `SpiritMovers.Core.Voice`):
  - `enum VoiceChannelKind { Group, Positional }` · `readonly struct VoiceChannel { string Name; VoiceChannelKind Kind; bool Text; }`
  - `enum VoiceSpace { None, Van, House }` · `static IReadOnlyList<VoiceChannel> VoiceRules.ChannelsFor(VoiceSpace space, string roomCode)` — None/โค้ดว่าง → ว่าง · Van → `[van-{code}]` · House → `[house-{code} (Positional), radio-{code}]` (Positional มาก่อน)
  - `static IReadOnlyList<string> VoiceRules.TransmitTo(VoiceSpace space, string roomCode, bool radioHeld)` — Van → van · House → house (+ radio เมื่อ `radioHeld`)
  - `static string VoiceRules.ChatChannel(VoiceSpace space, string roomCode)` — Van → van · House → radio · None → null
  - `const float VoiceRules.FullVolumeDistance = 2f, SilentDistance = 25f` · `const float PositionInterval = 0.25f` (4 ครั้ง/วินาที)
  - `static int ToolConfig.Radios(int players)` — 1 → 0 · 2–4 → 2 · 5–6 → 3
  - `sealed class MicNoiseGate` ctor `(float threshold = 0.6f, float minInterval = 0.25f)` · `bool TryReport(bool enabled, bool transmitting, double energy, float now)`
  - `static (float Crackle, bool Dropped) RadioInterference.At(int angerStage, float time, int seed)` — ค่าตาม Global Constraints · ตัดเสียงคิดเป็นช่วง 0.4 วินาทีจาก hash(seed, ช่วง)
  - `static string ChatText.Clean(string raw)` — ตัดหัวท้าย, ทิ้งอักขระควบคุมและ Surrogate, ยุบช่องว่าง, ≤ 120 ตัว (ไม่ตัดกลางสระ/วรรณยุกต์ — ใช้กติกาเดียวกับ `PlayerName`) · ว่าง → `""`
  - `enum QuickMessage { NeedHands, GhostHere, Watch, ToVan, Okay, No }` · `static string ChatText.KeyFor(QuickMessage m)` → `"quick.need_hands"` …
  - `sealed class PingRules` ctor `(float lifetime = 6f, float cooldown = 0.5f, int maxActive = 3)` · `bool TryAdd(ulong sender, float now)` (ลบของหมดอายุก่อน) · `int ActiveFor(ulong sender, float now)`

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] ChannelsFor_VanHouseNone()            // Van "AB12" → ["van-AB12"] · House → ["house-AB12" Positional, "radio-AB12" Group] (ลำดับนี้) · None / "" → []
[Test] TransmitTo_RadioAddsRadioChannel()    // House false → ["house-AB12"] · House true → ["house-AB12","radio-AB12"] · Van true → ["van-AB12"]
[Test] ChatChannel_VanOrRadio()              // Van → "van-AB12" · House → "radio-AB12" · None → null
[Test] Radios_ByCrew()                       // 1→0, 2→2, 3→2, 4→2, 5→3, 6→3
[Test] MicNoiseGate_OnlyWhenEnabledTransmittingLoud()  // enabled false → false · transmitting false → false · 0.59 → false · 0.6 → true   (Review Focus 2)
[Test] MicNoiseGate_AtMostFourPerSecond()    // พูดดังทุก 0.05 วินาทีเป็นเวลา 1 วินาที → true ไม่เกิน 4 ครั้ง   (Review Focus 3)
[Test] RadioInterference_ByStage()           // ขั้น 0,1 → (0,false) ทุกเวลา · ขั้น 2 → Crackle 0.3, ไม่ตัด · ขั้น 4 → Crackle 0.7 และใน 100 ช่วงตัด 40–60 ช่วง · seed เดิม เวลาเดิม → ผลเดิม
[Test] ChatText_CleansAndCaps()              // "  สวัสดี\u0007  ทุกคน " → "สวัสดี ทุกคน" · 300 ตัว → 120 · "\n\n" / null → "" · "😀ไปกัน" → "ไปกัน"   (Review Focus 3)
[Test] QuickMessage_EveryKeyDistinct()       // 6 ค่า 6 key ไม่ซ้ำ ขึ้นต้น "quick."
[Test] PingRules_CooldownAndMaxActive()      // ส่ง 0.0 ✓, 0.2 ✗ (cooldown), 0.6 ✓, 1.2 ✓, 1.8 ✗ (ครบ 3) · 6.1 ✓ (อันแรกหมดอายุ) · ผู้ส่งอื่นไม่ถูกนับรวม   (Review Focus 3)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter VoiceRulesTests` → ไม่คอมไพล์
- [ ] **Step 3: เขียนโค้ดตาม Interfaces**
- [ ] **Step 4: รัน EditMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: core voice and comms rules`

---

### Task 2: `IVoiceService` — Vivox จริงและตัวปลอม

**Files:**
- Create: `Net/IVoiceService.cs`, `Net/VoiceParticipant.cs`, `Net/VivoxVoiceService.cs`, `Dev/FakeVoiceService.cs` (ใช้ใน PlayMode และบอทโหมด `-dev-voice-fail` — อยู่ใน Dev assembly ไม่ปนกับโค้ดเกม)
- Modify: `Net/SpiritMovers.Net.asmdef` (+ `Unity.Services.Vivox`)
- Test: `Tests/PlayMode/FakeVoiceServiceTests.cs` (สัญญาของ Interface ที่ตัวจริงต้องทำเหมือน)

**Interfaces:**
- Consumes: `VoiceChannel`, `VoiceChannelKind` (Task 1) · การ Sign-in ของ `UgsSessionService.InitializeAsync` (Vivox ใช้ Authentication เดียวกัน)
- Produces (namespace `SpiritMovers.Net`):
  - `readonly struct VoiceParticipant { string PlayerId; string Channel; bool Speaking; double Energy; bool IsSelf; }`
  - `interface IVoiceService`:
    - `Task LoginAsync(string displayName)` · `Task LogoutAsync()` · `bool IsLoggedIn` · `string PlayerId` (UGS PlayerId ของเครื่องนี้)
    - `Task JoinAsync(VoiceChannel channel)` · `Task LeaveAsync(string channelName)` · `IReadOnlyCollection<string> Joined`
    - `Task TransmitAsync(IReadOnlyList<string> channels)` (ว่าง = ไม่ส่ง)
    - `bool MicMuted { get; set; }` · `void SetOutputVolume(float gain01)` (แปลงเป็นช่วง Vivox −50…+50, 1.0 = 0) · `void SetParticipantMuted(string playerId, bool muted)` · `void SetParticipantVolume(string playerId, float gain01)`
    - `void UpdatePosition(Transform head, string channelName)`
    - `double LocalEnergy` (ของตัวเองบนช่องที่กำลังส่ง) · `IReadOnlyList<VoiceParticipant> Participants`
    - `AudioSource TapParticipant(string playerId, string channelName)` (Participant Tap, `SilenceInChannelAudioMix = true`) · `void ReleaseTap(string playerId, string channelName)`
    - `Task SendTextAsync(string channelName, string text)` · `event Action<string, string, string> TextReceived` (senderPlayerId, channel, text)
    - `IReadOnlyList<string> InputDevices`, `OutputDevices` · `Task SetInputDeviceAsync(string name)` · `Task SetOutputDeviceAsync(string name)` (ชื่อไม่พบ → ใช้ค่าเริ่มต้นของระบบ)
    - `event Action ConnectionLost` · `event Action ConnectionRecovered`
  - `VivoxVoiceService` — Positional ใช้ `Channel3DProperties(SilentDistance=25, FullVolumeDistance=2, 1f, InverseByDistance)` · Logout จำกัดเวลา 5 วินาที (บทเรียน M2 ข้อ 4) · `ConnectionRecovering` → ไม่แจ้ง, `ConnectionFailedToRecover` → `ConnectionLost` · `-dev-wav <path>` (Development เท่านั้น) → `StartAudioInjection` แทนไมค์ · ไม่มี `-dev-wav` ในการรันอัตโนมัติ (`-batchmode`) → `MicMuted = true` เสมอ
  - `FakeVoiceService` — เก็บทุกการเรียก (`Calls`), `FailLogin`, `SimulateDrop()`, `SimulateRecover()`, `SetEnergy(double)`, `AddParticipant(VoiceParticipant)`, `DeliverText(...)`, ส่งข้อความกลับเข้า `TextReceived` ของตัวเอง

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[Test] Fake_JoinLeaveTracksChannels()        // Join van → Joined {van} · Leave → {}
[Test] Fake_TextEchoesToSender()             // SendTextAsync("van-AB12","สวัสดี") → TextReceived(self PlayerId, "van-AB12", "สวัสดี")
[Test] Fake_LoginFailureThrows()             // FailLogin → LoginAsync throws · IsLoggedIn false
[Test] Fake_DropAndRecoverRaiseEvents()      // SimulateDrop → ConnectionLost ครั้งเดียว · Joined ว่าง · SimulateRecover → ConnectionRecovered
[Test] OutputVolume_MapsToVivoxRange()       // VivoxVoiceService.ToVivoxVolume(1f)=0, (0f)=-50, (0.5f) ระหว่าง -50..0 (internal static)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter FakeVoiceServiceTests`
- [ ] **Step 3: เขียนโค้ดตาม Interfaces** — ดู API จริงใน `Library/PackageCache/com.unity.services.vivox*/Runtime/IVivoxService.cs` และต้นแบบ `Prototypes/NetSync/VoiceProto.cs`
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: voice service on Vivox with a fake for tests`

---

### Task 3: `VoiceDirector` — เข้าช่องตามฉาก, Push-to-Talk, เสียงหลุด, เลือกอุปกรณ์

**Files:**
- Create: `Runtime/Voice/VoiceDirector.cs` (บน `GameNetwork` Prefab), `Runtime/Voice/VoiceStatus.cs` (ป้ายสถานะมุมจอ)
- Modify: `Runtime/Session/GameSession.cs` (เริ่ม/หยุด Voice เมื่อเข้า/ออกห้องออนไลน์), `Core/Save/SettingsData.cs` (`micDevice`, `speakerDevice` — ค่าว่าง = ค่าเริ่มต้นของระบบ, schema ยัง 1), `Runtime/Settings/SettingsPanel.cs` (แท็บเสียง: เลือกไมค์/ลำโพงแบบกดวน + คำแนะนำสิทธิ์ไมค์ Windows), `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`
- Create (เทสต์): `Tests/PlayMode/FakeSessionService.cs` — ย้าย `FakeService` จาก `MainMenuTests` ออกมาใช้ร่วม (Host ออนไลน์ปลอมบน 127.0.0.1 ได้รหัส "FAKE01")
- Test: `Tests/PlayMode/VoiceDirectorTests.cs`

**Interfaces:**
- Consumes: `IVoiceService`, `FakeVoiceService` (Task 2) · `VoiceRules` (Task 1) · `GameSession.JoinCode/IsSolo/Phase` · `GameData.Settings` (`pushToTalk`, `micDevice`, `speakerDevice`) · `GameAudio.Gain(Voice)` · `PlayerInputState.TalkHeld`
- Produces:
  - `VoiceDirector` (หนึ่งตัวต่อเกม): `static VoiceDirector Instance` · `IVoiceService Service` · `IVoiceService ServiceOverride { set; }` (เทสต์/บอท ตั้งก่อนเข้าห้อง) · `VoiceSpace Space` · `VoiceState State` (`Off`, `Connecting`, `On`, `Lost`, `Unavailable`) · `bool RadioHeld { get; set; }` (Task 4 ตั้ง) · `bool Transmitting` · `event Action StateChanged`
  - เข้าห้องออนไลน์ (Host/Join สำเร็จ) → Login (ชื่อผู้เล่น) → ฉาก Lobby = Van, Job_Greybox = House → Join ช่องตาม `ChannelsFor` (ออกช่องเก่าที่ไม่อยู่ในรายการ) · Solo/เมนู → ออกทุกช่อง + Logout · ในบ้านอัปเดตตำแหน่งหัวทุก `PositionInterval`
  - ไมค์: `MicMuted = pushToTalk && !TalkHeld && !RadioHeld` (ทุกเฟรม) · ส่งเสียงไปที่ `TransmitTo(Space, code, RadioHeld)` · ระดับเสียงออก = `GameAudio.Gain(Voice)`
  - หลุด/Login ไม่ผ่าน → `Lost` → ลองเข้าช่องของฉากปัจจุบันใหม่ทุก 5 วินาที สูงสุด 12 ครั้ง → `Unavailable` · เกมไม่ถูกแตะ
  - Localization: `voice.connecting`, `voice.lost` "เสียงหลุด — กำลังเชื่อมใหม่…", `voice.unavailable` "ใช้เสียงไม่ได้ — เล่นต่อได้ ใช้ Ping/แชตแทน", `settings.mic`, `settings.speaker`, `settings.device_default` "ค่าเริ่มต้นของ Windows", `settings.mic_help` "ถ้าเพื่อนไม่ได้ยิน: Windows → Settings → Privacy & security → Microphone → เปิดให้แอปใช้ไมค์"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (ทุก Test ใช้ `FakeVoiceService` + Host ออนไลน์ปลอมแบบ `MainMenuTests.FakeService`)
```csharp
[UnityTest] Channels_VanThenHouseThenVan()      // ห้องปลอม "FAKE01" → รถตู้ Joined {van-FAKE01} · งาน → {house-FAKE01, radio-FAKE01} (house เข้าก่อน) · กลับรถตู้ → {van-FAKE01}   (Review Focus 5)
[UnityTest] Channels_LeaveRoomLogsOut()          // ออกห้อง (Esc → ออก) → Joined ว่าง, Logout ถูกเรียก 1 ครั้ง · Host ออก (Shutdown) → เหมือนกัน
[UnityTest] Solo_NoVoice()                       // เล่นคนเดียว → ไม่มีการเรียก Login/Join เลย · State Off
[UnityTest] PushToTalk_MutesUntilHeld()          // pushToTalk true → MicMuted true · TalkHeld → false · ปล่อย → true · pushToTalk false → false ตลอด   (Review Focus 2)
[UnityTest] Voice_LoginFailsGameGoesOn()         // FailLogin → State Lost, ป้าย voice.lost · Ready → งานเริ่มได้ตามปกติ   (Review Focus 1)
[UnityTest] Voice_DropMidJob_Reconnects()        // ในงาน SimulateDrop → Lost → (เวลาเร่งผ่าน internal RetryNow()) Join ใหม่ → On · ไม่มี Join ซ้ำช่องเดิม   (Review Focus 1, 5)
[UnityTest] Voice_GivesUpAfterRetries()          // ล้มทุกครั้ง → หลัง 12 ครั้ง State Unavailable, ป้าย voice.unavailable · เกมเล่นต่อ
[UnityTest] OutputVolume_FollowsVoiceSetting()   // voice 0.5, master 1 → SetOutputVolume(0.5)
[UnityTest] Devices_SavedChoiceApplied()         // Fake มีไมค์ "Mic A","Mic B" · settings.micDevice "Mic B" → SetInputDeviceAsync("Mic B") · ชื่อที่ไม่มีแล้ว → ค่าเริ่มต้น ไม่ throw
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter VoiceDirectorTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉาก/Prefab ใหม่** (`CreateGameScenes`, `LocalizationSetup.Setup`)
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: voice follows the team between van and house`

---

### Task 4: วิทยุสื่อสาร (เครื่องมือ)

**Files:**
- Create: `Runtime/Voice/Walkie.cs` (NetworkBehaviour + `IUsable` แบบ `SpiritCamera`), `Runtime/Voice/RadioFilter.cs` (MonoBehaviour บน AudioSource ของ Tap — ย้ายฟิลเตอร์จาก `Prototypes/NetSync/RadioVoiceEffect.cs` + เสียงซ่า/ตัดเสียง), `Runtime/Voice/RadioListener.cs` (บนผู้เล่นเจ้าของเครื่อง: สร้าง/ลบ Tap ต่อผู้พูดบนช่องวิทยุ, เปิดเสียงเฉพาะตอนถือวิทยุ)
- Modify: `Runtime/Job/JobDirector.cs` (`SpawnTools` + วิทยุตาม `ToolConfig.Radios`, `destroyWithScene` · `internal void SpawnToolsForTest(int players)` — Host ห้องปลอมมีคนเดียวจึงได้วิทยุ 0 เครื่อง), `Runtime/Player/PlayerInputState.cs` + `KeyboardMouseInput.cs` (`UseHeld`), `Editor/TestRoomSetup.cs` (Prefab วิทยุ + ใส่ใน Prefab List), `Editor/LocalizationSetup.cs`
- Test: `Tests/PlayMode/WalkieTests.cs`

**Interfaces:**
- Consumes: `VoiceDirector.RadioHeld`, `IVoiceService.TapParticipant/ReleaseTap/Participants` (Task 2–3) · `RadioInterference.At` (Task 1) · `JobDirector.AngerStage` · `PlayerCarrier.Held`
- Produces:
  - `Walkie` — `UsePromptKey` `"radio.talk"` ("{0} ค้างไว้เพื่อพูดทางวิทยุ") · เจ้าของเครื่องที่ถืออยู่ + `UseHeld` → `VoiceDirector.RadioHeld = true` · ปล่อยปุ่ม/วาง/โยน/ส่งต่อ/ถูกจับเข้าตู้/ออกจากเกม → `false` ทันที
  - `RadioListener` — ต่อผู้พูดแต่ละคน (ไม่ใช่ตัวเอง) บน `radio-{code}` มี Tap 1 ตัว + `RadioFilter` · ได้ยินเฉพาะเมื่อ**เจ้าของเครื่องถือวิทยุ** (ไม่ถือ = AudioSource.mute) · ผู้พูดออกจากช่อง → ReleaseTap · ออกจากฉากงาน → Release ทั้งหมด
  - `RadioFilter.Apply(int angerStage)` — HP 400 / LP 3,200 / Distortion 0.35 + `Crackle`/`Dropped` จาก `RadioInterference.At`
  - Localization: `radio.talk`, `radio.on_air` "กำลังพูดทางวิทยุ"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Fake voice + Host ออนไลน์ปลอม เพื่อให้มีช่องวิทยุ)
```csharp
[UnityTest] Radio_CountByCrew()                   // งาน 1 คน → ไม่มี Walkie · SpawnToolsForTest(2) → วิทยุเพิ่ม 2 เครื่อง (เทสต์อื่นในชุดนี้ใช้วิธีเดียวกันให้มีวิทยุถือ)
[UnityTest] Radio_TransmitsOnlyWhileHeldAndPressed() // ถือ + UseHeld → Transmit [house, radio] · ปล่อยปุ่ม → [house]
[UnityTest] Radio_DropWhileTalkingStops()         // ระหว่างกดพูด วาง/โยน → RadioHeld false ในเฟรมนั้น · Transmit [house]   (Review Focus 4)
[UnityTest] Radio_OnlyHoldersHear()               // Fake มีผู้พูด "p2" บน radio → Tap ถูกสร้าง 1 ตัว · ไม่ถือวิทยุ → AudioSource.mute true · ถือ → false   (Review Focus 4)
[UnityTest] Radio_TapsCleanedUpOnLeave()          // p2 ออกจากช่อง → ReleaseTap("p2") · กลับรถตู้ → ไม่มี Tap ค้าง · กลับงานอีกรอบ → Tap ไม่ซ้ำ   (Review Focus 5)
[UnityTest] Radio_GhostInterferenceFollowsAnger() // ApplyAngerForTest ถึงขั้น 2 → RadioFilter.Crackle 0.3 · ขั้น 4 → 0.7
[UnityTest] Radio_SecondJobFresh()                // งานที่สองมีวิทยุครบจำนวน ไม่มีของงานแรกค้าง (destroyWithScene เหมือนกล้อง)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter WalkieTests`
- [ ] **Step 3: เขียนโค้ด + สร้าง Prefab/ฉากใหม่** (`TestRoomSetup` + `CreateGameScenes`)
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน** (รวม `GhostTests`/`JobCycleTests` เดิม — จำนวนเครื่องมือเปลี่ยน)
- [ ] **Step 5: Commit** — `M4C: walkie-talkies with radio voice and ghost static`

---

### Task 5: ไมค์ดัง → ผีหูไว

**Files:**
- Create: `Runtime/Voice/MicNoise.cs` (NetworkBehaviour บน Player Prefab)
- Modify: `Editor/TestRoomSetup.cs` (ใส่ใน Player Prefab)
- Test: `Tests/PlayMode/MicNoiseTests.cs`

**Interfaces:**
- Consumes: `MicNoiseGate` (Task 1) · `VoiceDirector.Transmitting`, `Service.LocalEnergy`, `Service.MicMuted` (Task 3) · `GameData.Settings.ghostHearsMic` · `NoiseHub.Current` + `NoiseEvent(NoiseSource.Voice, …)` (M3D)
- Produces: `MicNoise` — เจ้าของเครื่อง: `TryReport(enabled: ghostHearsMic, transmitting: Transmitting && !MicMuted, energy: LocalEnergy, now)` → `ReportVoiceRpc(float energy)` (ไป Server) · Server: Gate ของตัวเองต่อ Client (เกณฑ์เดียวกัน, ช่วงห่าง 0.25 วินาที) → `NoiseHub.Current.Raise(new NoiseEvent(NoiseSource.Voice, position of that player's head, energy))` · ค่า energy จาก Client ถูก Clamp 0–1

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] MicNoise_LoudVoiceReachesTheGhost()  // งานออนไลน์ปลอม · Fake SetEnergy(0.8), พูดอยู่ → NoiseHub ได้ Voice event ที่ตำแหน่งหัวผู้เล่น ภายใน 0.5 วินาที
[UnityTest] MicNoise_OffSendsNothing()           // ghostHearsMic false + พูดดังต่อเนื่อง 2 วินาที → 0 event · ไม่มี RPC ออกจาก Client   (Review Focus 2)
[UnityTest] MicNoise_MutedOrPttReleasedSendsNothing() // pushToTalk true ไม่กด → 0 event
[UnityTest] MicNoise_ServerRateLimited()         // เรียก ReportVoiceRpc ตรง ๆ 20 ครั้งในเฟรมเดียว → event ≤ 1 · 1 วินาที ≤ 4 · energy 9 → loudness 1   (Review Focus 3)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter MicNoiseTests`
- [ ] **Step 3: เขียนโค้ด + สร้าง Prefab ใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: a loud microphone wakes the sharp-eared ghost`

---

### Task 6: ใครกำลังพูด · ปิด/ปรับเสียงรายคน

**Files:**
- Create: `Runtime/Voice/VoiceHud.cs` (รายชื่อผู้พูดมุมจอ + ไอคอนวิทยุ), `Runtime/Voice/PlayerVoicePanel.cs` (ในเมนู Esc: รายชื่อ + ปิดเสียง + ระดับเสียงรายคน)
- Modify: `Core/Session/ConnectionPayload.cs` (บรรทัดที่สาม = voice id, อ่าน Payload 2 บรรทัดเดิมได้), `Runtime/Session/LobbyGate.cs` (`VoiceIdFor`), `Runtime/Session/LobbyState.cs` (`LobbyMember.VoiceId`), `Runtime/Session/LobbyPanel.cs` (เครื่องหมายกำลังพูดข้างชื่อ), `Runtime/Session/PauseMenu.cs` (ปุ่ม "ผู้เล่น"), `Runtime/Session/GameSession.cs` (ใส่ PlayerId ลง Payload), `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`
- Test: `Tests/EditMode/Session/ConnectionPayloadTests.cs` (เพิ่ม), `Tests/PlayMode/VoicePeopleTests.cs`

**Interfaces:**
- Consumes: `IVoiceService.Participants/SetParticipantMuted/SetParticipantVolume`, `PlayerId` (Task 2–3) · `LobbyMember` (M4A/M4B)
- Produces:
  - `ConnectionPayload.Write(string version, string name, string voiceId = "")` · `TryRead(text, out version, out name, out voiceId)` (voiceId ≤ 64 ตัว, เฉพาะ A–Z a–z 0–9 `-_`) · ของเดิม 2 ตัวแปรยังใช้ได้
  - `LobbyMember.VoiceId` (`FixedString64Bytes`) · `static IReadOnlyList<LobbyMember> LobbyState.Roster` — สำเนารายชื่อล่าสุดที่ทุกเครื่องเก็บไว้ตอนอยู่ในรถตู้ ใช้ต่อในฉากงาน (ฉาก Lobby ถูกปิดไปแล้ว) · ล้างเมื่อกลับเมนู
  - `VoiceHud`: แถว "● ชื่อ" ต่อคนที่ `Speaking` (ตัวเองด้วย) · ผู้พูดทางวิทยุ (อ่านระดับจาก Tap ตามบทเรียน M2 3.1) ขึ้น "📻" แทนด้วยข้อความ `voice.radio_tag` "[วิทยุ]" (ฟอนต์ไม่มีอีโมจิ) · `internal string LastText`
  - `PlayerVoicePanel`: ต่อคน (ไม่รวมตัวเอง) ปุ่ม `voice.mute`/`voice.unmute` + Slider 0–100% · จำค่าตาม VoiceId ตลอด Session (ย้ายฉากไม่หาย) · `internal void SetMuted(string voiceId, bool)`, `SetVolume(string voiceId, float)`
  - Localization: `voice.radio_tag`, `voice.mute` "ปิดเสียง", `voice.unmute` "เปิดเสียง", `pause.players` "ผู้เล่น"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
// EditMode
[Test] Payload_CarriesVoiceId()                  // Write("0.0.2","บีม","abcDEF-12") → ได้ครบ 3 ค่า · voiceId "a b<c>" → "abc" · 2 บรรทัดเดิม → voiceId ""
// PlayMode (Fake voice + ห้องปลอม)
[UnityTest] Hud_ShowsWhoIsSpeaking()             // Fake participant p2 (VoiceId ของสมาชิก "เพื่อน") Speaking → HUD มี "เพื่อน" · หยุดพูด → หายใน 0.5 วินาที
[UnityTest] Hud_UnknownSpeakerShownAsPlayerN()   // PlayerId ที่ไม่มีใน Roster → "ผู้เล่น ?" ไม่ throw
[UnityTest] Mute_PerPersonAppliedAndKept()       // SetMuted(p2,true) → Fake.SetParticipantMuted(p2,true) · ย้ายรถตู้ → งาน → ยัง mute (เรียกซ้ำเมื่อ p2 เข้าช่องใหม่)
[UnityTest] Volume_PerPersonApplied()            // SetVolume(p2,0.3) → SetParticipantVolume(p2,0.3)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Filter ConnectionPayloadTests` และ `-Platform PlayMode -Filter VoicePeopleTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: who is talking, and muting a player`

---

### Task 7: แชตข้อความไทย

**Files:**
- Create: `Runtime/Voice/ChatPanel.cs` (บน GameNetwork: กล่องข้อความล่าสุด + ช่องพิมพ์)
- Modify: `Settings/Input/SpiritMovers.inputactions` (Gameplay/Chat = `<Keyboard>/t`), `Runtime/Player/PlayerInputState.cs` + `KeyboardMouseInput.cs` (`ChatPressed`), `Runtime/Settings/SettingsPanel.cs` (แถว Chat), `Editor/GameScenesSetup.cs`, `Editor/LocalizationSetup.cs`
- Test: `Tests/PlayMode/ChatTests.cs`

**Interfaces:**
- Consumes: `IVoiceService.SendTextAsync/TextReceived` (Task 2) · `VoiceRules.ChatChannel` · `ChatText.Clean` (Task 1) · `LobbyState.Roster` (Task 6) · `SettingsPanel.Open` ปิดแชตก่อน
- Produces: `ChatPanel` — กด Chat → ช่องพิมพ์เปิด (`EnableThaiShaping`, `PlayerMotor.Local.InputEnabled = false`, เมาส์ไม่ล็อก) · Enter ส่ง (Clean แล้วว่าง = ไม่ส่ง) · Esc ยกเลิก (ไม่เปิดเมนู Esc ในเฟรมนั้น) · ส่งได้ห่างกัน ≥ 0.5 วินาที · แสดง 6 ข้อความล่าสุด 10 วินาที ("ชื่อ: ข้อความ", ชื่อจาก Roster ตาม VoiceId) · ข้อความที่เข้ามาถูก Clean อีกครั้ง · Voice ไม่ `On` → ช่องพิมพ์แสดง `chat.unavailable` และไม่ส่ง · Solo → ไม่มีแชต · `internal string LastText`, `internal void Type(string)`, `internal bool IsTyping`
- Localization: `chat.placeholder` (มีแล้ว), `chat.hint` เปลี่ยนเป็น "กด {0} เพื่อพิมพ์", `chat.unavailable` "แชตใช้ไม่ได้ตอนเสียงหลุด", `action.chat` "แชต"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว** (Fake voice + ห้องปลอม, คีย์บอร์ดจำลองแบบ `SettingsPanelTests`)
```csharp
[UnityTest] Chat_ThaiMessageSentOnRightChannel()   // รถตู้: พิมพ์ "ไปกันเลย ผู้ใหญ่บ้าน" Enter → SendTextAsync("van-FAKE01", ข้อความเดิมไม่ Shape) · ในงาน → "radio-FAKE01"
[UnityTest] Chat_IncomingShownWithName()           // DeliverText(p2,"ช่วยด้วย") → LastText (Unshape) มี "เพื่อน: ช่วยด้วย"
[UnityTest] Chat_TypingStopsWalkingAndEscCancels() // กด T → IsTyping, InputEnabled false · Esc → ปิด ไม่ส่ง · เมนู Esc ไม่เปิด · InputEnabled true
[UnityTest] Chat_RateLimitedAndCleaned()           // ส่ง 5 ครั้งในเฟรมเดียว → 1 · ข้อความเว้นวรรคล้วน → 0 · ข้อความเข้ามา 300 ตัว → แสดง ≤ 120   (Review Focus 3)
[UnityTest] Chat_UnavailableWhenVoiceDown()        // SimulateDrop → พิมพ์แล้ว Enter → ไม่เรียก SendTextAsync, แสดง chat.unavailable   (Review Focus 1)
[UnityTest] Chat_RebindableKey()                   // Chat → Y ผ่าน ApplyBindingOverride → คำแนะนำ "กด Y เพื่อพิมพ์"
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter ChatTests`
- [ ] **Step 3: เขียนโค้ด + สร้างฉากใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: Thai text chat in the van and on the job`

---

### Task 8: Ping และข้อความด่วน

**Files:**
- Create: `Runtime/Voice/CommsHub.cs` (NetworkBehaviour — Prefab วางในฉาก Lobby และ Job_Greybox เป็น In-scene NetworkObject แบบ `LobbyState`), `Runtime/Voice/PingMarker.cs`, `Runtime/Voice/QuickWheel.cs`
- Modify: `Settings/Input/SpiritMovers.inputactions` (Gameplay/Ping = `<Mouse>/middleButton`, Gameplay/QuickWheel = `<Keyboard>/q`, Gameplay/Quick1..6 = `<Keyboard>/1`..`6`), `Runtime/Player/PlayerInputState.cs` + `KeyboardMouseInput.cs`, `Runtime/Settings/SettingsPanel.cs` (แถว Ping, QuickWheel), `Editor/TestRoomSetup.cs` + `Editor/GameScenesSetup.cs` (CommsHub ในฉาก Lobby/Job_Greybox), `Editor/LocalizationSetup.cs`
- Test: `Tests/PlayMode/PingTests.cs`

**Interfaces:**
- Consumes: `PingRules`, `QuickMessage`, `ChatText.KeyFor` (Task 1) · `ChatPanel` (Task 7: ข้อความด่วนขึ้นในกล่องแชตด้วย) · `PlayerMotor.Look/Head`
- Produces:
  - `CommsHub` — `PingRpc(Vector3 point)` (Server: `PingRules.TryAdd` ต่อผู้ส่ง → `ShowPingRpc(sender, point)` ถึงทุกคน) · `QuickRpc(QuickMessage m)` (Server: Cooldown 0.5 วินาที → `ShowQuickRpc(sender, m)`) · ใช้ได้ทั้ง Solo/ออนไลน์ ไม่ผ่าน Vivox · `internal IReadOnlyList<PingMarker> Markers`
  - Ping = Raycast จากหัวผู้เล่น 30 ม. (โดนอะไร = จุดนั้น, ไม่โดน = จุดที่ 10 ม.) → Marker สีอำพัน + ชื่อผู้ส่ง หายใน 6 วินาที
  - `QuickWheel` — กดค้าง QuickWheel → วงล้อ 6 ช่อง (เมาส์ไม่ล็อกขณะเปิด, เลือกตามทิศเมาส์จากกลางจอ), ปล่อย = ส่งช่องที่ชี้ (กลางจอ = ไม่ส่ง) · หรือกด Quick1–6 ส่งทันที · ข้อความแสดงในกล่องแชตเป็น "ชื่อ: ข้อความ" ในภาษาของผู้รับ
  - Localization: `quick.need_hands`… 6 ข้อ, `action.ping` "ปักหมุด", `action.quick_wheel` "วงล้อข้อความ"

- [ ] **Step 1: เขียน Test ที่ล้มเหลว**
```csharp
[UnityTest] Ping_ShownToEveryoneAndFades()     // Solo งาน: กด Ping มองพื้น → Marker 1 อันที่จุดชน, มีชื่อผู้ส่ง · 6.5 วินาที → หาย
[UnityTest] Ping_RateLimitedOnHost()           // เรียก PingRpc 10 ครั้งในเฟรมเดียว → Marker 1 · 4 ครั้งห่างกัน 0.6 วินาที → สูงสุด 3 อยู่พร้อมกัน   (Review Focus 3)
[UnityTest] Quick_NumberKeySendsMessage()      // กด Quick2 → กล่องแชตมี "ผีอยู่ตรงนี้!" พร้อมชื่อ · ภาษาอังกฤษ → "Ghost here!"
[UnityTest] Quick_WheelPicksByDirection()      // เปิดวงล้อ, ชี้ขวา (QuickWheel.PickAt(new Vector2(1,0))) ปล่อย → ข้อความช่องขวา · ชี้กลาง → ไม่ส่ง
[UnityTest] Ping_WorksWithoutVoice()           // Fake voice Unavailable → Ping/ข้อความด่วนยังทำงาน   (Review Focus 1)
[UnityTest] Comms_SecondJobFresh()             // งานที่สองไม่มี Marker ค้าง (CommsHub ผูกกับฉาก)
```
- [ ] **Step 2: รันให้ล้มเหลว** — `Tools\test.ps1 -Platform PlayMode -Filter PingTests`
- [ ] **Step 3: เขียนโค้ด + สร้าง Prefab/ฉากใหม่**
- [ ] **Step 4: รัน EditMode + PlayMode ทั้งหมดให้ผ่าน**
- [ ] **Step 5: Commit** — `M4C: pings and quick messages`

---

### Task 9: ตรวจด้วย Vivox จริง — หลาย Instance, ภาพหน้าจอ, เอกสาร

**Files:**
- Create: `Tools/m4c-voice-test.ps1` (สร้างไฟล์ WAV เสียงสังเคราะห์ 48 kHz เองตอนรัน — ไม่ใช้ไฟล์จากที่อื่น), `Dev/DevVoiceBot.cs` (`-dev-voice <role>`)
- Modify: `README.md` (เสียงพูดผ่านเซิร์ฟเวอร์ Unity/Vivox · วิธีเปิดสิทธิ์ไมค์ Windows), `docs/TESTING.md`, `docs/STATUS.md`, `CHANGELOG.md`, `docs/superpowers/plans/2026-10-31-m4-overview.md`
- Create: `docs/screenshots/m4/voice-hud-th.png`, `chat-thai.png`, `ping-quick-th.png`, `radio-held-th.png`

**Interfaces:**
- Consumes: ทุก Task ก่อนหน้า · `Tools/build.ps1 -Development` · Relay + บอทแบบ `m4a-flow-test.ps1`

- [ ] **Step 1: บอทและสคริปต์ (ให้ล้มเหลวกับ Build เก่าก่อน)** — Host ฉีด WAV (`-dev-wav`), Client ปิดไมค์ · ขั้น: (1) รถตู้: Client ได้ยิน Host (`peak energy ≥ 0.3`) และแชตไทยถึงกันทั้งสองทาง (สคริปต์ตรวจจากรหัส ASCII ท้ายข้อความ เช่น `[c1]` เพื่อไม่ขึ้นกับการถอดรหัส Log) · (2) งาน ห่าง 1 ม.: ได้ยิน · ห่าง 40 ม.: ไม่ได้ยินทางช่อง house · (3) Host ถือวิทยุกดพูด, Client ถือวิทยุห่าง 40 ม.: Tap ได้ระดับ ≥ 0.3 · Client วางวิทยุ: Tap ถูกปิดเสียง · (4) Host Log มี `NoiseEvent Voice` จากไมค์ของ Host (ghostHearsMic เปิด) และ ≤ 4/วินาที · (5) Ping/ข้อความด่วนถึง · (6) `-dev-voice-fail` (Login ล้ม) อีกเครื่องยังเล่นงานจบได้ · SUMMARY `[M4C]` ต่อขั้น
- [ ] **Step 2: Build ใหม่และรัน** — `Tools\build.ps1 -Development` · `Tools\m4c-voice-test.ps1` 3 รอบ · `-LatencyMs 150` 1 รอบ · `Tools\m4a-flow-test.ps1` 1 รอบ (เส้นทางเดิมไม่พัง) → PASS ทุกขั้น
- [ ] **Step 3: ภาพหน้าจอจาก Build จริง (หน้าต่าง)** — รายชื่อผู้พูด, แชตไทย, Ping + วงล้อข้อความด่วน, ถือวิทยุ (ดูภาพจริงก่อนบันทึก)
- [ ] **Step 4: เอกสาร** — README, TESTING (Test ใหม่ + ผล Vivox), STATUS, CHANGELOG, m4-overview (ผล M4C + ข้อค้าง) · ระบุชัด: **ความดังตามระยะ, เสียงวิทยุ, ความไวไมค์ → ผี ต้องให้คนฟังจริงยืนยัน** (`Verified` ตอนทดสอบกับเพื่อน, D-040)
- [ ] **Step 5: Commit** — `M4C: voice and comms over Relay and Vivox, screenshots and docs`
