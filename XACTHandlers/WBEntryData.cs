namespace TCCS.XACTHandlers
{
    public enum WBEntryFlags
    {
        ReadAhead = 1,
        LoopCache = 2,
        RemoveLoopTail = 4,
        IgnoreLoop = 8
    }
    public class WBEntryData
    {
        public uint DurationNFlags { get; set; }
        public uint Format { get; set; }
        public uint PlayOffset { get; set; }
        public uint PlayLength { get; set; }
        public uint LoopOffset { get; set; }
        public uint LoopTotal { get; set; }
        public WBEntryData() { }
        public WBEntryFlags SplitFlags()
        {
            return (WBEntryFlags)(DurationNFlags & 0x0000000F);
        }
        public uint GetTrueFormat()
        {
            return Format & 0x00000003;
        }
    }
}