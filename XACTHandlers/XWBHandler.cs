using System.Text;
using static TCCS.XACTHandlers.EndianReader;

namespace TCCS.XACTHandlers
{
    public class XWBHandler
    {
        public enum WBankFormat
        {
            PCM,
            XMA,
            ADPCM,
            WMA
        }

        public struct WBRegion
        {
            public uint Offset, Length;
            internal WBRegion(uint offset, uint length)
            {
                Offset = offset;
                Length = length;
            }
        }

        public struct WBEntry
        {
            public string Name;
            public byte[] Header, Data, Dpds, Seek;

            internal WBEntry(string EntryName, byte[] EntryHeader, byte[] EntryData, byte[] EntryDpds, byte[] EntrySeek)
            {
                Name = EntryName;
                Header = EntryHeader;
                Data = EntryData;
                Dpds = EntryDpds;
                Seek = EntrySeek;
            }
        }

		private static IEnumerable<(int index, T value)> Enumerate<T>(IEnumerable<T> Collection) => Collection.Select((Index, Value) => (Value, Index));

		private readonly Stream WBStream;

		private static readonly byte[] LittleMagic = Encoding.ASCII.GetBytes("WBND");
		private static Endianness FileEndian = Endianness.Big;

		public List<WBEntryData> MetadataEntryList { get; set; } = new List<WBEntryData>();
		public List<byte[]> SeekTables { get; set; } = new();
		public List<string> EntryNames { get; set; } = new List<string>();
		public WBEntry[] Entries { get; set; } = Array.Empty<WBEntry>();
		public WBRegion[] Segments { get; set; } = new WBRegion[5]; // BankData, EntryMetaData, SeekTables, EntryNames, EntryWaveData

		public byte[] WBSignature { get; set; }
        public uint WBVersion { get; set; }
        public uint WBHeaderVersion { get; set; }
		public WBData Data { get; set; }

		public uint WBankEntryDurationMask = 0xFFFFFFF0;
        public uint WBankFormatChannels = 0x0000001C;
        public uint WBankFormatSampsPerSec = 0x007FFFE0;
        public uint WBankFormatBlockAlign = 0x7F800000;
        public uint WBankFormatBitsPerSample = 0x80000000;

        public uint[] WMAAvgBytesPerSec = new uint[7] { 12000, 24000, 4000, 6000, 8000, 20000, 2500 };
        public uint[] WMABlockAlignment = new uint[17] { 929, 1487, 1280, 2230, 8917, 8192, 4459, 5945,
            2304, 1536, 1485, 1008, 2731, 4096, 6827, 5462, 1280 };
        public (int X, int Y)[] ADPCMCoEfficients = new[] { (256, 0), (512, -256), (0, 0), (192, 64),
            (240, 0), (460, -208), (392, -232) };
        
        public static DateTime FiletimeToDate(ulong Low, ulong High)
		{
			return DateTime.FromFileTimeUtc(0).AddMilliseconds((Low | High << 32) / 10000);
        }

        public XWBHandler(string Filename)
        {
            WBStream = File.Open(Filename, FileMode.Open);
            using EndianReader Reader = new(WBStream, Encoding.Default, false);
            Read(Reader);
        }

