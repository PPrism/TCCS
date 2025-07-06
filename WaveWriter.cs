using NAudio.Wave;
using System.Diagnostics;
using System.Text;
using TCCS.EndianUtils;
using static TCCS.Handlers.XWBHandler;

namespace TCCS
{
	public class WaveWriter
	{
		private const uint SampleRate = 44100;
		private const ushort ChannelCount = 2;
		private static readonly ushort BlockAlignment = Program.BlockSetting;

		public int HeaderSize = 16;
		public int XMA2Size = 34;
		public int ExtSize = 22;
		public int? DpdsSize = null;
		public int? SeekSize = null;

		public uint ExtChannelMask;
		public uint[] Seek = [];
		public ushort ExtValidBitRate;
		public ushort? HSize = null;

		public XMAData Parameters = new();
		public Guid ExtSubfmt;
		private WaveStream? WaveData;
		private WaveFormat Format;

		public byte[] Header, Data, Dpds = [];
		public byte[]? ExtSubfmtBytes, Remainder, ExtRemainder;
		private byte[] SampleBufferA = new byte[BlockAlignment]; // ADPCM
		private static readonly short[] SampleBufferP = new short[(BlockAlignment * 2) - 24]; // PCM
		private readonly byte[] TempBuffer = new byte[SampleBufferP.Length * 2];
		public byte[] RIFF = Encoding.ASCII.GetBytes("WAVE");

		private readonly int[] AdaptionTable =
		[
				230, 230, 230, 230,
				307, 409, 512, 614,
				768, 614, 512, 409,
				307, 230, 230, 230,
		];

		public WaveWriter(WaveFormat WaveFormat, string WaveName, byte[] WaveHeader, MetaData WaveMeta, byte[] WaveData, byte[]? WaveDpds, uint[]? WaveSeek, EndianReader.Endianness Endian)
		{
			Format = WaveFormat;
			// Name = WaveName;
			Header = WaveHeader;
			// Meta = WaveMeta;
			Data = WaveData;
			Dpds = WaveDpds;
			Seek = WaveSeek;

			MemoryStream EntryDetails = new(Header);
			EndianReader EntryReader = new(EntryDetails, Endian);

			if (Header.Length >= HeaderSize + 2)
			{
				EntryReader.BaseStream.Position = 0x10;
				HSize = EntryReader.ReadUInt16();
				HeaderSize += 2;

				if (Format.Encoding == EncodingType.XMA2)
				{
					if (HSize != XMA2Size)
					{
						throw new Exception("Unknown Size");
					}

					Parameters.XMANumStreams = EntryReader.ReadUInt16();
					Parameters.XMAChannelMask = EntryReader.ReadUInt32();
					Parameters.XMASamplesEncoded = EntryReader.ReadUInt32();
					Parameters.XMABytesPBlock = EntryReader.ReadUInt32();
					Parameters.XMAPlayStart = EntryReader.ReadUInt32();
					Parameters.XMAPlayLength = EntryReader.ReadUInt32();
					Parameters.XMALoopStart = EntryReader.ReadUInt32();
					Parameters.XMALoopLength = EntryReader.ReadUInt32();
					Parameters.XMALoopCount = EntryReader.ReadByte();
					Parameters.XMAEncoderVer = EntryReader.ReadByte();
					Parameters.XMABlockCount = EntryReader.ReadUInt16();

					HeaderSize += XMA2Size;
				}
				else if (Format.Encoding == EncodingType.Extensible)
				{
					if (HSize < ExtSize)
					{
						throw new Exception("Invalid Size");
					}
					ExtValidBitRate = EntryReader.ReadUInt16();
					ExtChannelMask = EntryReader.ReadUInt32();
					ExtSubfmtBytes = EntryReader.ReadBytes(16);
					ExtSubfmt = new Guid(ExtSubfmtBytes);
					HeaderSize += ExtSize;

					if (HSize > ExtSize)
					{
						ExtRemainder = EntryReader.ReadBytes((int)(HSize - ExtSize));
						HeaderSize += (int)HSize - ExtSize;
						throw new Exception("Too many bytes");
					}
				}

				long Length = EntryReader.BaseStream.Length - EntryReader.BaseStream.Position;
				Remainder = EntryReader.ReadBytes((int)Length);
				if (Remainder.Length > 0)
				{
					HeaderSize += Remainder.Length;
				}
			}
			if (HeaderSize != Header.Length)
			{
				throw new Exception("Size Mismatch");
			}
		}

