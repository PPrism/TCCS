using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using static TCCS.XACTHandlers.EndianReader;

namespace TCCS.XACTHandlers
{
	public class XGSHandler
	{
		public struct AudioCategory
		{
			public byte InstanceLimit;
			public ushort FadeInMS;
			public ushort FadeOutMS;
			public byte MaxInstanceBehaviour;
			public short ParentCategory;
			public float Volume;
			public byte Visibility;
			public byte InstanceCount;
			public float CurVolume;
		}

		public struct AudioVariable
		{
			public byte Accessibility;
			public float InitialVal;
			public float MinVal;
			public float MaxVal;
		}

		public struct RPCPoint
		{
			public float X;
			public float Y;
			public byte Type;
		}

		public struct AudioRPC
		{
			public ushort Variable;
			public byte PointCount;
			public ushort Parameter;
			public RPCPoint[] Points;
		}

		private readonly Stream OldAEStream;
		private readonly Stream NewAEStream;

		private static readonly byte[] LittleMagic = Encoding.ASCII.GetBytes("XGSF");
		private static Endianness OldFileEndian = Endianness.Big;

		public List<string> Songs = new();
		public AudioCategory[] AudioCategories;
		public AudioVariable[] AudioVariables;
		public AudioRPC[] AudioRPCs;
		public uint[] RPCCodes;
		public string[] CategoryNames, VariableNames;

		public byte[] AESignature { get; set; }

		public ushort CategoryCount, VariableCount, RPCCount, Blob1Count, Blob2Count;

		public XGSHandler(string Filename, string NewPath)
		{
			// The entire point of this is to convert the normally big-endian audio engine files used by the game into little-endian files.
			// This is due to FNA having some weird bug with big-endian audio engines where the music can play, but volume control is non-existent.
			// Until this is fixed, we got this mess to convert them for the most part (I'ma leave the unique stuff, like the header).
			// Btw, I based this, XSB, and the XWB handler off an old C# project I had so some parts might look a bit mad.
			OldAEStream = File.Open(Filename, FileMode.Open);
			using EndianReader Reader = new(OldAEStream, Encoding.Default, false);

			AESignature = Reader.ReadBytes(LittleMagic.Length);
			if (AESignature.SequenceEqual(LittleMagic))
			{
				OldFileEndian = Endianness.Little;
			}
			Reader.BaseStream.Position = 0;

			NewAEStream = new FileStream(NewPath, FileMode.Create, FileAccess.ReadWrite);
			using EndianWriter Writer = new(NewAEStream, Encoding.Default, false);
			WriteSwap(Reader, Writer);
		}