        public void Read(EndianReader Reader)
        {
            WBSignature = Reader.ReadBytes(LittleMagic.Length);

            if (WBSignature.SequenceEqual(LittleMagic))
            {
                FileEndian = Endianness.Little;
            }

            WBVersion = Reader.ReadUInt32(FileEndian);
            WBHeaderVersion = Reader.ReadUInt32(FileEndian);

            for (int i = 0; i < 5; i++)
            {
                Segments[i].Offset = Reader.ReadUInt32(FileEndian);
                Segments[i].Length = Reader.ReadUInt32(FileEndian);
            }

            if (Segments[0].Offset > 0)
            {
                Reader.BaseStream.Seek(Segments[0].Offset, SeekOrigin.Begin);
				Data = new WBData
				{
					FlagData = Reader.ReadUInt32(FileEndian),
					EntryCount = Reader.ReadUInt32(FileEndian),
					BankName = new string(Reader.ReadChars(64)),
					EntryMetadataElementSize = Reader.ReadUInt32(FileEndian),
					EntryNameElementSize = Reader.ReadUInt32(FileEndian),
					Alignment = Reader.ReadUInt32(FileEndian),
					CompactFormat = Reader.ReadUInt32(FileEndian)
				};
				ulong LowBuild = Reader.ReadUInt32(FileEndian);
				ulong HighBuild = Reader.ReadUInt32(FileEndian);
				Data.BuildTime = FiletimeToDate(LowBuild, HighBuild);
            }

            if (Segments[1].Offset > 0)
            {
                Reader.BaseStream.Seek(Segments[1].Offset, SeekOrigin.Begin);
                for (int i = 0; i < Data.EntryCount; i++)
                {
					WBEntryData ListEntry = new()
					{
						DurationNFlags = Reader.ReadUInt32(FileEndian),
						Format = Reader.ReadUInt32(FileEndian),
						PlayOffset = Reader.ReadUInt32(FileEndian),
						PlayLength = Reader.ReadUInt32(FileEndian),
						LoopOffset = Reader.ReadUInt32(FileEndian),
						LoopTotal = Reader.ReadUInt32(FileEndian)
					};
					MetadataEntryList.Add(ListEntry);
                }
            }

            if (Segments[2].Offset > 0)
            {
                int[] SeekOffsets = new int[Data.EntryCount];
                Reader.BaseStream.Seek(Segments[2].Offset, SeekOrigin.Begin);
                for (int i = 0; i < Data.EntryCount; i++)
                {
                    SeekOffsets[i] = Reader.ReadInt32(FileEndian);
                }
                long CurrentPos = Reader.BaseStream.Position;
                for (int j = 0; j < SeekOffsets.Length; j++)
                {
                    if (SeekOffsets[j] >= 0)
                    {
                        Reader.BaseStream.Seek(SeekOffsets[j] + CurrentPos, SeekOrigin.Begin);
                        uint PacketCount = Reader.ReadUInt32(FileEndian);
                        List<byte> CurrentSeekData = new();
                        for (int k = 0; k < PacketCount; k++)
                        {
                            CurrentSeekData.AddRange(BitConverter.GetBytes(Reader.ReadUInt32(FileEndian)));
                        }
                        SeekTables.Add(CurrentSeekData.ToArray());
                    }
                    else
                    {
                        SeekTables.Add(null);
                    }
                }
            }

            if (Segments[3].Offset > 0)
            {
                Reader.BaseStream.Seek(Segments[3].Offset, SeekOrigin.Begin);
                for (int i = 0; i < Data.EntryCount; i++)
                {
                    // Not needed for Terraria
                }
            }

            Entries = new WBEntry[MetadataEntryList.Count];
            foreach (var (Index, CurrentMetaEntry) in Enumerate(MetadataEntryList))
            {
				byte[] EntryData = Array.Empty<byte>();
				byte[] EntryHeader = Array.Empty<byte>();
				byte[] AddHeader = Array.Empty<byte>();
				CurrentWBEntry Entry = new();
                MemoryStream EntryHeaderStream = new();
                EndianWriter EntryHeaderWriter = new(EntryHeaderStream, EndianWriter.Endianness.Little);
                MemoryStream AddHeaderStream = new();
                EndianWriter AddHeaderWriter = new(AddHeaderStream, EndianWriter.Endianness.Little);

                Entry.CurrentFlags = (uint)CurrentMetaEntry.SplitFlags();
                Entry.CurrentDuration = (CurrentMetaEntry.DurationNFlags & WBankEntryDurationMask) >> 4;
                Entry.CurrentFormatTag = CurrentMetaEntry.GetTrueFormat();
                Entry.CurrentChannels = (CurrentMetaEntry.Format & WBankFormatChannels) >> 2;
                Entry.CurrentSamplesPSec = (CurrentMetaEntry.Format & WBankFormatSampsPerSec) >> 5;
                Entry.CurrentBlockAlignment = (CurrentMetaEntry.Format & WBankFormatBlockAlign) >> 23;
                Entry.CurrentBitsPSample = (CurrentMetaEntry.Format & WBankFormatBitsPerSample) >> 31;
                Entry.CurrentName = ""; // Not needed since we don't get the names from the wave bank.
                if (EntryNames.Count > 0)
                {
                    Entry.CurrentName = EntryNames[Index];
                }
                Entry.CurrentDpds = Array.Empty<byte>();
                Entry.CurrentSeek = Array.Empty<byte>();

                if (Entry.CurrentFormatTag == (int)WBankFormat.PCM)
                {
                    Entry.CurrentFormatTag = (int)WaveWriter.WaveFormat.PCM;
                    if (Entry.CurrentBitsPSample == 1)
                    {
                        Entry.CurrentBitsPSample = 16;
                    }
                    else
                    {
                        Entry.CurrentBitsPSample = 8;
                    }
                    Entry.CurrentAvgBytesPSec = Entry.CurrentBlockAlignment * Entry.CurrentSamplesPSec;
                }
                else if (Entry.CurrentFormatTag == (int)WBankFormat.XMA)
                {
                    
                    Entry.CurrentFormatTag = (int)WaveWriter.WaveFormat.XMA2;
                    Entry.CurrentBitsPSample = 16;
                    Entry.CurrentAvgBytesPSec = 0;

					XMAData Parameters = new()
					{
						XMANumStreams = 1,
						XMAChannelMask = 0,
						XMASamplesEncoded = 0,
					    XMABytesPBlock = 0,
					    XMAPlayStart = 0,
					    XMAPlayLength = 0,
					    XMALoopStart = 0,
					    XMALoopLength = 0,
					    XMALoopCount = 0,
					    XMAEncoderVer = 4,
					    XMABlockCount = 1
				    };

					if (Entry.CurrentChannels == 2)
					{
						Parameters.XMAChannelMask = 3;
					}

					AddHeaderWriter.Write(Parameters.XMANumStreams);
                    AddHeaderWriter.Write(Parameters.XMAChannelMask);
                    AddHeaderWriter.Write(Parameters.XMASamplesEncoded);
                    AddHeaderWriter.Write(Parameters.XMABytesPBlock);
                    AddHeaderWriter.Write(Parameters.XMAPlayStart);
                    AddHeaderWriter.Write(Parameters.XMAPlayLength);
                    AddHeaderWriter.Write(Parameters.XMALoopStart);
                    AddHeaderWriter.Write(Parameters.XMALoopLength);
                    AddHeaderWriter.Write(Parameters.XMALoopCount);
                    AddHeaderWriter.Write(Parameters.XMAEncoderVer);
                    AddHeaderWriter.Write(Parameters.XMABlockCount);
                    AddHeader = AddHeaderStream.ToArray();

                    if (SeekTables.Count > 0)
                    {
                        Entry.CurrentSeek = SeekTables[Index];
                    }
                    else
                    {
                        throw new Exception("No seek tables found; XMA2 requires seek tables");
                    }
                }
                else if (Entry.CurrentFormatTag == (int)WBankFormat.ADPCM)
                {
                    Entry.CurrentFormatTag = (int)WaveWriter.WaveFormat.ADPCM;
                    Entry.CurrentBitsPSample = 4;
                    Entry.CurrentBlockAlignment = (Entry.CurrentBlockAlignment + 22) * Entry.CurrentChannels; // 22 is the alignment offset needed for ADPCM
                    ushort ADPCMSampsPBlock = (ushort)Math.Floor((decimal)((Entry.CurrentBlockAlignment - (7 * Entry.CurrentChannels)) * 8)
                        / ((Entry.CurrentBitsPSample * Entry.CurrentChannels) + 2));
                    Entry.CurrentAvgBytesPSec = (uint)Math.Floor(Entry.CurrentSamplesPSec / (decimal)ADPCMSampsPBlock) 
                        * Entry.CurrentBlockAlignment;
                    ushort ADPCMCoEfCount = (ushort)ADPCMCoEfficients.Length;
                    AddHeaderWriter.Write(ADPCMSampsPBlock);
                    AddHeaderWriter.Write(ADPCMCoEfCount);
                    AddHeader = AddHeaderStream.ToArray();
                    foreach (var (CoEfficientX, CoEfficientY) in ADPCMCoEfficients)
                    {
                        MemoryStream memStreamADPCM = new();
                        EndianWriter binWriterADPCM = new(memStreamADPCM, EndianWriter.Endianness.Little);
                        binWriterADPCM.Write((short)CoEfficientX);
                        binWriterADPCM.Write((short)CoEfficientY);
                        byte[] CoEfficients = memStreamADPCM.ToArray();
                        foreach (byte CoEfficient in CoEfficients)
                        {
                            AddHeader.Append(CoEfficient);
                        }
                    }
                }
                else if (Entry.CurrentFormatTag == (int)WBankFormat.WMA)
                {
                    Entry.CurrentFormatTag = (int)WaveWriter.WaveFormat.WMAudio2;
                    if (Entry.CurrentBitsPSample == 1)
                    {
                        Entry.CurrentFormatTag = (int)WaveWriter.WaveFormat.WMAudio3;
                    }
                    Entry.CurrentBitsPSample = 16;
                    Entry.CurrentAvgBytesPSec = WMAAvgBytesPerSec[Entry.CurrentBlockAlignment >> 5];
                    Entry.CurrentBlockAlignment = WMABlockAlignment[Entry.CurrentBlockAlignment & 0x1f];

                    if (SeekTables.Count > 0)
                    {
                        Entry.CurrentDpds = SeekTables[Index];
                    }
                    else
                    {
                        throw new Exception("No seek tables found; xWMA requires seek tables");
                    }
                }
                else { throw new Exception("Unknown XWB format"); }

                EntryHeaderWriter.Write((ushort)Entry.CurrentFormatTag);
                EntryHeaderWriter.Write((ushort)Entry.CurrentChannels);
                EntryHeaderWriter.Write(Entry.CurrentSamplesPSec);
                EntryHeaderWriter.Write(Entry.CurrentAvgBytesPSec);
                EntryHeaderWriter.Write((ushort)Entry.CurrentBlockAlignment);
                EntryHeaderWriter.Write((ushort)Entry.CurrentBitsPSample);
                EntryHeaderWriter.Write((ushort)AddHeader.Length);
                EntryHeader = EntryHeaderStream.ToArray();
                foreach (byte Addition in AddHeader)
                {
                    EntryHeader.Append(Addition);
                }

                Reader.BaseStream.Seek(Segments[4].Offset + CurrentMetaEntry.PlayOffset, SeekOrigin.Begin);
                EntryData = Reader.ReadBytes((int)CurrentMetaEntry.PlayLength);
                Entries[Index].Name = Entry.CurrentName;
                Entries[Index].Header = EntryHeader;
                Entries[Index].Data = EntryData;
                Entries[Index].Dpds = Entry.CurrentDpds;
                Entries[Index].Seek = Entry.CurrentSeek;
            }
        }
    }
}