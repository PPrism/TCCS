using System.Text;
using TCCS.EndianUtils;
using static TCCS.EndianUtils.EndianReader;

namespace TCCS.Handlers
{
	public sealed class XWBHandler : IDisposable
	{
		private BankData WaveBank;
		private readonly EndianReader Reader;
		private WBEntryCompact[] CompactMeta;
		private Header BankHeader;
		private Endianness FileEndian;
		private WBEntryData[] Meta;
		private string[] WaveNames;
		private List<byte[]?> Dpds = [];
		private uint[][] SeekTable;
		private byte[] WaveData;
		private uint SeekEntries = 0;

		internal static readonly (int X, int Y)[] ADPCMCoEfficients = [ (256, 0), (512, -256), (0, 0), (192, 64),
			(240, 0), (460, -208), (392, -232) ];

		public enum EncodingType
		{
			// I know NAudio has its own but that lacks XMA, so for completeness-sake, take this.
			Unknown = 0x0,
			PCM = 0x1,
			ADPCM = 0x2,
			IEEE = 0x3,
			ALaw = 0x6,
			MuLaw = 0x7,
			WMAudio2 = 0x161,
			WMAudio3 = 0x162,
			XMA = 0x165,
			XMA2 = 0x166,
			Extensible = 0xFFFE,
			Dev = 0xFFFF
		}

		public enum BankFlags : uint
		{
			EntryNames = 0x00010000,
			Compact = 0x00020000,
			SyncDisabled = 0x00040000,
			SeekTables = 0x00080000,
		}

		public enum EntryFlags
		{
			ReadAhead = 1,
			LoopCache = 2,
			RemoveLoopTail = 4,
			IgnoreLoop = 8,
		}

		public int Count { get { return WaveBank == null ? 0 : (int)WaveBank.EntryCount; } }

		public void Dispose() => Reader.Close();

		internal static class BitField
		{
			public static uint Get(uint Src, int Length, int Offset)
			{
				var Mask = uint.MaxValue >> sizeof(uint) * 8 - Length;
				return Src >> Offset & Mask;
			}

			public static void Set(uint Src, int Length, int Offset, ref uint Dest)
			{
				var Mask = uint.MaxValue >> sizeof(uint) * 8 - Length;
				Dest |= (Src & Mask) << Offset;
			}
		}

		public XWBHandler(Stream BankStream)
		{
			Reader = new EndianReader(BankStream);
			FileEndian = Endianness.Little; // Not having big as the preset this time due to music extraction which might be from more PC banks than console ones.

			ReadHeader();
			ReadBankData();
			ReadNames();
			ReadMetadata();
			ReadDpdsBytes();
			ReadSeekTable();

			SeekSegment(SegmentIndex.EntryWaveData);
			WaveData = Reader.ReadBytes((int)BankHeader.Segments[SegmentIndex.EntryWaveData].Length);
		}

		public string? GetName(uint Index)
		{
			if (WaveNames == null || WaveNames.Length == 0)
				return null;

			return WaveNames[Index];
		}

		public MetaData GetMetadata(uint Index)
		{
			var Data = new MetaData();

			if ((WaveBank.Flags & BankFlags.Compact) == BankFlags.Compact)
			{
				var MetaEntry = CompactMeta[Index];
				MetaEntry.ComputeLocations(out uint Offset, out uint Length, Index, BankHeader, WaveBank, CompactMeta);

				Data.Duration = WBEntryCompact.GetDuration(Length, WaveBank, FindSeekTable(Index), out SeekEntries);
				Data.LoopStart = Data.LoopLength = 0;
				Data.OffsetBytes = Offset;
				Data.LengthBytes = Length;
			}
			else
			{
				var MetaEntry = Meta[Index];

				Data.Duration = MetaEntry.Collective.Duration;
				Data.LoopStart = MetaEntry.LoopRegion.StartSample;
				Data.LoopLength = MetaEntry.LoopRegion.TotalSamples;
				Data.OffsetBytes = MetaEntry.PlayRegion.Offset;
				Data.LengthBytes = MetaEntry.PlayRegion.Length;
			}

			return Data;
		}

