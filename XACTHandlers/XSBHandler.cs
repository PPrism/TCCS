using System.Text;
using static TCCS.XACTHandlers.EndianReader;

namespace TCCS.XACTHandlers
{
	public class XSBHandler
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

		private readonly Stream SBStream;

		private static readonly byte[] LittleMagic = Encoding.ASCII.GetBytes("SDBK");
		private static Endianness FileEndian = Endianness.Big;

		public List<string> Songs = new();

		public byte[] SBSignature { get; set; }

		private int CueNamesTableOffset;
		private uint CueSet1Count, CueSet2Count, CueNamesLen;

		public XSBHandler(string Filename)
		{
			SBStream = File.Open(Filename, FileMode.Open);
			using EndianReader Reader = new(SBStream, Encoding.Default, false);
			Read(Reader);
		}

		public void Read(EndianReader Reader)
		{
			SBSignature = Reader.ReadBytes(LittleMagic.Length);

			if (SBSignature.SequenceEqual(LittleMagic))
			{
				FileEndian = Endianness.Little;
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

			Reader.ReadInt32(FileEndian); // Cue Set 1 Offset
			Reader.ReadInt32(FileEndian); // Cue Set 2 Offset
			Reader.ReadInt32(FileEndian); // Cue Names Offset
			Reader.ReadInt32(FileEndian); // Unknown
			Reader.ReadInt32(FileEndian); // Unknown

			Reader.ReadInt32(FileEndian); // Unknown
			Reader.ReadInt32(FileEndian); // Wave Bank Offset
			Reader.ReadInt32(FileEndian); // Cue Name Hash Offset
			CueNamesTableOffset = Reader.ReadInt32(FileEndian);
			Reader.ReadInt32(FileEndian); // Sounds Offset

			Reader.ReadChars(64); // Sound Bank Name

			// To-do: Something with the rest of this; right now we only extract the names.

			if (CueNamesLen > 0 && CueNamesTableOffset > 0)
			{
				SBNameTableEntry[] CueNameEntries = new SBNameTableEntry[CueSet1Count + CueSet2Count];
				string[] Names = new string[CueNameEntries.Length];
				Reader.BaseStream.Seek(CueNamesTableOffset, SeekOrigin.Begin);
				for (int i = 0; i < CueSet1Count + CueSet2Count; i++)
				{
					CueNameEntries[i].NameOffset = Reader.ReadInt32(FileEndian);
					CueNameEntries[i].Unknown = Reader.ReadInt16(FileEndian);
				}

				for (int j = 0; j < CueSet1Count + CueSet2Count; j++)
				{
					List<char> Characters = new();
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
				Songs = Names.ToList();
			}
		}
	}
}
