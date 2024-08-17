namespace TCCS.XACTHandlers
{
    public enum WBFlags
    {
        EntryNames = 0x00010000,
        Compact = 0x00020000,
        SyncDisabled = 0x00040000,
        SeekTables = 0x00080000
    }
    public class WBData
    {
        public uint FlagData { get; set; }
        public uint EntryCount { get; set; }
        public string BankName { get; set; }
        public uint EntryMetadataElementSize { get; set; }
        public uint EntryNameElementSize { get; set; }
        public uint Alignment { get; set; }
        public uint CompactFormat { get; set; }
        public DateTime BuildTime { get; set; }

        public WBData() { }

        public bool BufferOrStream()
        {
            if ((FlagData & 0x00000001) == 0x00000000)
            {
                return true; // Buffer
            }
            return false; // Stream
        }

        public bool HasEntryNames()
        {
            uint Condition = FlagData & (uint)WBFlags.EntryNames;
            return Convert.ToBoolean(Condition);
        }
        public bool HasCompact()
        {
            uint Condition = FlagData & (uint)WBFlags.Compact;
            return Convert.ToBoolean(Condition);
        }
        public bool HasSyncDisabled()
        {
            uint Condition = FlagData & (uint)WBFlags.SyncDisabled;
            return Convert.ToBoolean(Condition);
        }
        public bool HasSeekTables()
        {
            uint Condition = FlagData & (uint)WBFlags.SeekTables;
            return Convert.ToBoolean(Condition);
        }
    }
}