		public void Write(string SongName, string Directory)
		{
			MemoryStream EntryStream = new();
			EndianWriter EntryWriter = new(EntryStream, EndianWriter.Endianness.Little);
			EntryWriter.Write((ushort)Format.Encoding);
			EntryWriter.Write((ushort)Format.Channels);
			EntryWriter.Write(Format.SampleRate);
			EntryWriter.Write(Format.AvgBytesPerSecond);
			EntryWriter.Write((ushort)Format.BlockAlignment);
			EntryWriter.Write((ushort)Format.BitDepth);

			if (HSize != null)
			{
				EntryWriter.Write((ushort)HSize);
				if (Format.Encoding == EncodingType.XMA2)
				{
					if (Format.Channels == 1 & Parameters.XMAChannelMask == 1)
					{
						Parameters.XMAChannelMask = 0;
					}
					EntryWriter.Write(Parameters.XMANumStreams);
					EntryWriter.Write(Parameters.XMAChannelMask);
					EntryWriter.Write(Parameters.XMASamplesEncoded);
					EntryWriter.Write(Parameters.XMABytesPBlock);
					EntryWriter.Write(Parameters.XMAPlayStart);
					EntryWriter.Write(Parameters.XMAPlayLength);
					EntryWriter.Write(Parameters.XMALoopStart);
					EntryWriter.Write(Parameters.XMALoopLength);
					EntryWriter.Write(Parameters.XMALoopCount);
					EntryWriter.Write(Parameters.XMAEncoderVer);
					EntryWriter.Write(Parameters.XMABlockCount);
				}
				else if (Format.Encoding == EncodingType.Extensible)
				{
					EntryWriter.Write(ExtValidBitRate);
					EntryWriter.Write(ExtChannelMask);
					EntryWriter.Write(ExtSubfmt.ToByteArray());
					if (ExtRemainder.Length > 0)
					{
						EntryWriter.Write(ExtRemainder);
					}
				}
				if (Remainder.Length > 0)
				{
					EntryWriter.Write(Remainder);
				}
			}
			Header = EntryStream.ToArray();

			if (Dpds != null && Dpds.Length > 0)
			{
				DpdsSize = Dpds.Length;
			}

			if (Seek != null && Seek.Length > 0)
			{
				SeekSize = Seek.Length;
			}

			MemoryStream WaveStream = new();
			EndianWriter StreamWriter = new(WaveStream, EndianWriter.Endianness.Little);
			if (Format.Encoding == EncodingType.WMAudio2 | Format.Encoding == EncodingType.WMAudio3)
			{
				RIFF = Encoding.ASCII.GetBytes("XWMA");
			}

			int FullSize = 20 + Header.Length + Data.Length;
			if (DpdsSize != null)
			{
				FullSize += 8 + (int)DpdsSize;
			}
			if (1 == 2) // if SeekSize != null
			{
				FullSize += 8 + (int)SeekSize;
			}
			StreamWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
			StreamWriter.Write((uint)FullSize);
			StreamWriter.Write(RIFF);

			WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("fmt "), Header);

			if (Dpds != null && Dpds.Length > 0)
			{
				WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("dpds"), Dpds);
			}

