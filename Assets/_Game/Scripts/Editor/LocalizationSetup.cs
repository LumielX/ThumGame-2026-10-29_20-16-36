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
