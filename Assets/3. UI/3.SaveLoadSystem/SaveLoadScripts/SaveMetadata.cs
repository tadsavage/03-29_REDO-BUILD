using System;

namespace SaveLoadSystem
{
    [Serializable]
    public class SaveMetadata
    {
        public int slotIndex;
        public string saveName;
        public string timestamp;          // "Apr 26, 2026  9:53 PM"
        public long timestampTicks;     // DateTime.Ticks for sorting
        public string thumbnailFileName;  // "slot_0_thumb.png"
        public string gameDataFileName;   // "slot_0_data.json"
    }

    [Serializable]
    public class SaveMetadataCollection
    {
        public SaveMetadata[] slots;
    }
}
