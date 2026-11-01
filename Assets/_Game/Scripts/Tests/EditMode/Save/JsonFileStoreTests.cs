using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using UnityEngine;

namespace SpiritMovers.Tests
{
    public class JsonFileStoreTests
    {
        /// <summary>The same JsonUtility codec the game uses, kept here so Core stays engine-free.</summary>
        private sealed class Codec : IJsonCodec
        {
            [Serializable]
            private sealed class SchemaProbe
            {
                public int schema = -1;
            }

            public string ToJson(object value) => JsonUtility.ToJson(value, true);
            public T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);

            public int ReadSchema(string json)
            {
                try
                {
                    return JsonUtility.FromJson<SchemaProbe>(json)?.schema ?? -1;
                }
                catch (ArgumentException)
                {
                    return -1;
                }
            }
        }

        private static readonly Codec s_Codec = new();
        private string m_Dir;

        private string SavePath(int schema) => Path.Combine(m_Dir, $"save_v{schema}.json");

        private JsonFileStore<SaveData> SaveStore(int current = 1, IReadOnlyDictionary<int, Func<string, string>> migrations = null) =>
            new(m_Dir, n => $"save_v{n}.json", current, s_Codec, migrations);

        private JsonFileStore<SettingsData> SettingsStore() => new(m_Dir, _ => "settings.json", SettingsData.CurrentSchema, s_Codec);

