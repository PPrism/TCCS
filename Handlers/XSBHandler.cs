using System.Text;
using TCCS.EndianUtils;
using static TCCS.EndianUtils.EndianReader;

namespace TCCS.Handlers
{
	public class XSBHandler : IDisposable
	{
		public struct SBNameTableEntry
		{
			public int NameOffset;
			public short Unknown;
			internal SBNameTableEntry(int Offset, short Data)
			{
				NameOffset = Offset;
				Unknown = Data;
			}
		}

		private readonly EndianReader Reader;
		private static readonly byte[] LittleMagic = Encoding.ASCII.GetBytes("SDBK");
		private Endianness FileEndian;

		public string[] Names = [];

		protected byte[] SBSignature { get; set; }

		private int CueNamesTableOffset;
		private uint CueSet1Count, CueSet2Count, CueNamesLen;

		public XSBHandler(Stream FileStream)
		{
			Reader = new EndianReader(FileStream);
			FileEndian = Endianness.Little;
			Read();
		}

		public void Dispose() => Reader.Close();

		private void Read()
		{
			SBSignature = Reader.ReadBytes(LittleMagic.Length);

			if (!SBSignature.SequenceEqual(LittleMagic))
			{
				byte[] SwappedSig = LittleMagic;
				Array.Reverse(SwappedSig);
				SBSignature = SwappedSig;
				FileEndian = Endianness.Big;
			}

			Reader.ReadUInt16(FileEndian); // Version
			Reader.ReadUInt16(FileEndian); // Header Version
			Reader.ReadUInt16(FileEndian); // CRC

			Reader.ReadUInt32(FileEndian); // LowBuild
			Reader.ReadUInt32(FileEndian); // HighBuild

			Reader.ReadByte(); // Platform ID

			CueSet1Count = Reader.ReadUInt16(FileEndian);
			CueSet2Count = Reader.ReadUInt16(FileEndian);
			Reader.ReadUInt16(FileEndian); // Unknown
			Reader.ReadUInt16(FileEndian); // Cue Name Hash Count
			Reader.ReadByte(); // # of Wave Banks
			Reader.ReadUInt16(FileEndian); // # of Sounds

			CueNamesLen = Reader.ReadUInt32(FileEndian);

			Reader.ReadInt32(FileEndian); // Cue Set 1 DataOffset
			Reader.ReadInt32(FileEndian); // Cue Set 2 DataOffset
			Reader.ReadInt32(FileEndian); // Cue Names DataOffset
			Reader.ReadInt32(FileEndian); // Unknown
			Reader.ReadInt32(FileEndian); // Unknown

			Reader.ReadInt32(FileEndian); // Unknown
			Reader.ReadInt32(FileEndian); // Wave Bank DataOffset
			Reader.ReadInt32(FileEndian); // Cue Name Hash DataOffset
			CueNamesTableOffset = Reader.ReadInt32(FileEndian);
			Reader.ReadInt32(FileEndian); // Sounds DataOffset

			Reader.ReadChars(64); // Sound Bank Name

			// To-do: Something with the rest of this; right now we only extract the WaveNames.

			if (CueNamesLen > 0 && CueNamesTableOffset > 0)
			{
				SBNameTableEntry[] CueNameEntries = new SBNameTableEntry[CueSet1Count + CueSet2Count];
				Names = new string[CueNameEntries.Length];
				Reader.BaseStream.Seek(CueNamesTableOffset, SeekOrigin.Begin);
				for (int i = 0; i < CueSet1Count + CueSet2Count; i++)
				{
					CueNameEntries[i].NameOffset = Reader.ReadInt32(FileEndian);
					CueNameEntries[i].Unknown = Reader.ReadInt16(FileEndian);
				}

				for (int j = 0; j < CueSet1Count + CueSet2Count; j++)
				{
					List<char> Characters = [];
					Reader.BaseStream.Seek(CueNameEntries[j].NameOffset, SeekOrigin.Begin);
					while (Reader.PeekChar() >= 0)
					{
						char Character = Reader.ReadChar();

						if (Character == '\0')
						{
							Names[j] = new string(Characters.ToArray());
							Characters.Clear();
							break;
						}

						Characters.Add(Character);
					}
				}
			}
		}
	}
}
