namespace TCCS.XACTHandlers
{
    public class XMAData
    {
        public ushort XMANumStreams { get; set; }
        public uint XMAChannelMask { get; set; }
        public uint XMASamplesEncoded { get; set; }
        public uint XMABytesPBlock { get; set; }
        public uint XMAPlayStart { get; set; }
        public uint XMAPlayLength { get; set; }
        public uint XMALoopStart { get; set; }
        public uint XMALoopLength { get; set; }
        public byte XMALoopCount { get; set; }
        public byte XMAEncoderVer { get; set; }
        public ushort XMABlockCount { get; set; }
    }
}
