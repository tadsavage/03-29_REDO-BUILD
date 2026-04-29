using System;
using System.IO;
using UnityEngine;

namespace SaveLoadSystem
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        public const int MAX_SLOTS = 8;

        // --- Events: hook your toast / SFX to these ---
        public event Action<int> OnSaveCompleted;
        public event Action<int> OnLoadCompleted;
        public event Action<int> OnSlotDeleted;

        [Header("References")]
        [SerializeField] private SaveThumbnailCapture thumbnailCapture;
        [SerializeField] private PlacementSystem placementSystem;

        private SaveMetadataCollection metadataCollection;
        private string saveFolderPath;
        private string metadataFilePath;

        // ========== LIFECYCLE ==========

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(saveFolderPath))
                Directory.CreateDirectory(saveFolderPath);

            metadataFilePath = Path.Combine(saveFolderPath, "metadata.json");
            LoadMetadataFromDisk();
        }

        // ========== PUBLIC API ==========

        public SaveMetadata[] GetAllMetadata() => metadataCollection.slots;

        public SaveMetadata GetSlotMetadata(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return null;
            return metadataCollection.slots[slotIndex];
        }

        /// <summary>
        /// Saves game state + thumbnail to the given slot.
        /// Does NOT interfere with quicksave (F5/F9).
        /// </summary>
        public void SaveToSlot(int slotIndex, string saveName)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS)
            {
                Debug.LogError($"[SaveManager] Invalid slot: {slotIndex}");
                return;
            }

            // 1. Serialize game data (synchronous — fine to do now)
            string gameDataJson = SerializeGameState();
            string dataFileName = $"slot_{slotIndex}_data.json";
            File.WriteAllText(Path.Combine(saveFolderPath, dataFileName), gameDataJson);

            // 2. Capture thumbnail (async — writes PNG at end of frame)
            string thumbFileName = $"slot_{slotIndex}_thumb.png";
            thumbnailCapture.CaptureThumbnail(saveFolderPath, thumbFileName, (tex) =>
            {
                if (tex != null) Destroy(tex);

                // 3. Write metadata AFTER thumbnail is on disk
                DateTime now = DateTime.Now;
                SaveMetadata metadata = new SaveMetadata
                {
                    slotIndex = slotIndex,
                    saveName = string.IsNullOrWhiteSpace(saveName)
                                           ? $"Save {slotIndex + 1}" : saveName,
                    timestamp = now.ToString("MMM dd, yyyy  h:mm tt").ToUpper(),
                    timestampTicks = now.Ticks,
                    thumbnailFileName = thumbFileName,
                    gameDataFileName = dataFileName
                };

                metadataCollection.slots[slotIndex] = metadata;
                WriteMetadataToDisk();

                // 4. NOW fire the event — thumbnail + metadata both ready
                OnSaveCompleted?.Invoke(slotIndex);
                // 5. Bonus: show a toast (hook your own toast to the event if you want)
                UIToast.Show($"Saved to Slot {slotIndex + 1}");
            });
        }

        /// <summary>
        /// Loads game state from slot. Returns false if empty/missing.
        /// Does NOT interfere with quicksave (F5/F9).
        /// </summary>
        public bool LoadFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;

            SaveMetadata metadata = metadataCollection.slots[slotIndex];
            if (metadata == null) return false;

            string dataFilePath = Path.Combine(saveFolderPath, metadata.gameDataFileName);
            if (!File.Exists(dataFilePath))
            {
                Debug.LogError($"[SaveManager] File missing: {dataFilePath}");
                return false;
            }

            string gameDataJson = File.ReadAllText(dataFilePath);
            DeserializeGameState(gameDataJson);

            Debug.Log($"[SaveManager] Loaded slot {slotIndex}: \"{metadata.saveName}\"");
            OnLoadCompleted?.Invoke(slotIndex);
            UIToast.Show($"Loaded Slot {slotIndex + 1}");
            return true;
        }

        public void DeleteSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return;
            SaveMetadata metadata = metadataCollection.slots[slotIndex];
            if (metadata == null) return;

            string dataPath = Path.Combine(saveFolderPath, metadata.gameDataFileName);
            string thumbPath = Path.Combine(saveFolderPath, metadata.thumbnailFileName);
            if (File.Exists(dataPath)) File.Delete(dataPath);
            if (File.Exists(thumbPath)) File.Delete(thumbPath);

            metadataCollection.slots[slotIndex] = null;
            WriteMetadataToDisk();

            Debug.Log($"[SaveManager] Deleted slot {slotIndex}");
            OnSlotDeleted?.Invoke(slotIndex);
            UIToast.Show($"Slot {slotIndex + 1} deleted");
        }

        public string GetThumbnailPath(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return null;
            SaveMetadata metadata = metadataCollection.slots[slotIndex];
            if (metadata == null) return null;
            if (string.IsNullOrEmpty(metadata.thumbnailFileName)) return null;
            return Path.Combine(saveFolderPath, metadata.thumbnailFileName);
        }

        // ========== SERIALIZATION BRIDGE ==========
        // Wire these to your PlacementSystem. Quicksave uses the
        // same PlacementSystem methods independently.

        private string SerializeGameState()
        {
            return placementSystem.SerializeToJson();
        }

        private void DeserializeGameState(string json)
        {
            placementSystem.DeserializeFromJson(json);
        }

        // ========== METADATA PERSISTENCE ==========

        private void LoadMetadataFromDisk()
        {
            if (File.Exists(metadataFilePath))
            {
                string json = File.ReadAllText(metadataFilePath);
                metadataCollection = JsonUtility.FromJson<SaveMetadataCollection>(json);
            }

            if (metadataCollection == null || metadataCollection.slots == null)
            {
                metadataCollection = new SaveMetadataCollection
                { slots = new SaveMetadata[MAX_SLOTS] };
            }

            if (metadataCollection.slots.Length != MAX_SLOTS)
            {
                SaveMetadata[] resized = new SaveMetadata[MAX_SLOTS];
                int copyCount = Mathf.Min(metadataCollection.slots.Length, MAX_SLOTS);
                for (int i = 0; i < copyCount; i++)
                    resized[i] = metadataCollection.slots[i];
                metadataCollection.slots = resized;
            }
            // Kill ghost entries: JsonUtility deserializes null array
             // elements as empty objects with all-null string fields.
            for (int i = 0; i < metadataCollection.slots.Length; i++)
            {
                SaveMetadata slot = metadataCollection.slots[i];
                if (slot != null && string.IsNullOrEmpty(slot.gameDataFileName))
                    metadataCollection.slots[i] = null;
            }
        }

        private void WriteMetadataToDisk()
        {
            string json = JsonUtility.ToJson(metadataCollection, true);
            File.WriteAllText(metadataFilePath, json);
        }
    }
}