			if (Seek != null && Seek.Length > 0)
			{
				// Unimplemented until I fix up WaveSeek tables, but this weird xWMA WaveFormat works just fine for Terraria.
				/*
				StreamWriter.Write(Encoding.ASCII.GetBytes("WaveSeek"));
				StreamWriter.Write((uint)Seek.Length);
				byte[] result = new byte[Seek.Length * sizeof(uint)];
				Buffer.BlockCopy(Seek, 0, result, 0, result.Length);
				StreamWriter.Write(result);
                */
			}
			WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("data"), Data);

			string Target = string.Format("{0}/{1}", Directory, SongName);
			string BaseName = Target;
			string Extension = "O.wav";

			if (Format.Encoding == EncodingType.XMA2)
			{
				Extension = "O.xma";
			}
			else if (Format.Encoding == EncodingType.WMAudio2 | Format.Encoding == EncodingType.WMAudio3)
			{
				Extension = "O.xwma";
			}
			Target += Extension;

			using (FileStream WaveFile = new(Target, FileMode.Create, FileAccess.Write))
			{
				WaveStream.WriteTo(WaveFile);
			}

			if (Format.Encoding == EncodingType.XMA2)
			{
				Process Converter = new();
				Converter.StartInfo.FileName = "xma2encode.exe";
				Converter.StartInfo.Arguments = string.Format("{0} /DecodeToPCM {1}", "\"" + Target + "\"", "\"" + Target[..^4] + ".wav" + "\"");
				Converter.StartInfo.UseShellExecute = false;
				Converter.StartInfo.RedirectStandardOutput = true;
				Converter.Start();
				Converter.WaitForExit();
				File.Delete(Target);
			}

			if (Format.Encoding == EncodingType.WMAudio2 | Format.Encoding == EncodingType.WMAudio3)
			{
				Process Converter = new();
				Converter.StartInfo.FileName = "xwmaencode.exe";
				Converter.StartInfo.Arguments = string.Format("{0} {1}", "\"" + Target + "\"", "\"" + Target[..^5] + ".wav" + "\"");
				Converter.StartInfo.UseShellExecute = false;
				Converter.StartInfo.RedirectStandardOutput = true;
				Converter.Start();
				Converter.WaitForExit();
				File.Delete(Target);
			}

			WaveData = new WaveFileReader(BaseName + "O.wav");
			using (var Writer = new BinaryWriter(File.OpenWrite(BaseName + ".wav")))
			{
				ConvertAndWrite(WaveData, Writer);
				WaveData.Close();
				Writer.Close();
			}
			File.Delete(BaseName + "O.wav");
		}

		private void ConvertAndWrite(WaveStream WaveData, BinaryWriter Writer)
		{
			long Start = Writer.BaseStream.Position;
			long End;
			int FactData = 0;
			int TempIdx, BytesRead;
			ushort SamplesPerBlock = (ushort)(((BlockAlignment - 7 * ChannelCount) * 8 / (4 * ChannelCount)) + 2);
			uint DataRate = SampleRate * BlockAlignment / SamplesPerBlock;

			Writer.BaseStream.Position += 0x5A;
			while ((BytesRead = ((IWaveProvider)WaveData).Read(TempBuffer, 0, TempBuffer.Length)) > 0)
			{
				FactData += BytesRead;

				TempIdx = 0;
				for (int i = 0; i < SampleBufferP.Length; i++)
				{
					SampleBufferP[i] = TempBuffer[TempIdx++];
					SampleBufferP[i] += (short)(TempBuffer[TempIdx++] << 8);
				}

				EncodeBlock(SampleBufferP, ref SampleBufferA);

				for (int i = 0; i < BlockAlignment; i++)
					Writer.Write(SampleBufferA[i]);
			}
			End = Writer.BaseStream.Position;
			FactData /= 4;

			Writer.BaseStream.Position = Start;

			int Blocks = FactData / SamplesPerBlock;
			int SampsInLastBlock = FactData % SamplesPerBlock;
			if (SampsInLastBlock != 0)
				Blocks++;

			uint DataSize = (uint)(Blocks * BlockAlignment);
			uint FileSize = 82 + DataSize;

			Writer.Write(Encoding.ASCII.GetBytes("RIFF"));
			Writer.Write(FileSize);
			Writer.Write(Encoding.ASCII.GetBytes("WAVE"));
			Writer.Write(Encoding.ASCII.GetBytes("fmt "));
			Writer.Write(50); // Format Size
			Writer.Write((ushort)2); // MS-ADPCM ID

			Writer.Write(ChannelCount);
			Writer.Write(SampleRate);
			Writer.Write(DataRate);
			Writer.Write((ushort)BlockAlignment);

			Writer.Write((ushort)4);
			Writer.Write((ushort)32);

			Writer.Write(SamplesPerBlock);
			Writer.Write((ushort)ADPCMCoEfficients.Length);
			for (int PairIdx = 0; PairIdx < ADPCMCoEfficients.Length; PairIdx++)
			{
				Writer.Write((short)ADPCMCoEfficients[PairIdx].X);
				Writer.Write((short)ADPCMCoEfficients[PairIdx].Y);
			}

			Writer.Write(Encoding.ASCII.GetBytes("fact"));
			Writer.Write((uint)4); // Fact Size
			Writer.Write((uint)FactData);
			Writer.Write(Encoding.ASCII.GetBytes("data"));
			Writer.Write(DataSize);

			Writer.BaseStream.Position = End;
		}

		public static void WriteChunk(EndianWriter Writer, byte[] Name, byte[] RawData)
		{
			Writer.Write(Name);
			Writer.Write((uint)RawData.Length);
			Writer.Write(RawData);
		}

		public void EncodeBlock(short[] DecodedBuffer, ref byte[] EncodedBuffer)
		{
			int Encoded = 0;
			int Decoded = 0;
			short[] PredBase1 = new short[(DecodedBuffer.Length / 2) - 4];
			short[] PredBase2 = new short[(DecodedBuffer.Length / 2) - 4];

			State[] ADPCMStates =
			[
				new State(),
				new State()
			];

			for (int TestSample = 0; TestSample < (DecodedBuffer.Length / 2) - 4; ++TestSample)
			{
				PredBase1[TestSample] = DecodedBuffer[2 * TestSample];
				PredBase2[TestSample] = DecodedBuffer[2 * TestSample + 1];
			}

			int BestPredictIdx1 = GetBestPredictorIndex(PredBase1, ref ADPCMStates[0]);
			int BestPredictIdx2 = GetBestPredictorIndex(PredBase2, ref ADPCMStates[1]);

			ADPCMStates[0].CoEff1 = ADPCMCoEfficients[BestPredictIdx1].X;
			ADPCMStates[0].CoEff2 = ADPCMCoEfficients[BestPredictIdx1].Y;
			ADPCMStates[1].CoEff1 = ADPCMCoEfficients[BestPredictIdx2].X;
			ADPCMStates[1].CoEff2 = ADPCMCoEfficients[BestPredictIdx2].Y;

			EncodedBuffer[Encoded++] = (byte)BestPredictIdx1;
			EncodedBuffer[Encoded++] = (byte)BestPredictIdx2;

			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Delta & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Delta >> 8);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Delta & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Delta >> 8);

			ADPCMStates[0].Sample2 = DecodedBuffer[Decoded++];
			ADPCMStates[1].Sample2 = DecodedBuffer[Decoded++];

			ADPCMStates[0].Sample1 = DecodedBuffer[Decoded++];
			ADPCMStates[1].Sample1 = DecodedBuffer[Decoded++];

			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Sample1 & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Sample1 >> 8);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Sample1 & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Sample1 >> 8);

			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Sample2 & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[0].Sample2 >> 8);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Sample2 & 0xff);
			EncodedBuffer[Encoded++] = (byte)(ADPCMStates[1].Sample2 >> 8);

			for (; Encoded < EncodedBuffer.Length;)
			{
				byte Rem1 = EncodeSample(DecodedBuffer[Decoded++], ref ADPCMStates[0]);
				byte Rem2 = EncodeSample(DecodedBuffer[Decoded++], ref ADPCMStates[1]);

				EncodedBuffer[Encoded++] = (byte)((Rem1 << 4) | Rem2);
			}
		}

		private byte EncodeSample(short Sample, ref State ADPCM)
		{
			int Predictor = (ADPCM.Sample1 * ADPCM.CoEff1 + ADPCM.Sample2 * ADPCM.CoEff2) >> 8;
			int Remainder = Sample - Predictor;
			int Bias = ADPCM.Delta / 2;

			if (Remainder < 0)
			{
				Bias = -Bias;
			}

			Remainder = (Remainder + Bias) / ADPCM.Delta;
			Remainder = Math.Clamp(Remainder, -8, 7) & 0xf;

			Predictor += (((Remainder & 0x8) != 0) ? (Remainder - 0x10) : Remainder) * ADPCM.Delta;

			ADPCM.Sample2 = ADPCM.Sample1;
			ADPCM.Sample1 = (short)Math.Clamp(Predictor, short.MinValue, short.MaxValue);
			ADPCM.Delta = (AdaptionTable[Remainder] * ADPCM.Delta) >> 8;

			if (ADPCM.Delta < 16)
			{
				ADPCM.Delta = 16;
			}

			return (byte)Remainder;
		}

		private static int GetBestPredictorIndex(short[] Samples, ref State ADPCM)
		{
			int NumSamples = Samples.Length;

			int CoEffNum = ADPCMCoEfficients.Length;

			int BestPredictIdx = 0;
			int BestPredictErr = int.MaxValue;
			for (int PairIdx = 0; PairIdx < CoEffNum; PairIdx++)
			{
				int CoEff1 = ADPCMCoEfficients[PairIdx].X;
				int CoEff2 = ADPCMCoEfficients[PairIdx].Y;
				int CurrentPredictErr = 0;

				for (int i = 2; i < NumSamples; i++)
				{
					int ErrorValue = Math.Abs(Samples[i] - ((CoEff1 * Samples[i - 1] + CoEff2 * Samples[i - 2]) >> 8));
					CurrentPredictErr += ErrorValue;
				}

				CurrentPredictErr /= 4 * NumSamples;

				if (CurrentPredictErr < BestPredictErr)
				{
					BestPredictErr = CurrentPredictErr;
					BestPredictIdx = PairIdx;
				}

				if (CurrentPredictErr == 0)
				{
					break;
				}
			}

			if (BestPredictErr < 16)
			{
				BestPredictErr = 16;
			}

			ADPCM.Delta = BestPredictErr;
			return BestPredictIdx;
		}
	}

	internal struct State
	{
		internal int Delta;
		internal int CoEff1;
		internal int CoEff2;
		internal short Sample1;
		internal short Sample2;
	}
}
