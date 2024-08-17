namespace TCCS.XACTHandlers
{
    public class CurrentWBEntry
    {
        public uint CurrentFlags { get; set; }
        public uint CurrentDuration { get; set; }
        public uint CurrentFormatTag { get; set; }
        public uint CurrentChannels { get; set; }
        public uint CurrentSamplesPSec { get; set; }
        public uint CurrentBlockAlignment { get; set; }
        public uint CurrentBitsPSample { get; set; }
        public string CurrentName { get; set; }

        public byte[] CurrentSeek { get; set; }
        public byte[] CurrentDpds { get; set; }

        public uint CurrentAvgBytesPSec { get; set; }

        public CurrentWBEntry() { }
    }
}
