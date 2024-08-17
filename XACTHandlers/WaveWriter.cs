using System.Diagnostics;
using System.Text;

namespace TCCS.XACTHandlers
{
    public class WaveWriter
    {
        public enum WaveFormat
        {
            Unknown = 0x0000,
            PCM = 0x0001,
            ADPCM = 0x0002,
            WMAudio2 = 0x0161,
            WMAudio3 = 0x0162,
            XMA = 0x0165,
            XMA2 = 0x0166,
            Extensible = 0xFFFE,
            Dev = 0xFFFF
        }

		public byte[] Header, Data, Dpds, Seek, Remainder, ExtRemainder = Array.Empty<byte>();
        public int HeaderSize = 16;
        public int XMA2Size = 34;
        public int ExtSize = 22;
        public int? DpdsSize = null;
        public int? SeekSize = null;
        public ushort? HSize = null;

        public ushort ExtValidBitsPSample;
        public uint ExtChannelMask;

        public byte[] ExtSubfmtBytes;
        public byte[] RIFF = Encoding.ASCII.GetBytes("WAVE");

        public XMAData Parameters = new();
        public Guid ExtSubfmt;
        public CurrentWBEntry WaveEntry = new();

        public WaveWriter(XWBHandler.WBEntry WaveBank, EndianReader.Endianness Endian)
        {
            Header = WaveBank.Header;
            Data = WaveBank.Data;
            Dpds = WaveBank.Dpds;
            Seek = WaveBank.Seek;

            MemoryStream EntryDetails = new(Header);
            EndianReader EntryReader = new(EntryDetails, Endian);

            WaveEntry.CurrentFormatTag = EntryReader.ReadUInt16();
            WaveEntry.CurrentChannels = EntryReader.ReadUInt16();
            WaveEntry.CurrentSamplesPSec = EntryReader.ReadUInt32();
            WaveEntry.CurrentAvgBytesPSec = EntryReader.ReadUInt32();
            WaveEntry.CurrentBlockAlignment = EntryReader.ReadUInt16();
            WaveEntry.CurrentBitsPSample = EntryReader.ReadUInt16();


            if (Header.Length >= HeaderSize + 2)
            {
                HSize = EntryReader.ReadUInt16();
                HeaderSize += 2;

                if (WaveEntry.CurrentFormatTag == (int)WaveFormat.XMA2)
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
                else if (WaveEntry.CurrentFormatTag == (int)WaveFormat.Extensible)
                {
                    if (HSize < ExtSize)
                    {
                        throw new Exception("Invalid Size");
                    }
                    ExtValidBitsPSample = EntryReader.ReadUInt16();
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

        public void Write(int Index, string SongName, string Directory)
        {
            MemoryStream EntryStream = new();
            EndianWriter EntryWriter = new(EntryStream, EndianWriter.Endianness.Little);
            EntryWriter.Write((ushort)WaveEntry.CurrentFormatTag);
            EntryWriter.Write((ushort)WaveEntry.CurrentChannels);
            EntryWriter.Write(WaveEntry.CurrentSamplesPSec);
            EntryWriter.Write(WaveEntry.CurrentAvgBytesPSec);
            EntryWriter.Write((ushort)WaveEntry.CurrentBlockAlignment);
            EntryWriter.Write((ushort)WaveEntry.CurrentBitsPSample);

            if (HSize != null)
            {
                EntryWriter.Write((ushort)HSize);
                if (WaveEntry.CurrentFormatTag == (uint)WaveFormat.XMA2)
                {
                    if (WaveEntry.CurrentChannels == 1 & Parameters.XMAChannelMask == 1)
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
                else if (WaveEntry.CurrentFormatTag == (uint)WaveFormat.Extensible)
                {
                    EntryWriter.Write(ExtValidBitsPSample);
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

            if (Dpds.Length > 0)
            {
                DpdsSize = Dpds.Length;
            }

            if (Seek.Length > 0)
            {
                SeekSize = Seek.Length;
            }

            MemoryStream WaveStream = new();
            EndianWriter StreamWriter = new(WaveStream, EndianWriter.Endianness.Little);
            if (WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio2 | WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio3)
            {
                RIFF = Encoding.ASCII.GetBytes("XWMA");
            }

			int FullSize = 20 + Header.Length + Data.Length;
			if (DpdsSize != null)
			{
				FullSize += 8 + (int)DpdsSize;
			}
			if (SeekSize != null)
			{
				FullSize += 8 + (int)SeekSize;
			}
			StreamWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
			StreamWriter.Write((uint)FullSize);
			StreamWriter.Write(RIFF);

			WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("fmt "), Header);

            if (Dpds.Length > 0)
            {
				WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("dpds"), Dpds);
            }

            if (Seek.Length > 0)
            {
				WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("seek"), Seek);
            }
			WriteChunk(StreamWriter, Encoding.ASCII.GetBytes("data"), Data);

            string Target = string.Format("{0}/{1}-{2}", Directory, Index, SongName);

            if (WaveEntry.CurrentFormatTag == (int)WaveFormat.XMA2)
            {
				Target += ".xma";
            }
            else if (WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio2 | WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio3) 
            {
                Target += ".xwma";
            }
            else
            {
                Target += ".wav";
            }

            using (FileStream WaveFile = new(Target, FileMode.Create, FileAccess.Write))
            {
                WaveStream.WriteTo(WaveFile);
            }

			if (WaveEntry.CurrentFormatTag == (int)WaveFormat.XMA2)
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

			if (WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio2 | WaveEntry.CurrentFormatTag == (int)WaveFormat.WMAudio3)
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
        }

        public static void WriteChunk(EndianWriter Writer, byte[] Name, byte[] RawData)
        {
            Writer.Write(Name);
            Writer.Write((uint)RawData.Length);
            Writer.Write(RawData);
        }
    }
}
