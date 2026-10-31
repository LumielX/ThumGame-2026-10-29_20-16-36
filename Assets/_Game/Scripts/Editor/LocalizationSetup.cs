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

        // Seed entries; the tables are the source of truth once created.
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
            ("menu.connecting", "กำลังเชื่อมต่อ…", "Connecting…"),
            ("session.host_left_lobby", "หัวหน้าห้องออกแล้ว", "The host left"),
            ("session.host_left_job", "หัวหน้าห้องออกระหว่างงาน — ได้ค่าจ้างห้องที่ผีรับแล้ว {0}", "The host left during the job — you keep the pay for accepted rooms: {0}"),
            ("session.host_left_results", "หัวหน้าห้องออก — ค่าจ้างงานนี้ {0}", "The host left — your pay for this job: {0}"),
            ("pause.resume", "กลับเข้าเกม", "Back to the game"),
            ("pause.leave", "ออกจากห้อง", "Leave the room"),
            ("loading.house", "กำลังเดินทางไปบ้านลูกค้า…", "On the way to the client's house…"),
            ("results.back_in", "กลับรถตู้ใน {0} วินาที (หัวหน้ากด Enter เพื่อไปเลย)", "Back to the van in {0} s (host: Enter to go now)"),
            ("lobby.title", "รถตู้ Spirit Movers", "Spirit Movers van"),
            ("lobby.code", "รหัสห้อง: {0}", "Room code: {0}"),
            ("lobby.member", "ผู้เล่น {0} — {1}", "Player {0} — {1}"),
            ("lobby.ready", "พร้อม", "ready"),
            ("lobby.not_ready", "ยังไม่พร้อม", "not ready"),
            ("lobby.experimental", "5–6 คน: โหมดทดลอง", "5–6 players: experimental"),
            ("lobby.job", "งาน: บ้านไม้ (Greybox) · ระดับปกติ", "Job: wooden house (greybox) · normal"),
            ("lobby.hard_locked", "ระดับยาก — ปลดล็อกด้วยยศ", "Hard — unlocked by rank"),
            ("lobby.hint_ready", "กด R เมื่อพร้อม", "Press R when ready"),
            ("lobby.hint_start", "ทุกคนพร้อมแล้ว — หัวหน้ากด Enter เพื่อออกเดินทาง", "Everyone is ready — the host presses Enter to set off"),
            ("lobby.countdown", "ออกเดินทางใน {0}", "Setting off in {0}"),
            ("error.version", "เวอร์ชันไม่ตรงกับห้อง (ของคุณ {0} · ห้อง {1}) — อัปเดตเกม", "Version mismatch (yours {0} · room {1}) — please update the game"),
            ("error.code", "รหัสห้องไม่ถูกต้อง", "That room code is not valid"),
            ("error.join", "เข้าห้องไม่ได้ — ตรวจรหัสหรืออินเทอร์เน็ต", "Could not join — check the code or your internet"),
            ("error.in_job", "ห้องนี้กำลังทำงานอยู่ เข้าได้ตอนทีมกลับรถตู้", "That team is on a job; join when they are back in the van"),
            ("error.full", "ห้องเต็มแล้ว", "That room is full"),
            ("camera.use", "คลิกขวา ส่องกล้องวิญญาณ", "Right-click to look through the spirit camera"),
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
                if (th.GetEntry(key) == null) th.AddEntry(key, thText);
                if (en.GetEntry(key) == null) en.AddEntry(key, enText);
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
    }
}
