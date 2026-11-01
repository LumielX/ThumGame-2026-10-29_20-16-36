using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpiritMovers.Core.Save
{
    /// <summary>
    /// One JSON file on disk (TECHNICAL_DESIGN 9): written atomically (temp file, then replace) with one backup kept,
    /// a damaged file falls back to the backup, older versions are migrated step by step, and a file from a newer
    /// game is never written over.
    /// </summary>
    public sealed class JsonFileStore<T> where T : class, new()
    {
        /// <summary>How far past the current version to look for a newer game's file.</summary>
        private const int NewerVersionsChecked = 16;

        private readonly string m_Directory;
        private readonly Func<int, string> m_FileName;
        private readonly int m_CurrentSchema;
        private readonly IJsonCodec m_Codec;
        private readonly IReadOnlyDictionary<int, Func<string, string>> m_Migrations;

        /// <param name="fileName">The file for a schema version: "save_v{n}.json", or always "settings.json".</param>
        /// <param name="migrations">Step n turns a version-n document into a version n+1 document.</param>
        public JsonFileStore(string directory, Func<int, string> fileName, int currentSchema, IJsonCodec codec,
            IReadOnlyDictionary<int, Func<string, string>> migrations = null)
        {
            m_Directory = directory;
            m_FileName = fileName;
            m_CurrentSchema = currentSchema;
            m_Codec = codec;
            m_Migrations = migrations ?? new Dictionary<int, Func<string, string>>();
        }

        /// <summary>A newer game's file was found: <see cref="Save"/> does nothing so that file survives.</summary>
        public bool ReadOnly { get; private set; }

        public string FilePath => PathFor(m_CurrentSchema);

        public (T Value, LoadOutcome Outcome) Load()
        {
            ReadOnly = false;
            for (var n = m_CurrentSchema + 1; n <= m_CurrentSchema + NewerVersionsChecked; n++)
            {
                if (PathFor(n) != FilePath && File.Exists(PathFor(n)))
                    return Newer();
            }

            if (File.Exists(FilePath))
            {
                var read = TryRead(FilePath, out var value);
                if (read == Read.Newer)
                    return Newer();
                if (read == Read.Ok)
                    return (value, LoadOutcome.Loaded);
                // Locked or not permitted is not damaged: never "repair" a file we could not even read.
                if (read == Read.Unreadable)
                    return Blocked(new T());
                if (TryRead(FilePath + ".bak", out var backup) == Read.Ok)
                    return Restore(backup);
                try
                {
                    var evidence = FilePath + ".corrupt";
                    if (File.Exists(evidence))
                        File.Delete(evidence);
                    File.Move(FilePath, evidence);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    return Blocked(new T());
                }
                return (new T(), LoadOutcome.ResetCorrupt);
            }

            // A replace that failed half way can leave only the backup: it is the player's last copy.
            if (File.Exists(FilePath + ".bak") && TryRead(FilePath + ".bak", out var lone) == Read.Ok)
                return Restore(lone);

            for (var n = m_CurrentSchema - 1; n >= 0; n--)
            {
                if (PathFor(n) == FilePath || !File.Exists(PathFor(n)))
                    continue;
                if (TryRead(PathFor(n), out var migrated) != Read.Ok)
                    break;
                return TryWrite(m_Codec.ToJson(migrated), keepBackup: true) ? (migrated, LoadOutcome.Loaded) : Blocked(migrated);
            }
            return (new T(), LoadOutcome.Fresh);
        }

        private (T, LoadOutcome) Restore(T backup) =>
            TryWrite(m_Codec.ToJson(backup), keepBackup: false) ? (backup, LoadOutcome.RestoredFromBackup) : Blocked(backup);

        /// <summary>Play on with what could be read, writing nothing this session.</summary>
        private (T, LoadOutcome) Blocked(T value)
        {
            ReadOnly = true;
            return (value, LoadOutcome.Unreadable);
        }

        private bool TryWrite(string json, bool keepBackup)
        {
            try
            {
                WriteAtomic(json, keepBackup);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void Save(T value)
        {
            if (!ReadOnly)
                WriteAtomic(m_Codec.ToJson(value), keepBackup: true);
        }

        private (T, LoadOutcome) Newer()
        {
            ReadOnly = true;
            return (new T(), LoadOutcome.NewerVersion);
        }

        private string PathFor(int schema) => Path.Combine(m_Directory, m_FileName(schema));

        private enum Read { Ok, Damaged, Newer, Unreadable }

        /// <summary>Reads and, for an older schema, migrates a file: a parse problem is damage, an IO problem is not.</summary>
        private Read TryRead(string path, out T value)
        {
            value = null;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return Read.Damaged;
                var schema = m_Codec.ReadSchema(json);
                if (schema < 0)
                    return Read.Damaged;
                if (schema > m_CurrentSchema)
                    return Read.Newer;
                for (; schema < m_CurrentSchema; schema++)
                {
                    if (!m_Migrations.TryGetValue(schema, out var step))
                        return Read.Damaged;
                    json = step(json);
                }
                if (m_Codec.ReadSchema(json) != m_CurrentSchema)
                    return Read.Damaged;
                value = m_Codec.FromJson<T>(json);
                return value == null ? Read.Damaged : Read.Ok;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return Read.Unreadable;
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidOperationException)
            {
                return Read.Damaged;
            }
        }

        /// <summary>Temp file first, flushed to disk, then swapped in — a crash mid-write leaves the old file whole.</summary>
        private void WriteAtomic(string json, bool keepBackup)
        {
            Directory.CreateDirectory(m_Directory);
            var temp = FilePath + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(FilePath))
                File.Replace(temp, FilePath, keepBackup ? FilePath + ".bak" : null);
            else
                File.Move(temp, FilePath);
        }
    }
}