        [SetUp]
        public void CreateFolder()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "SpiritMoversStoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Dir);
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(m_Dir))
                Directory.Delete(m_Dir, true);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            SaveStore().Save(new SaveData { money = 120, playerName = "ป้าแจ่ม" });
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(120, value.money);
            Assert.AreEqual("ป้าแจ่ม", value.playerName);
        }

        [Test]
        public void Load_MissingIsFresh()
        {
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.Fresh, outcome);
            Assert.AreEqual(0, value.money);
        }

        [Test]
        public void Save_KeepsOneBackup()
        {
            var store = SaveStore();
            store.Save(new SaveData { money = 1 });
            store.Save(new SaveData { money = 2 });
            Assert.AreEqual(2, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1))).money);
            Assert.AreEqual(1, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1) + ".bak")).money);
            Assert.IsFalse(File.Exists(SavePath(1) + ".tmp"));
        }

        [Test]
        public void Load_CorruptUsesBackup()
        {
            var store = SaveStore();
            store.Save(new SaveData { money = 7 });
            store.Save(new SaveData { money = 9 });
            File.WriteAllText(SavePath(1), "{\"schema\":1,\"money\":5");
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.RestoredFromBackup, outcome);
            Assert.AreEqual(7, value.money);
            Assert.AreEqual(7, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1))).money, "the good copy is written back");
            Assert.AreEqual(7, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1) + ".bak")).money, "the backup stays good");
        }

        [Test]
        public void Load_EmptyFileUsesBackup()
        {
            var store = SaveStore();
            store.Save(new SaveData { money = 3 });
            store.Save(new SaveData { money = 4 });
            File.WriteAllText(SavePath(1), "");
            Assert.AreEqual(LoadOutcome.RestoredFromBackup, SaveStore().Load().Outcome);
        }

        [Test]
        public void Load_BothCorruptResetsAndKeepsEvidence()
        {
            File.WriteAllText(SavePath(1), "not json");
            File.WriteAllText(SavePath(1) + ".bak", "{\"money\":");
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.ResetCorrupt, outcome);
            Assert.AreEqual(0, value.money);
            Assert.AreEqual("not json", File.ReadAllText(SavePath(1) + ".corrupt"));
        }

        [Test]
        public void Load_LeftoverTmpIgnored()
        {
            SaveStore().Save(new SaveData { money = 11 });
            File.WriteAllText(SavePath(1) + ".tmp", "{\"schema\":1,\"mon");
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(11, value.money);
        }

        [Test]
        public void Load_LockedFileIsLeftAlone()
        {
            // Final review: a good file that only cannot be read right now (antivirus, another copy of the game) must
            // not be "repaired" from the older backup.
            var store = SaveStore();
            store.Save(new SaveData { money = 1 });
            store.Save(new SaveData { money = 2 });
            var before = File.ReadAllText(SavePath(1));
            JsonFileStore<SaveData> locked;
            (SaveData Value, LoadOutcome Outcome) loaded;
            using (new FileStream(SavePath(1), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                locked = SaveStore();
                loaded = locked.Load();
            }
            Assert.AreEqual(LoadOutcome.Unreadable, loaded.Outcome);
            Assert.IsTrue(locked.ReadOnly, "nothing is written over a file we could not read");
            locked.Save(new SaveData { money = 99 });
            Assert.AreEqual(before, File.ReadAllText(SavePath(1)));
            Assert.AreEqual(1, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1) + ".bak")).money);
        }

        [Test]
        public void Load_DamagedButUnwritable_DoesNotThrow()
        {
            var store = SaveStore();
            store.Save(new SaveData { money = 7 });
            store.Save(new SaveData { money = 9 });
            File.WriteAllText(SavePath(1), "{\"schema\":1,\"mon");
            JsonFileStore<SaveData> blocked;
            (SaveData Value, LoadOutcome Outcome) loaded;
            // Readable but not replaceable (another program holds it open): the repair cannot be written.
            using (new FileStream(SavePath(1), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                blocked = SaveStore();
                loaded = blocked.Load();
            }
            Assert.AreEqual(7, loaded.Value.money, "the backup's money is still shown");
            Assert.AreEqual(LoadOutcome.Unreadable, loaded.Outcome);
            Assert.IsTrue(blocked.ReadOnly);
            Assert.AreEqual(7, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1) + ".bak")).money, "the backup is untouched");
        }

        [Test]
        public void Load_MissingMainUsesBackup()
        {
            // Final review: a replace that failed half way leaves only save_v1.json.bak; starting fresh would later
            // write over that last copy.
            var store = SaveStore();
            store.Save(new SaveData { money = 5 });
            store.Save(new SaveData { money = 6 });
            File.Delete(SavePath(1));
            var (value, outcome) = SaveStore().Load();
            Assert.AreEqual(LoadOutcome.RestoredFromBackup, outcome);
            Assert.AreEqual(5, value.money);
            Assert.IsTrue(File.Exists(SavePath(1)));
            Assert.AreEqual(5, s_Codec.FromJson<SaveData>(File.ReadAllText(SavePath(1) + ".bak")).money);
        }

        [Test]
        public void Load_MigratesOlderFileStepByStep()
        {
            File.WriteAllText(SavePath(1), s_Codec.ToJson(new SaveData { schema = 1, money = 5 }));
            string Step(string json, int to, int add)
            {
                var data = s_Codec.FromJson<SaveData>(json);
                data.schema = to;
                data.money += add;
                return s_Codec.ToJson(data);
            }
            var migrations = new Dictionary<int, Func<string, string>>
            {
                [1] = json => Step(json, 2, 10),
                [2] = json => Step(json, 3, 100),
            };
            var (value, outcome) = SaveStore(3, migrations).Load();
            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(115, value.money);
            Assert.AreEqual(3, value.schema);
            Assert.IsTrue(File.Exists(SavePath(3)), "written in the new version");
            Assert.IsTrue(File.Exists(SavePath(1)), "the old file is kept");
        }

        [Test]
        public void Load_NewerVersionIsNotOverwritten()
        {
            var newer = "{\"schema\":2,\"money\":999,\"medals\":3}";
            File.WriteAllText(SavePath(2), newer);
            var store = SaveStore();
            var (value, outcome) = store.Load();
            Assert.AreEqual(LoadOutcome.NewerVersion, outcome);
            Assert.IsTrue(store.ReadOnly);
            Assert.AreEqual(0, value.money);
            store.Save(new SaveData { money = 1 });
            Assert.AreEqual(newer, File.ReadAllText(SavePath(2)));
            Assert.IsFalse(File.Exists(SavePath(1)), "nothing written next to it either");
        }

        [Test]
        public void Settings_NewerSchemaInsideFile_ReadOnly()
        {
            var path = Path.Combine(m_Dir, "settings.json");
            var newer = "{\"schema\":9,\"fov\":90}";
            File.WriteAllText(path, newer);
            var store = SettingsStore();
            var (value, outcome) = store.Load();
            Assert.AreEqual(LoadOutcome.NewerVersion, outcome);
            Assert.AreEqual(75f, value.fov);
            store.Save(new SettingsData());
            Assert.AreEqual(newer, File.ReadAllText(path));
        }
    }
}
