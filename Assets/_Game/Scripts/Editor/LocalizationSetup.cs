using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SpiritMovers.Editor
{
    /// <summary>
    /// Creates the Localization settings, the Thai/English locales and the UI string table (D-012, M2 item 5).
    /// Safe to run again: existing assets and translations are kept, only missing pieces are added.
    /// Batch mode: -executeMethod SpiritMovers.Editor.LocalizationSetup.Setup
    /// </summary>
    public static class LocalizationSetup
    {
        public const string Root = "Assets/_Game/Localization";
        public const string UiTable = "UI";
        public static readonly string[] LocaleCodes = { "th", "en" };

        // Seed entries (key, Thai, English); running Setup writes them into the tables.
        private static readonly (string Key, string Th, string En)[] s_Seed =
        {
            ("chat.hint", "กด Enter เพื่อพิมพ์แชต", "Press Enter to chat"),
            ("chat.placeholder", "พิมพ์ข้อความ…", "Type a message…"),
            ("language.switch", "F2 เปลี่ยนภาษา (ไทย/English)", "F2 change language (ไทย/English)"),
            ("interact.pickup", "ยก", "Pick up"),
            ("interact.drop", "วาง", "Put down"),
            ("interact.throw", "โยน", "Throw"),
            ("bell.ring", "กดกริ่งเรียกผี", "Ring for the ghost"),
            ("bell.nod", "ผีเคาะ {0} ครั้ง", "The ghost knocked {0} times"),
            ("job.leave", "ปิดงาน", "Close the job"),
            ("job.leave.wait", "รอทุกคนมาที่รถก่อนปิดงาน", "Everyone must be at the van to close the job"),
            ("cabinet.open", "เปิดตู้ช่วยเพื่อน", "Open the cabinet"),
            ("menu.host", "สร้างห้อง", "Host a room"),
            ("menu.join", "เข้าห้อง", "Join"),
            ("menu.code_hint", "รหัสห้อง", "Room code"),
            ("menu.solo", "เล่นคนเดียว", "Play solo"),
            ("menu.settings", "ตั้งค่า", "Settings"),
            ("menu.quit", "ออกจากเกม", "Quit"),
            ("menu.money", "เงินสะสม {0}", "Savings {0}"),
            ("results.total", "ยอดสะสม {0}", "Total savings {0}"),
            ("save.restored", "ไฟล์บันทึกเสีย — ใช้สำรองล่าสุดแทน", "The save file was damaged — the last backup was used"),
            ("save.reset", "ไฟล์บันทึกเสียและไม่มีสำรอง — เริ่มใหม่ (เก็บไฟล์เสียไว้ในโฟลเดอร์เกม)", "The save file was damaged with no backup — starting over (the damaged file is kept in the game folder)"),
            ("save.newer", "ไฟล์บันทึกมาจากเกมรุ่นใหม่กว่า — รอบนี้จะไม่บันทึก", "The save comes from a newer version of the game — nothing will be saved this time"),
            ("save.locked", "อ่านหรือซ่อมไฟล์บันทึกไม่ได้ (โปรแกรมอื่นอาจเปิดอยู่) — รอบนี้จะไม่บันทึก", "The save file could not be read or repaired (another program may have it open) — nothing will be saved this time"),
            ("action.pause", "เปิดเมนู", "Open the menu"),
            ("save.failed", "บันทึกไม่สำเร็จ — ตรวจพื้นที่ดิสก์", "Could not save — check your disk space"),
            ("menu.connecting", "กำลังเชื่อมต่อ…", "Connecting…"),
            ("session.host_left_lobby", "หัวหน้าห้องออกแล้ว", "The host left"),
            ("session.host_left_job", "หัวหน้าห้องออกระหว่างงาน — ได้ค่าจ้างห้องที่ผีรับแล้ว {0}", "The host left during the job — you keep the pay for accepted rooms: {0}"),
            ("session.connection_lost", "การเชื่อมต่อหลุด", "Connection lost"),
            ("session.connection_lost_job", "การเชื่อมต่อหลุด — ได้ค่าจ้างห้องที่ผีรับแล้ว {0}", "Connection lost — you keep the pay for accepted rooms: {0}"),
            ("session.host_left_results", "หัวหน้าห้องออก — ค่าจ้างงานนี้ {0}", "The host left — your pay for this job: {0}"),
            ("pause.resume", "กลับเข้าเกม", "Back to the game"),
            ("pause.leave", "ออกจากห้อง", "Leave the room"),
            ("loading.house", "กำลังเดินทางไปบ้านลูกค้า…", "On the way to the client's house…"),
            ("results.back_in", "กลับรถตู้ใน {0} วินาที (หัวหน้ากด {1} เพื่อไปเลย)", "Back to the van in {0} s (host: {1} to go now)"),
            ("lobby.title", "รถตู้ Spirit Movers", "Spirit Movers van"),
            ("lobby.code", "รหัสห้อง: {0}", "Room code: {0}"),
            ("lobby.member", "{0} — {1}", "{0} — {1}"),
            ("lobby.player_n", "ผู้เล่น {0}", "Player {0}"),
            ("menu.name_hint", "ชื่อของคุณ", "Your name"),
            ("lobby.ready", "พร้อม", "ready"),
            ("lobby.not_ready", "ยังไม่พร้อม", "not ready"),
            ("lobby.experimental", "5–6 คน: โหมดทดลอง", "5–6 players: experimental"),
            ("lobby.job", "งาน: บ้านไม้ (Greybox) · ระดับปกติ", "Job: wooden house (greybox) · normal"),
            ("lobby.hard_locked", "ระดับยาก — ปลดล็อกด้วยยศ", "Hard — unlocked by rank"),
            ("lobby.hint_ready", "กด {0} เมื่อพร้อม", "Press {0} when ready"),
            ("lobby.hint_start", "ทุกคนพร้อมแล้ว — หัวหน้ากด {0} เพื่อออกเดินทาง", "Everyone is ready — the host presses {0} to set off"),
            ("lobby.countdown", "ออกเดินทางใน {0}", "Setting off in {0}"),
            ("error.version", "เวอร์ชันไม่ตรงกับห้อง (ของคุณ {0} · ห้อง {1}) — อัปเดตเกม", "Version mismatch (yours {0} · room {1}) — please update the game"),
            ("error.code", "รหัสห้องไม่ถูกต้อง", "That room code is not valid"),
            ("error.join", "เข้าห้องไม่ได้ — ตรวจรหัสหรืออินเทอร์เน็ต", "Could not join — check the code or your internet"),
            ("error.in_job", "ห้องนี้กำลังทำงานอยู่ เข้าได้ตอนทีมกลับรถตู้", "That team is on a job; join when they are back in the van"),
            ("error.full", "ห้องเต็มแล้ว", "That room is full"),
            ("camera.use", "{0} ส่องกล้องวิญญาณ", "{0}: look through the spirit camera"),
            ("key.mouse_left", "คลิกซ้าย", "Left click"),
            ("settings.title", "ตั้งค่า", "Settings"),
            ("settings.mic", "ไมค์: {0}", "Microphone: {0}"),
            ("settings.speaker", "ลำโพง: {0}", "Speaker: {0}"),
            ("settings.device_default", "ค่าเริ่มต้นของ Windows", "Windows default"),
            ("settings.mic_help", "เพื่อนไม่ได้ยิน? Windows > Settings > Privacy & security > Microphone > เปิดให้แอปใช้ไมค์", "Friends can't hear you? Windows > Settings > Privacy & security > Microphone > let apps use it"),
            ("radio.talk", "{0} ค้างไว้เพื่อพูดทางวิทยุ", "Hold {0} to talk on the radio"),
            ("radio.on_air", "กำลังพูดทางวิทยุ", "On the radio"),
            ("voice.connecting", "กำลังเชื่อมต่อเสียง…", "Connecting voice…"),
            ("voice.lost", "เสียงหลุด — กำลังเชื่อมใหม่…", "Voice dropped — reconnecting…"),
            ("voice.unavailable", "ใช้เสียงไม่ได้ — เล่นต่อได้ ใช้ Ping/แชตแทน", "Voice is unavailable — play on with pings and chat"),
            ("settings.tab_general", "ทั่วไป", "General"),
            ("settings.tab_audio", "เสียง", "Audio"),
            ("settings.tab_keys", "ปุ่ม", "Keys"),
            ("settings.language", "ภาษา", "Language"),
            ("settings.lang_th", "ไทย", "ไทย"),
            ("settings.lang_en", "English", "English"),
            ("settings.sensitivity", "ความไวเมาส์ {0}", "Mouse sensitivity {0}"),
            ("settings.invert_y", "กลับแกนขึ้น-ลงของเมาส์", "Invert mouse up/down"),
            ("settings.fov", "มุมมองกล้อง (FOV) {0}", "Field of view {0}"),
            ("settings.vol_master", "เสียงรวม {0}%", "Master {0}%"),
            ("settings.vol_music", "เพลง {0}%", "Music {0}%"),
            ("settings.vol_sfx", "เสียงประกอบ {0}%", "Effects {0}%"),
            ("settings.vol_ambience", "เสียงบรรยากาศ {0}%", "Ambience {0}%"),
            ("settings.vol_voice", "เสียงพูดของเพื่อน {0}%", "Voice chat {0}%"),
            ("settings.vol_ui", "เสียงเมนู {0}%", "Menus {0}%"),
            ("settings.push_to_talk", "กดค้างเพื่อพูด (ปิด = พูดแล้วส่งเลย)", "Push to talk (off = open mic)"),
            ("settings.ghost_hears_mic", "ให้ผีได้ยินเสียงไมค์ของฉัน", "Let the ghost hear my mic"),
            ("settings.reset_keys", "คืนค่าปุ่มเริ่มต้น", "Reset keys"),
            ("settings.change_key", "เปลี่ยน", "Change"),
            ("settings.press_key", "กดปุ่มใหม่… (Esc ยกเลิก)", "Press a key… (Esc cancels)"),
            ("settings.conflict", "ซ้ำกับ {0}", "Same as {0}"),
            ("settings.close", "บันทึกและปิด", "Save and close"),
            ("action.move_up", "เดินหน้า", "Forward"),
            ("action.move_down", "ถอยหลัง", "Back"),
            ("action.move_left", "ไปทางซ้าย", "Left"),
            ("action.move_right", "ไปทางขวา", "Right"),
            ("action.sprint", "วิ่ง", "Sprint"),
            ("action.crouch", "ย่อตัว", "Crouch"),
            ("action.jump", "กระโดด", "Jump"),
            ("action.interact", "ยก / วาง / ใช้", "Pick up / use"),
            ("action.throw", "โยน", "Throw"),
            ("action.jobsheet", "ดูใบสั่งงาน", "Work order"),
            ("action.wai", "ไหว้ผี", "Wai"),
            ("action.use", "ใช้ของในมือ", "Use held item"),
            ("action.ready", "พร้อม (ในรถตู้)", "Ready (van)"),
            ("action.confirm", "ยืนยัน / ออกเดินทาง", "Confirm / set off"),
            ("action.push_to_talk", "กดพูด", "Push to talk"),
            ("key.mouse_right", "คลิกขวา", "Right click"),
            ("key.mouse_middle", "คลิกกลาง", "Middle click"),
            ("camera.empty", "ฟิล์มหมด", "Out of film"),
            ("cabinet.locked", "ตู้ล็อก — จัดของในห้องนี้ให้ถูกอีก 1 ชิ้น หรือส่องกล้อง", "Locked: put one more item right in this room, or use the camera"),
            ("job.sheet.title", "ใบสั่งงาน", "Work order"),
            ("job.accepted", "ผีรับแล้ว", "Accepted"),
            ("room.west", "ห้องนั่งเล่น", "Living room"),
            ("room.east", "ห้องครัว", "Kitchen"),
            ("room.loft", "ห้องนอนชั้นลอย", "Loft bedroom"),
            ("ghost.mood.0", "ผี: สงบ", "Ghost: calm"),
            ("ghost.mood.1", "ผี: หงุดหงิด", "Ghost: annoyed"),
            ("ghost.mood.2", "ผี: โมโห", "Ghost: angry"),
            ("ghost.mood.3", "ผี: อาละวาด", "Ghost: raging"),
            ("ghost.mood.4", "ผี: ไล่ออก", "Ghost: evicting"),
            ("results.title", "สรุปงาน", "Job report"),
            ("results.full", "ส่งผีสำเร็จ", "Ghost at peace"),
            ("results.early", "ปิดงานก่อนเสร็จ", "Left early"),
            ("results.evicted", "ถูกไล่ออก", "Evicted"),
            ("results.rooms", "ห้องที่ผีรับ {0}/{1}", "Rooms accepted {0}/{1}"),
            ("results.pay", "ค่าจ้างต่อคน {0}", "Pay each {0}"),
            ("results.stars", "รีวิว {0} ดาว", "{0}-star review"),
        };

        [MenuItem("Spirit Movers/Localization/Create Settings and UI Table")]
        public static void Setup()
        {
            Directory.CreateDirectory(Root);
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Localization Settings";
                AssetDatabase.CreateAsset(settings, $"{Root}/Localization Settings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            foreach (var code in LocaleCodes)
            {
                if (LocalizationEditorSettings.GetLocale(code) != null)
                    continue;
                var locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, $"{Root}/{locale.name} ({code}).asset");
                LocalizationEditorSettings.AddLocale(locale);
            }

            var collection = LocalizationEditorSettings.GetStringTableCollection(UiTable) ??
                             LocalizationEditorSettings.CreateStringTableCollection(UiTable, $"{Root}/Tables");
            var th = (StringTable)collection.GetTable("th");
            var en = (StringTable)collection.GetTable("en");
            foreach (var (key, thText, enText) in s_Seed)
            {
                // The seed is the source of the text: a changed line here updates the table (M4B).
                SetEntry(th, key, thText);
                SetEntry(en, key, enText);
            }
            EditorUtility.SetDirty(th);
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(collection.SharedData);

            // Localization loads tables through Addressables, so every player build must build them too.
            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Localization] locales={string.Join(",", LocaleCodes)} table={UiTable} entries={collection.SharedData.Entries.Count}");
        }

        private static void SetEntry(StringTable table, string key, string text)
        {
            var entry = table.GetEntry(key);
            if (entry == null)
                table.AddEntry(key, text);
            else
                entry.Value = text;
        }
    }
}