		public byte[]? GetDpds(uint Index)
		{
			if (Dpds == null || Dpds.Count == 0)
				return null;

			return Dpds[(int)Index];
		}

		public uint[]? GetSeekTable(uint Index)
		{
			var MiniFmt = GetMiniWaveFormat(Index);
			uint[]? TableAlloc = FindSeekTable(Index);
			switch (MiniFmt.FormatTag)
			{
				case MiniWaveFormat.Tag.WMA:
					if (TableAlloc != null)
					{

					}
					break;

				case MiniWaveFormat.Tag.XMA:
					if (TableAlloc != null)
					{

					}
					break;

				default:
					return [0];
			}
			return TableAlloc;
		}

		public byte[] GetWaveData(uint Index)
		{
			if ((WaveBank.Flags & BankFlags.Compact) == BankFlags.Compact)
			{
				WBEntryCompact Entry = CompactMeta[Index];
				Entry.ComputeLocations(out uint Offset, out uint Length, Index, BankHeader, WaveBank, CompactMeta);
				byte[]? DataArray = new byte[Length];
				Array.Copy(WaveData, (int)Offset, DataArray, 0, (int)Length);
				return DataArray;
			}
			else
			{
				WBEntryData Entry = Meta[Index];
				byte[]? DataArray = new byte[Entry.PlayRegion.Length];
				Array.Copy(WaveData, (int)Entry.PlayRegion.Offset, DataArray, 0, (int)Entry.PlayRegion.Length);
				return DataArray;
			}
		}

		public WaveFormat GetWaveFormat(uint Index)
		{
			var MiniFmt = GetMiniWaveFormat(Index);

			switch (MiniFmt.FormatTag)
			{
				case MiniWaveFormat.Tag.ADPCM:
					return WaveFormat.SetupNonPCM(EncodingType.ADPCM, (int)MiniFmt.SamplesPerSecond, (int)MiniFmt.Channels, (int)MiniFmt.GetAvgBytesPerSec(), (int)MiniFmt.GetAlignment(), (int)MiniFmt.GetBitsPerSample(), MiniFmt.GetExtraHeader());

				case MiniWaveFormat.Tag.WMA:
					return WaveFormat.SetupNonPCM((MiniFmt.BitsPerSample & 0x1) > 0 ? EncodingType.WMAudio3 : EncodingType.WMAudio2,
					(int)MiniFmt.SamplesPerSecond,
					(int)MiniFmt.Channels,
					(int)MiniFmt.GetAvgBytesPerSec(),
					(int)MiniFmt.GetAlignment(),
					(int)MiniFmt.GetBitsPerSample(),
					[]);

				case MiniWaveFormat.Tag.XMA:
					return WaveFormat.SetupNonPCM(EncodingType.XMA2, (int)MiniFmt.SamplesPerSecond, (int)MiniFmt.Channels, (int)MiniFmt.GetAvgBytesPerSec(), (int)MiniFmt.GetAlignment(), (int)MiniFmt.GetBitsPerSample(), MiniFmt.GetExtraHeader());

				case MiniWaveFormat.Tag.PCM:
				default:
					return new WaveFormat((int)MiniFmt.SamplesPerSecond, (int)MiniFmt.GetBitsPerSample(), (int)MiniFmt.Channels);
			}
		}

		private MiniWaveFormat GetMiniWaveFormat(uint Index)
		{
			return (WaveBank.Flags & BankFlags.Compact) == BankFlags.Compact ? WaveBank.CompactFormat : Meta[Index].Format;
		}