		private void WriteSwap(EndianReader BaseReader, EndianWriter CurrentWriter)
		{
			CurrentWriter.Write(BaseReader.EndianHandler(AESignature.Length, OldFileEndian));
			CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian)); // Version
			CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian)); // Header Version
			CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian));
			CurrentWriter.Write(BaseReader.ReadUInt64(OldFileEndian));
			CurrentWriter.Write(BaseReader.ReadByte()); // XACT Version

			CategoryCount = BaseReader.ReadUInt16(OldFileEndian);
			CurrentWriter.Write(CategoryCount);
			VariableCount = BaseReader.ReadUInt16(OldFileEndian);
			CurrentWriter.Write(VariableCount);
			Blob1Count = BaseReader.ReadUInt16(OldFileEndian);
			CurrentWriter.Write(Blob1Count);
			Blob2Count = BaseReader.ReadUInt16(OldFileEndian);
			CurrentWriter.Write(Blob2Count);
			RPCCount = BaseReader.ReadUInt16(OldFileEndian);
			CurrentWriter.Write(RPCCount);
			CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian)); // DSP Preset Count
			CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian)); // DSP Parameter Count

			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Category Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Variable Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Blob 1 Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Category Name Idx Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Blob 2 Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Variable Name Idx Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Category Name Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // Variable Name Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // RPC Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // DSP Preset Offset
			CurrentWriter.Write(BaseReader.ReadUInt32(OldFileEndian)); // DSP Parameter Offset

			for (int CatIdx = 0; CatIdx < CategoryCount; CatIdx++)
			{
				CurrentWriter.Write(BaseReader.ReadByte());
				CurrentWriter.Write(BinaryPrimitives.ReverseEndianness(BaseReader.ReadUInt16(OldFileEndian)));
				CurrentWriter.Write(BinaryPrimitives.ReverseEndianness(BaseReader.ReadUInt16(OldFileEndian)));
				CurrentWriter.Write(BaseReader.ReadByte());
				CurrentWriter.Write(BinaryPrimitives.ReverseEndianness(BaseReader.ReadInt16(OldFileEndian)));
				CurrentWriter.Write(BaseReader.ReadByte());
				CurrentWriter.Write(BaseReader.ReadByte());
			}

			for (int VarIdx = 0; VarIdx < VariableCount; VarIdx++)
			{
				CurrentWriter.Write(BaseReader.ReadByte());
				CurrentWriter.Write(BaseReader.ReadSingle(OldFileEndian));
				CurrentWriter.Write(BaseReader.ReadSingle(OldFileEndian));
				CurrentWriter.Write(BaseReader.ReadSingle(OldFileEndian));
			}

			if (RPCCount > 0)
			{
				for (int RPCIdx = 0; RPCIdx < RPCCount; RPCIdx++)
				{
					CurrentWriter.Write(BaseReader.ReadUInt16(OldFileEndian));
					byte PointCount = BaseReader.ReadByte();
					CurrentWriter.Write(PointCount);
					CurrentWriter.Write(BinaryPrimitives.ReverseEndianness(BaseReader.ReadUInt16(OldFileEndian)));
					for (int PointIdx = 0; PointIdx < PointCount; PointIdx++)
					{
						CurrentWriter.Write(BaseReader.ReadSingle(OldFileEndian));
						CurrentWriter.Write(BaseReader.ReadSingle(OldFileEndian));
						CurrentWriter.Write(BaseReader.ReadByte());
					}
				}
			}

			// DSP Information is not needed for Terraria.

			for (int BlobIdx = 0; BlobIdx < Blob1Count * 2; BlobIdx++)
			{
				CurrentWriter.Write((byte)0);
				BaseReader.BaseStream.Position++;
			}

			for (int CatIdx = 0; CatIdx < CategoryCount * 6; CatIdx++)
			{
				CurrentWriter.Write((byte)0);
				BaseReader.BaseStream.Position++;
			}

			for (int CatIdx = 0; CatIdx < CategoryCount; CatIdx++)
			{
				while (BaseReader.PeekChar() >= 0)
				{
					char Character = BaseReader.ReadChar();
					CurrentWriter.Write(Character);

					if (Character == '\0')
					{
						break;
					}
				}
			}

			for (int BlobIdx = 0; BlobIdx < Blob2Count * 2; BlobIdx++)
			{
				CurrentWriter.Write((byte)0);
				BaseReader.BaseStream.Position++;
			}

			for (int VarIdx = 0; VarIdx < VariableCount * 6; VarIdx++)
			{
				CurrentWriter.Write((byte)0);
				BaseReader.BaseStream.Position++;
			}

			for (int VarIdx = 0; VarIdx < VariableCount; VarIdx++)
			{
				while (BaseReader.PeekChar() >= 0)
				{
					char Character = BaseReader.ReadChar();
					CurrentWriter.Write(Character);

					if (Character == '\0')
					{
						break;
					}
				}
			}
		}

		public void Read(EndianReader Reader) // Modelled after FNA's FAudio
		{
			AESignature = Reader.ReadBytes(LittleMagic.Length);

			if (AESignature.SequenceEqual(LittleMagic))
			{
				OldFileEndian = Endianness.Little;
			}

			Reader.ReadUInt16(OldFileEndian); // Version
			Reader.ReadUInt16(OldFileEndian); // Header Version
			Reader.ReadUInt16(OldFileEndian);
			Reader.ReadUInt64(OldFileEndian);
			Reader.ReadByte(); // XACT Version
			CategoryCount = Reader.ReadUInt16(OldFileEndian);
			VariableCount = Reader.ReadUInt16(OldFileEndian);
			Blob1Count = Reader.ReadUInt16(OldFileEndian);
			Blob2Count = Reader.ReadUInt16(OldFileEndian);
			RPCCount = Reader.ReadUInt16(OldFileEndian);
			Reader.ReadUInt16(OldFileEndian); // DSP Preset Count
			Reader.ReadUInt16(OldFileEndian); // DSP Parameter Count

			Reader.ReadUInt32(OldFileEndian); // Category Offset
			Reader.ReadUInt32(OldFileEndian); // Variable Offset
			Reader.ReadUInt32(OldFileEndian); // Blob 1 Offset
			Reader.ReadUInt32(OldFileEndian); // Category Name Idx Offset
			Reader.ReadUInt32(OldFileEndian); // Blob 2 Offset
			Reader.ReadUInt32(OldFileEndian); // Variable Name Idx Offset
			Reader.ReadUInt32(OldFileEndian); // Category Name Offset
			Reader.ReadUInt32(OldFileEndian); // Variable Name Offset
			Reader.ReadUInt32(OldFileEndian); // RPC Offsets
			Reader.ReadUInt32(OldFileEndian); // DSP Preset Offset
			Reader.ReadUInt32(OldFileEndian); // DSP Parameter Offset

			AudioCategories = new AudioCategory[CategoryCount];
			AudioVariables = new AudioVariable[VariableCount];
			CategoryNames = new string[CategoryCount];
			VariableNames = new string[VariableCount];

			for (int CatIdx = 0; CatIdx < CategoryCount; CatIdx++)
			{
				AudioCategories[CatIdx].InstanceLimit = Reader.ReadByte();
				AudioCategories[CatIdx].FadeInMS = Reader.ReadUInt16(OldFileEndian);
				AudioCategories[CatIdx].FadeOutMS = Reader.ReadUInt16(OldFileEndian);
				AudioCategories[CatIdx].MaxInstanceBehaviour = (byte)(Reader.ReadByte() >> 3);
				AudioCategories[CatIdx].ParentCategory = Reader.ReadInt16(OldFileEndian);

				AudioCategories[CatIdx].Volume = (float)Math.Pow(10, ((3969f * Math.Log10(Reader.ReadByte() / 28240f)) + 8715f) / 2000);
				AudioCategories[CatIdx].Visibility = Reader.ReadByte();
				AudioCategories[CatIdx].InstanceCount = 0;
				AudioCategories[CatIdx].CurVolume = 1f;
			}

			for (int VarIdx = 0; VarIdx < VariableCount; VarIdx++)
			{
				AudioVariables[VarIdx].Accessibility = Reader.ReadByte();
				AudioVariables[VarIdx].InitialVal = Reader.ReadSingle(OldFileEndian);
				// No need to do anything with the GlobalVarVals.
				AudioVariables[VarIdx].MinVal = Reader.ReadSingle(OldFileEndian);
				AudioVariables[VarIdx].MaxVal = Reader.ReadSingle(OldFileEndian);
			}

			if (RPCCount > 0)
			{  
				AudioRPCs = new AudioRPC[RPCCount + 1];
				RPCCodes = new uint[RPCCount + 1];
				for (int RPCIdx = 0; RPCIdx < RPCCount; RPCIdx++)
				{
					RPCCodes[RPCIdx] = (uint)Reader.BaseStream.Position;
					AudioRPCs[RPCIdx].Variable = Reader.ReadUInt16(OldFileEndian);
					AudioRPCs[RPCIdx].PointCount = Reader.ReadByte();
					AudioRPCs[RPCIdx].Parameter = Reader.ReadUInt16(OldFileEndian);
					AudioRPCs[RPCIdx].Points = new RPCPoint[AudioRPCs[RPCIdx].PointCount + 1];
					for (int PointIdx = 0; PointIdx < AudioRPCs[RPCIdx].PointCount; PointIdx++)
					{
						AudioRPCs[RPCIdx].Points[PointIdx].X = Reader.ReadSingle(OldFileEndian);
						AudioRPCs[RPCIdx].Points[PointIdx].Y = Reader.ReadSingle(OldFileEndian);
						AudioRPCs[RPCIdx].Points[PointIdx].Type = Reader.ReadByte();
					}
				}
			}

			// DSP Information is not needed for Terraria.

			Reader.BaseStream.Position += Blob1Count * 2;
			Reader.BaseStream.Position += CategoryCount * 6;

			for (int CatIdx = 0; CatIdx < CategoryCount; CatIdx++)
			{
				List<char> Characters = new();
				while (Reader.PeekChar() >= 0)
				{
					char Character = Reader.ReadChar();

					if (Character == '\0')
					{
						CategoryNames[CatIdx] = new string(Characters.ToArray());
						Characters.Clear();
						break;
					}

					Characters.Add(Character);
				}
			}

			Reader.BaseStream.Position += Blob2Count * 2;
			Reader.BaseStream.Position += VariableCount * 6;

			for (int VarIdx = 0; VarIdx < VariableCount; VarIdx++)
			{
				List<char> Characters = new();
				while (Reader.PeekChar() >= 0)
				{
					char Character = Reader.ReadChar();

					if (Character == '\0')
					{
						VariableNames[VarIdx] = new string(Characters.ToArray());
						Characters.Clear();
						break;
					}

					Characters.Add(Character);
				}
			}
		}
	}
}
