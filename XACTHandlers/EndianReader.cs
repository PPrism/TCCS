using System.Text;

namespace TCCS.XACTHandlers
{
    public class EndianReader : BinaryReader
    {
        public enum Endianness
        {
            Little,
            Big,
        }

        private readonly Endianness _Endian = Endianness.Little;

        public EndianReader(Stream Input) : base(Input) // Did up the rest of these and the writer; maybe someone will make use of these in another project.
        {
        }

		public EndianReader(Stream Input, Endianness Endian) : base(Input)
		{
			_Endian = Endian;
		}

		public EndianReader(Stream Input, Encoding Type) : base(Input, Type)
        {
        }

		public EndianReader(Stream Input, Encoding Type, Endianness Endian) : base(Input, Type)
		{
			_Endian = Endian;
		}

		public EndianReader(Stream Input, Encoding Type, bool LeaveOpen) : base(Input, Type, LeaveOpen)
        {
        }

        public EndianReader(Stream Input, Encoding Type, bool LeaveOpen, Endianness Endian) : base(Input, Type, LeaveOpen)
        {
            _Endian = Endian;
        }

        public override float ReadSingle() => ReadSingle(_Endian); // Why did I have this here again?

        public float ReadSingle(Endianness Endian) => BitConverter.ToSingle(EndianHandler(sizeof(float), Endian));

		public override short ReadInt16() => ReadInt16(_Endian);

		public short ReadInt16(Endianness Endian) => BitConverter.ToInt16(EndianHandler(sizeof(short), Endian));

		public override ushort ReadUInt16() => ReadUInt16(_Endian);

		public ushort ReadUInt16(Endianness Endian) => BitConverter.ToUInt16(EndianHandler(sizeof(ushort), Endian));

		public override int ReadInt32() => ReadInt32(_Endian);

		public int ReadInt32(Endianness Endian) => BitConverter.ToInt32(EndianHandler(sizeof(int), Endian));

		public override uint ReadUInt32() => ReadUInt32(_Endian);

		public uint ReadUInt32(Endianness Endian) => BitConverter.ToUInt32(EndianHandler(sizeof(uint), Endian));

		public override long ReadInt64() => ReadInt64(_Endian);

		public long ReadInt64(Endianness Endian) => BitConverter.ToInt64(EndianHandler(sizeof(long), Endian));

		public override ulong ReadUInt64() => ReadUInt64(_Endian);

		public ulong ReadUInt64(Endianness Endian) => BitConverter.ToUInt64(EndianHandler(sizeof(ulong), Endian));

        internal byte[] EndianHandler(int BytesToRead, Endianness Endian)
        {
            var Bytes = ReadBytes(BytesToRead);

            if ((Endian == Endianness.Little && !BitConverter.IsLittleEndian) || (Endian == Endianness.Big && BitConverter.IsLittleEndian))
            {
                Array.Reverse(Bytes);
            }

            return Bytes;
        }
    }
}