		private void ReadHeader()
		{
			BankHeader = new Header
			{
				WBSignature = Reader.ReadBytes(Header.LittleMagic.Length),
				Version = Reader.ReadUInt32(FileEndian),
				HeaderVersion = Reader.ReadUInt32(FileEndian)
			};

			if (!BankHeader.WBSignature.SequenceEqual(Header.LittleMagic))
			{
				byte[] SwappedSig = Header.LittleMagic;
				Array.Reverse(SwappedSig);
				BankHeader.WBSignature = SwappedSig;
				FileEndian = Endianness.Big;
			}

			for (int i = 0; i < BankHeader.Segments.Length; i++)
			{
				SegmentData Segment = new()
				{
					Offset = Reader.ReadUInt32(FileEndian),
					Length = Reader.ReadUInt32(FileEndian)
				};
				BankHeader.Segments[i] = Segment;
			}
		}
		private void ReadBankData()
		{
			SeekSegment(SegmentIndex.BankData);

			WaveBank = new BankData
			{
				Flags = (BankFlags)Reader.ReadUInt32(FileEndian),
				EntryCount = Reader.ReadUInt32(FileEndian),
				BankName = Encoding.ASCII.GetString(Reader.ReadBytes(64)).Trim('\0'),
				MetaElementSize = Reader.ReadUInt32(FileEndian),
				NameElementSize = Reader.ReadUInt32(FileEndian),
				Alignment = Reader.ReadUInt32(FileEndian),
				CompactFormat = (MiniWaveFormat)Reader.ReadUInt32(FileEndian),
				BuildTime = new FileTime { LowTime = Reader.ReadUInt32(FileEndian), HighTime = Reader.ReadUInt32(FileEndian) }
			};
		}

		private void ReadNames()
		{
			WaveNames = new string[((int)WaveBank.EntryCount)];
			var NameData = BankHeader.Segments[SegmentIndex.EntryNames].Length;
			if (NameData > 0 && NameData >= WaveBank.NameElementSize * WaveBank.EntryCount)
			{
				SeekSegment(SegmentIndex.EntryNames);
				for (int Entry = 0; Entry < WaveBank.EntryCount; ++Entry)
				{
					WaveNames[Entry] = Encoding.ASCII.GetString(Reader.ReadBytes((int)WaveBank.NameElementSize)).Trim('\0');
				}
			}
		}

		private void ReadMetadata()
		{
			SeekSegment(SegmentIndex.EntryMetaData);

			if ((WaveBank.Flags & BankFlags.Compact) == BankFlags.Compact)
			{
				CompactMeta = new WBEntryCompact[(int)WaveBank.EntryCount];

				for (int Entry = 0; Entry < WaveBank.EntryCount; ++Entry)
				{
					WBEntryCompact CompactData = new();
					CompactData.CompactValue = Reader.ReadUInt32(FileEndian);
					CompactMeta[Entry] = CompactData;
				}
			}
			else
			{
				Meta = new WBEntryData[(int)WaveBank.EntryCount];
				for (int Entry = 0; Entry < WaveBank.EntryCount; ++Entry)
				{
					DurationNFlags Bundle = new();
					Bundle.Collective = Reader.ReadUInt32(FileEndian);

					WBEntryData MetaEntry = new()
					{
						Collective = Bundle,
						Format = (MiniWaveFormat)Reader.ReadUInt32(FileEndian),
						PlayRegion = new SegmentData { Offset = Reader.ReadUInt32(FileEndian), Length = Reader.ReadUInt32(FileEndian) },
						LoopRegion = new SampleRegion { StartSample = Reader.ReadUInt32(FileEndian), TotalSamples = Reader.ReadUInt32(FileEndian) }
					};
					Meta[Entry] = MetaEntry;    // Ok, maybe the names could've been better.
				}
			}
		}

