using System;
using System.IO;
using SpiritMovers.Core.Save;
using UnityEngine;

namespace SpiritMovers.Save
{
    /// <summary>
    /// The player's save and settings for this run of the game (TECHNICAL_DESIGN 9): loaded once by Bootstrap, saved
    /// as soon as something worth keeping changes. Before Load (test scenes without Boot) nothing is read or written.
    /// </summary>
    public static class GameData
    {
        private static JsonFileStore<SaveData> s_SaveStore;
        private static JsonFileStore<SettingsData> s_SettingsStore;
        private static object s_PaidJob;

        public static bool IsLoaded { get; private set; }
        public static SaveData Save { get; private set; } = new();
        public static SettingsData Settings { get; private set; } = new();
        public static int Money => Save.money;

        /// <summary>
        /// What the main menu should tell the player about the files (save.restored, save.reset, save.newer) or a save
        /// that failed later (save.failed); the menu clears it once shown.
        /// </summary>
        public static string MessageKey { get; set; }

        /// <summary>Money, name or settings changed.</summary>
        public static event Action Changed;

        public static void Load()
        {
            var root = DataLocation.Root;
            var codec = new UnityJsonCodec();
            s_SaveStore = new JsonFileStore<SaveData>(root, n => $"save_v{n}.json", SaveData.CurrentSchema, codec);
            s_SettingsStore = new JsonFileStore<SettingsData>(root, _ => "settings.json", SettingsData.CurrentSchema, codec);

            var (save, saveOutcome) = s_SaveStore.Load();
            var (settings, settingsOutcome) = s_SettingsStore.Load();
            Save = save.Sanitized();
            Settings = settings.Sanitized();
            s_PaidJob = null;
            IsLoaded = true;
            MessageKey = MessageFor(saveOutcome) ?? MessageFor(settingsOutcome);
            if (Save.nameSeed == 0)
            {
                Save.nameSeed = UnityEngine.Random.Range(1, int.MaxValue);
                SaveNow();
            }
            Debug.Log($"[Save] loaded from {root}: save {saveOutcome}, settings {settingsOutcome}, money {Money}");
            Changed?.Invoke();
        }

        /// <summary>Tests: forget the loaded data so scenes without Boot read and write nothing.</summary>
        internal static void Unload()
        {
            IsLoaded = false;
            s_SaveStore = null;
            s_SettingsStore = null;
            s_PaidJob = null;
            Save = new SaveData();
            Settings = new SettingsData();
            MessageKey = null;
        }

        /// <summary>
        /// Add a job's pay once: the results screen and a host leaving during that job's results name the same
        /// <paramref name="job"/>, so the pay is never counted twice.
        /// </summary>
        public static void CreditPay(int amount, object job)
        {
            if (!IsLoaded || job == null || ReferenceEquals(job, s_PaidJob))
                return;
            s_PaidJob = job;
            Save.Credit(amount);
            Debug.Log($"[Save] pay +{amount}, money {Money}");
            SaveNow();
            Changed?.Invoke();
        }

        public static void SaveNow() => Write(() => s_SaveStore.Save(Save));

        public static void SaveSettingsNow() => Write(() => s_SettingsStore.Save(Settings));

        private static void Write(Action write)
        {
            if (!IsLoaded)
                return;
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Save] could not write to {DataLocation.Root}: {e.Message}");
                MessageKey = "save.failed";
                Changed?.Invoke();
            }
        }

        private static string MessageFor(LoadOutcome outcome) => outcome switch
        {
            LoadOutcome.RestoredFromBackup => "save.restored",
            LoadOutcome.ResetCorrupt => "save.reset",
            LoadOutcome.NewerVersion => "save.newer",
            _ => null,
        };
    }
}
