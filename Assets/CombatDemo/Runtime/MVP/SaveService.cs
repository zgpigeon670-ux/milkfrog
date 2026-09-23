using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    [Serializable]
    public sealed class PlayerSnapshot
    {
        public int version = 1;
        public string levelId = "MVP_TestLevel";
        public Vector3 position;
        public float yaw;
        public float health = 100;
        public float posture;
        public string[] defeatedEnemyIds = Array.Empty<string>();
        public bool bossDefeated;
    }

    public sealed class SaveService
    {
        private const int CurrentVersion = 1;
        private const string CurrentLevelId = "MVP_TestLevel";
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";

        private readonly string _savePath;
        private readonly string _backupPath;

        public SaveService(string directory = null)
        {
            string targetDirectory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Application.persistentDataPath, "MilkfrogMVP")
                : directory;

            DirectoryPath = Path.GetFullPath(targetDirectory);
            _savePath = Path.Combine(DirectoryPath, SaveFileName);
            _backupPath = Path.Combine(DirectoryPath, BackupFileName);
        }

        public string LastMessage { get; private set; }

        public string DirectoryPath { get; private set; }

        public bool HasSave
        {
            get { return File.Exists(_savePath) || File.Exists(_backupPath); }
        }

        public bool TryLoad(out PlayerSnapshot snapshot)
        {
            snapshot = null;

            SnapshotReadResult primary = ReadSnapshot(_savePath);
            if (primary.status == SnapshotReadStatus.Valid)
            {
                snapshot = primary.snapshot;
                LastMessage = "Loaded save.";
                return true;
            }

            SnapshotReadResult backup = ReadSnapshot(_backupPath);
            if (backup.status == SnapshotReadStatus.Valid)
            {
                snapshot = backup.snapshot;
                LastMessage = "Recovered backup save.";
                return true;
            }

            if (backup.status == SnapshotReadStatus.Unreadable)
            {
                LastMessage = backup.message;
                return false;
            }

            if (primary.status == SnapshotReadStatus.Missing && backup.status == SnapshotReadStatus.Missing)
            {
                LastMessage = "No save found.";
                return false;
            }

            if (backup.status == SnapshotReadStatus.Unreadable)
                LastMessage = backup.message;
            else if (primary.status == SnapshotReadStatus.Unreadable || primary.status == SnapshotReadStatus.Invalid)
                LastMessage = primary.message;
            else
                LastMessage = backup.message;
            return false;
        }

        public bool TrySave(PlayerSnapshot snapshot)
        {
            string saveTempPath = null;
            string backupTempPath = null;

            string validationMessage;
            if (!IsValid(snapshot, out validationMessage))
            {
                LastMessage = validationMessage;
                return false;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);

                SnapshotReadResult current = ReadSnapshot(_savePath);
                if (current.status == SnapshotReadStatus.Unreadable)
                {
                    LastMessage = current.message;
                    return false;
                }

                string json = JsonUtility.ToJson(snapshot, true);
                saveTempPath = NewTemporaryPath(SaveFileName);
                File.WriteAllText(saveTempPath, json, new UTF8Encoding(false));

                if (current.status == SnapshotReadStatus.Valid)
                {
                    backupTempPath = NewTemporaryPath(BackupFileName);
                    File.Copy(_savePath, backupTempPath);
                    CommitTemporaryFile(backupTempPath, _backupPath, null);
                    backupTempPath = null;

                    CommitTemporaryFile(saveTempPath, _savePath, null);
                    saveTempPath = null;
                }
                else if (current.status == SnapshotReadStatus.Invalid)
                {
                    string archivePath = NewCorruptArchivePath();
                    CommitTemporaryFile(saveTempPath, _savePath, archivePath);
                    saveTempPath = null;
                }
                else
                {
                    CommitTemporaryFile(saveTempPath, _savePath, null);
                    saveTempPath = null;
                }

                LastMessage = "Saved.";
                return true;
            }
            catch (Exception)
            {
                LastMessage = "Could not write save.";
                return false;
            }
            finally
            {
                DeleteTemporaryFile(saveTempPath);
                DeleteTemporaryFile(backupTempPath);
            }
        }

        private static SnapshotReadResult ReadSnapshot(string path)
        {
            if (!File.Exists(path))
                return SnapshotReadResult.Missing();

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                return SnapshotReadResult.Unreadable("Could not read save.");
            }

            if (!HasRequiredFields(json))
                return SnapshotReadResult.Invalid("Save data is invalid.");

            PlayerSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<PlayerSnapshot>(json);
            }
            catch (Exception)
            {
                return SnapshotReadResult.Invalid("Save data is invalid.");
            }

            string validationMessage;
            if (!IsValid(snapshot, out validationMessage))
                return SnapshotReadResult.Invalid(validationMessage);

            if (snapshot.defeatedEnemyIds == null)
                snapshot.defeatedEnemyIds = Array.Empty<string>();

            return SnapshotReadResult.Valid(snapshot);
        }

        private static bool HasRequiredFields(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != '{')
                return false;

            bool[] found = new bool[8];
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}')
                    break;

                string key;
                if (!TryReadUnescapedString(json, ref index, out key))
                    return false;

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index++] != ':')
                    return false;

                int fieldIndex = RequiredFieldIndex(key);
                if (fieldIndex >= 0)
                    found[fieldIndex] = true;

                if (!SkipJsonValue(json, ref index))
                    return false;

                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                    return false;

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                    break;

                return false;
            }

            return found[0] && found[1] && found[2] && found[3] &&
                   found[4] && found[5] && found[6] && found[7];
        }

        private static int RequiredFieldIndex(string key)
        {
            switch (key)
            {
                case "version": return 0;
                case "levelId": return 1;
                case "position": return 2;
                case "yaw": return 3;
                case "health": return 4;
                case "posture": return 5;
                case "defeatedEnemyIds": return 6;
                case "bossDefeated": return 7;
                default: return -1;
            }
        }

        private static bool TryReadUnescapedString(string json, ref int index, out string value)
        {
            value = null;
            if (index >= json.Length || json[index++] != '"')
                return false;

            int start = index;
            bool escaped = false;
            while (index < json.Length)
            {
                char character = json[index++];
                if (character == '\\')
                {
                    escaped = true;
                    if (index >= json.Length)
                        return false;
                    index++;
                }
                else if (character == '"')
                {
                    if (!escaped)
                        value = json.Substring(start, index - start - 1);
                    return true;
                }
            }

            return false;
        }

        private static bool SkipJsonValue(string json, ref int index)
        {
            int nesting = 0;
            bool inString = false;
            bool escaped = false;

            while (index < json.Length)
            {
                char character = json[index];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        inString = false;
                }
                else if (character == '"')
                    inString = true;
                else if (character == '{' || character == '[')
                    nesting++;
                else if (character == '}' || character == ']')
                {
                    if (nesting == 0)
                        return true;
                    nesting--;
                }
                else if (character == ',' && nesting == 0)
                    return true;

                index++;
            }

            return false;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static bool IsValid(PlayerSnapshot snapshot, out string message)
        {
            if (snapshot == null)
            {
                message = "Save data is invalid.";
                return false;
            }

            if (snapshot.version != CurrentVersion)
            {
                message = "Unsupported save version.";
                return false;
            }

            if (!string.Equals(snapshot.levelId, CurrentLevelId, StringComparison.Ordinal))
            {
                message = "Save level does not match.";
                return false;
            }

            if (!IsFinite(snapshot.position.x) || !IsFinite(snapshot.position.y) ||
                !IsFinite(snapshot.position.z) || !IsFinite(snapshot.yaw) ||
                !IsFinite(snapshot.health) || !IsFinite(snapshot.posture) ||
                snapshot.health <= 0 || snapshot.posture < 0)
            {
                message = "Save values are invalid.";
                return false;
            }

            message = null;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private string NewTemporaryPath(string fileName)
        {
            return Path.Combine(DirectoryPath, fileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        }

        private string NewCorruptArchivePath()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string path;
            do
            {
                path = Path.Combine(DirectoryPath,
                    "save.corrupt." + timestamp + "." + Guid.NewGuid().ToString("N") + ".json");
            }
            while (File.Exists(path));

            return path;
        }

        private static void CommitTemporaryFile(string temporaryPath, string destinationPath, string backupPath)
        {
            if (File.Exists(destinationPath))
                File.Replace(temporaryPath, destinationPath, backupPath);
            else
                File.Move(temporaryPath, destinationPath);
        }

        private static void DeleteTemporaryFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // Cleanup is best effort; the save result reflects the write operation.
            }
        }

        private enum SnapshotReadStatus
        {
            Missing,
            Invalid,
            Unreadable,
            Valid
        }

        private struct SnapshotReadResult
        {
            public SnapshotReadStatus status;
            public PlayerSnapshot snapshot;
            public string message;

            public static SnapshotReadResult Missing()
            {
                return new SnapshotReadResult { status = SnapshotReadStatus.Missing };
            }

            public static SnapshotReadResult Invalid(string message)
            {
                return new SnapshotReadResult { status = SnapshotReadStatus.Invalid, message = message };
            }

            public static SnapshotReadResult Unreadable(string message)
            {
                return new SnapshotReadResult { status = SnapshotReadStatus.Unreadable, message = message };
            }

            public static SnapshotReadResult Valid(PlayerSnapshot snapshot)
            {
                return new SnapshotReadResult { status = SnapshotReadStatus.Valid, snapshot = snapshot };
            }
        }
    }
}