		private void ReadDpdsBytes()
		{
			var SeekLength = BankHeader.Segments[SegmentIndex.SeekTables].Length;

			if (SeekLength > 0) // While Seek tables are not relevant here, Dpds segments only exist where Seek tables do... from what I have found.
			{
				int[] Offsets = new int[WaveBank.EntryCount];
				SeekSegment(SegmentIndex.SeekTables);

				for (int i = 0; i < Offsets.Length; ++i)
				{
					Offsets[i] = Reader.ReadInt32(FileEndian);
				}

				long CurrentPos = Reader.BaseStream.Position;
				for (int j = 0; j < Offsets.Length; ++j)
				{
					if (Offsets[j] >= 0)
					{
						Reader.BaseStream.Seek(Offsets[j] + CurrentPos, SeekOrigin.Begin);
						uint PacketCount = Reader.ReadUInt32(FileEndian);
						List<byte> CurrentData = [];
						for (int k = 0; k < PacketCount; ++k)
						{
							CurrentData.AddRange(BitConverter.GetBytes(Reader.ReadUInt32(FileEndian)));
						}
						Dpds.Add(CurrentData.ToArray());
					}
					else
					{
						Dpds.Add(null);
					}
				}
			}
		}

		private void ReadSeekTable()
		{
			// Needs more work if it is to be used elsewhere, otherwise we're good
			var SeekLength = BankHeader.Segments[SegmentIndex.SeekTables].Length;

			if (SeekLength > 0)
			{
				SeekTable = new uint[WaveBank.EntryCount][];
				SeekSegment(SegmentIndex.SeekTables);

				SeekLength /= sizeof(uint);
				var SeekTableData = new uint[SeekLength];

				for (int Entry = 0; Entry < SeekLength; ++Entry)
				{
					SeekTableData[Entry] = Reader.ReadUInt32(FileEndian);
				}

				for (int Entry = 0; Entry < WaveBank.EntryCount; ++Entry)
				{
					var Data = SeekTableData[Entry];
					if (Data != unchecked((uint)-1))  // Check if the data is actually valid
					{
						Data += WaveBank.EntryCount;
						if (Data <= SeekLength)
						{
							if (SeekTableData[Data] > 0)
							{
								SeekTable[Entry] = new uint[SeekTableData[Data]];
							}
						}
					}
					else
					{
						SeekTable[Entry] = [];
					}
				}
			}
		}

		private uint[] FindSeekTable(uint Index)
		{
			if (SeekTable == null || Index >= WaveBank.EntryCount || Index >= SeekTable.Length)
				return null;

			return SeekTable[(int)Index];
		}

		private void SeekSegment(int Index)
		{
			Reader.BaseStream.Seek(BankHeader.Segments[Index].Offset, SeekOrigin.Begin);
		}

		public struct MetaData
		{
			public uint Duration { get; set; }

			public uint LoopStart { get; set; }

			public uint LoopLength { get; set; }

			public uint OffsetBytes { get; set; }

			public uint LengthBytes { get; set; }
		}

		private struct WBEntryData
		{
			public DurationNFlags Collective;
			public MiniWaveFormat Format;
			public SegmentData PlayRegion;
			public SampleRegion LoopRegion;
		}

		private struct WBEntryCompact
		{
			internal uint CompactValue;

			public uint DataOffset
			{
				get { return BitField.Get(CompactValue, 21, 0); }
				set { BitField.Set(value, 3, 2, ref CompactValue); }
			}

			public uint LengthDeviation
			{
				get { return BitField.Get(CompactValue, 11, 21); }
				set { BitField.Set(value, 3, 2, ref CompactValue); }
			}

			public static uint GetDuration(uint Length, BankData WaveBank, uint[] SeekTable, out uint SeekEntries)
			{
				SeekEntries = 0;
				switch (WaveBank.CompactFormat.FormatTag)
				{
					case MiniWaveFormat.Tag.ADPCM:
						{
							uint Duration = Length / WaveBank.CompactFormat.GetAlignment() * WaveBank.CompactFormat.GetAdpcmSamples();
							uint Remainder = Length % WaveBank.CompactFormat.GetAlignment();
							if (Remainder != 0)
							{
								if (Remainder >= 7 * WaveBank.CompactFormat.Channels)
									Duration += Remainder * 2 / WaveBank.CompactFormat.Channels - 12;
							}
							return Duration;
						}

					case MiniWaveFormat.Tag.WMA:
						if (SeekTable != null && SeekTable.Length > 0)
						{
							SeekEntries += (uint)(SeekTable.Length + 1);
							return SeekTable[SeekTable.Length - 1] / (2 * WaveBank.CompactFormat.Channels);
						}
						return 0;

					case MiniWaveFormat.Tag.XMA:
						if (SeekTable != null && SeekTable.Length > 0)
						{
							SeekEntries += (uint)(SeekTable.Length + 1);
							return SeekTable[SeekTable.Length - 1];
						}
						return 0;

					default:
						return Length * 8 / (WaveBank.CompactFormat.GetBitsPerSample() * WaveBank.CompactFormat.Channels);
				}
			}

			public void ComputeLocations(out uint Offset, out uint Length, uint Idx, Header HeaderData, BankData WaveBank, WBEntryCompact[] Entries)
			{
				Offset = DataOffset * WaveBank.Alignment;

				if (Idx < WaveBank.EntryCount - 1)
				{
					Length = Entries[Idx + 1].DataOffset * WaveBank.Alignment - Offset - LengthDeviation;
				}
				else
				{
					Length = HeaderData.Segments[SegmentIndex.EntryWaveData].Length - Offset - LengthDeviation;
				}
			}
		}

		private struct FileTime
		{
			public uint LowTime;
			public uint HighTime;
		}

		private struct DurationNFlags
		{
			internal uint Collective;

			public EntryFlags Flags
			{
				get { return (EntryFlags)BitField.Get(Collective, 4, 0); }
				set { BitField.Set((uint)value, 28, 4, ref Collective); }
			}

			public uint Duration
			{
				get { return BitField.Get(Collective, 3, 2); }
				set { BitField.Set(value, 3, 2, ref Collective); }
			}
		}

		private struct MiniWaveFormat
		{
			private static readonly uint[] WMAAvgBytesPerSec = [12000, 24000, 4000, 6000, 8000, 20000, 2500];

			private static readonly uint[] WMABlockAlign = [929, 1487, 1280, 2230, 8917, 8192, 4459, 5945, 2304, 1536, 1485, 1008, 2731, 4096, 6827, 5462, 1280];

			private uint MiniWaveFmt;

			public enum Tag : uint
			{
				PCM = 0x0,
				XMA = 0x1,
				ADPCM = 0x2,
				WMA = 0x3,
			}

			public Tag FormatTag
			{
				get { return (Tag)BitField.Get(MiniWaveFmt, 2, 0); }
				set { BitField.Set((uint)value, 2, 0, ref MiniWaveFmt); }
			}

			public uint Channels
			{
				get { return BitField.Get(MiniWaveFmt, 3, 2); }
				set { BitField.Set(value, 3, 2, ref MiniWaveFmt); }
			}

			public uint SamplesPerSecond
			{
				get { return BitField.Get(MiniWaveFmt, 18, 5); }
				set { BitField.Set(value, 18, 5, ref MiniWaveFmt); }
			}

			public uint BlockAlign
			{
				get { return BitField.Get(MiniWaveFmt, 8, 23); }
				set { BitField.Set(value, 8, 23, ref MiniWaveFmt); }
			}

			public uint BitsPerSample
			{
				get { return BitField.Get(MiniWaveFmt, 1, 31); }
				set { BitField.Set(value, 1, 31, ref MiniWaveFmt); }
			}

			public static explicit operator MiniWaveFormat(uint Value)
			{
				MiniWaveFormat Format = new();
				Format.MiniWaveFmt = Value;
				return Format;
			}

			public uint GetAdpcmSamples()
			{
				uint Alignment = (BlockAlign + 22) * Channels;
				return Alignment * 2 / Channels - 12;
			}

			public uint GetAlignment()
			{
				switch (FormatTag)
				{
					case Tag.PCM:
						return BlockAlign;

					case Tag.XMA:
						return Channels * 16 / 8;

					case Tag.ADPCM:
						return (BlockAlign + 22) * Channels;

					case Tag.WMA:
						uint Index = BlockAlign & 0x1F;
						if (Index < WMABlockAlign.Length)
							return WMABlockAlign[Index];
						break;

					default:
						break;
				}
				return 0;
			}

			public uint GetAvgBytesPerSec()
			{
				switch (FormatTag)
				{
					case Tag.PCM:
					case Tag.XMA:
						return SamplesPerSecond * GetAlignment(); // XMA_OUTPUT_SAMPLE_BITS = 16
					case Tag.ADPCM:
						{
							uint blockAlign = GetAlignment();
							uint samplesPerAdpcmBlock = GetAdpcmSamples();
							return blockAlign * SamplesPerSecond / samplesPerAdpcmBlock;
						}
					case Tag.WMA:
						uint bytesPerSecondIndex = BlockAlign >> 5;
						if (bytesPerSecondIndex < WMAAvgBytesPerSec.Length)
							return WMAAvgBytesPerSec[bytesPerSecondIndex];
						break;

					default:
						break;
				}
				return 0;
			}

			public uint GetBitsPerSample()
			{
				switch (FormatTag)
				{
					case Tag.WMA:
					case Tag.XMA:
						return 16;

					case Tag.ADPCM:
						return 4;

					default:
						return (uint)(BitsPerSample == 0x1 ? 16 : 8);
				}
			}

			public byte[] GetExtraHeader()
			{
				byte[] AddHeader;
				MemoryStream AddHeaderStream = new();
				EndianWriter AddHeaderWriter = new(AddHeaderStream, EndianWriter.Endianness.Little);
				switch (FormatTag)
				{
					case Tag.XMA:
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

						if (Channels == 2)
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
						return AddHeader;

					case Tag.ADPCM:
						AddHeaderWriter.Write(GetAdpcmSamples());
						AddHeaderWriter.Write(ADPCMCoEfficients.Length);
						AddHeader = AddHeaderStream.ToArray();
						foreach (var (CoEfficientX, CoEfficientY) in ADPCMCoEfficients)
						{
							MemoryStream MemStreamADPCM = new();
							EndianWriter BinWriterADPCM = new(MemStreamADPCM, EndianWriter.Endianness.Little);
							BinWriterADPCM.Write((short)CoEfficientX);
							BinWriterADPCM.Write((short)CoEfficientY);
							byte[] CoEfficients = MemStreamADPCM.ToArray();
							foreach (byte CoEfficient in CoEfficients)
							{
								AddHeader.Append(CoEfficient);
							}
						}
						return AddHeader;
				}
				return [];
			}
		}

		private struct SegmentData
		{
			public uint Offset;
			public uint Length;
		}

		private struct SampleRegion
		{
			public uint StartSample;
			public uint TotalSamples;
		}

		private static class SegmentIndex
		{
			public const int BankData = 0;
			public const int EntryMetaData = 1;
			public const int SeekTables = 2;
			public const int EntryNames = 3;
			public const int EntryWaveData = 4;
			public const int Count = 5;
		};

		private class BankData
		{
			public BankFlags Flags;
			public uint EntryCount;
			public string BankName;
			public uint NameElementSize; // Size of each CompactData name element, in bytes
			public uint MetaElementSize; // Size of each CompactData meta-WaveBank element, in bytes
			public uint Alignment;
			public MiniWaveFormat CompactFormat; // Format WaveBank
			public FileTime BuildTime;
		}

		private class Header
		{
			public const uint BankVersion = 44;
			internal static readonly byte[] LittleMagic = Encoding.ASCII.GetBytes("WBND");

			internal required byte[] WBSignature; // Magic of the bank; A requirement for structure.
			public uint Version; // Tool version
			public uint HeaderVersion; // WaveBank version
			public SegmentData[] Segments;

			public Header()
			{
				Segments = new SegmentData[SegmentIndex.Count];
			}
		}

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
}