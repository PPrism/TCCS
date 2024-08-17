using System.Buffers.Binary;
using System.Text;

namespace TCCS.XACTHandlers
{
    public class EndianWriter : BinaryWriter
    {
        public enum Endianness
        {
            Little,
            Big,
        }

        private readonly Endianness _Endian = Endianness.Little;

        public EndianWriter() : base()
        {
        }

        public EndianWriter(Endianness Endian) : base()
        {
            _Endian = Endian;
        }

		public EndianWriter(Stream Input) : base(Input)
		{
		}

		public EndianWriter(Stream Input, Endianness Endian) : base(Input)
		{
			_Endian = Endian;
		}

		public EndianWriter(Stream Input, Encoding Type) : base(Input, Type)
		{
		}

		public EndianWriter(Stream Input, Encoding Type, Endianness Endian) : base(Input, Type)
		{
			_Endian = Endian;
		}

		public EndianWriter(Stream Input, Encoding Type, bool LeaveOpen) : base(Input, Type, LeaveOpen)
		{
		}

		public EndianWriter(Stream Input, Encoding Type, bool LeaveOpen, Endianness Endian) : base(Input, Type, LeaveOpen)
		{
			_Endian = Endian;
		}

		public override void Write(float Value) => Write(Value, _Endian);

		public void Write(float Value, Endianness Endian)
		{
			Span<byte> Buffer = stackalloc byte[sizeof(float)];
			if (Endian == Endianness.Little)
			{
				BinaryPrimitives.WriteSingleLittleEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
			else
			{
				BinaryPrimitives.WriteSingleBigEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
		}

		public override void Write(short Value) => Write(Value, _Endian);

        public void Write(short Value, Endianness Endian)
        {
            Span<byte> Buffer = stackalloc byte[sizeof(short)];
            if (Endian == Endianness.Little)
            {
                BinaryPrimitives.WriteInt16LittleEndian(Buffer, Value);
                OutStream.Write(Buffer);
            }
            else
            {
                BinaryPrimitives.WriteInt16BigEndian(Buffer, Value);
                OutStream.Write(Buffer);
            }
        }

		public override void Write(ushort Value) => Write(Value, _Endian);

		public void Write(ushort Value, Endianness Endian)
		{
			Span<byte> Buffer = stackalloc byte[sizeof(ushort)];
			if (Endian == Endianness.Little)
			{
				BinaryPrimitives.WriteUInt16LittleEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
			else
			{
				BinaryPrimitives.WriteUInt16BigEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
		}
		public override void Write(int Value) => Write(Value, _Endian);

		public void Write(int Value, Endianness Endian)
		{
			Span<byte> Buffer = stackalloc byte[sizeof(int)];
			if (Endian == Endianness.Little)
			{
				BinaryPrimitives.WriteInt32LittleEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
			else
			{
				BinaryPrimitives.WriteInt32BigEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
		}

		public override void Write(uint Value) => Write(Value, _Endian);

		public void Write(uint Value, Endianness Endian)
		{
			Span<byte> Buffer = stackalloc byte[sizeof(uint)];
			if (Endian == Endianness.Little)
			{
				BinaryPrimitives.WriteUInt32LittleEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
			else
			{
				BinaryPrimitives.WriteUInt32BigEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
		}

		public override void Write(long Value) => Write(Value, _Endian);

		public void Write(long Value, Endianness Endian)
		{
			Span<byte> Buffer = stackalloc byte[sizeof(long)];
			if (Endian == Endianness.Little)
			{
				BinaryPrimitives.WriteInt64LittleEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
			else
			{
				BinaryPrimitives.WriteInt64BigEndian(Buffer, Value);
				OutStream.Write(Buffer);
			}
		}

		public override void Write(ulong Value) => Write(Value, _Endian);

        public void Write(ulong Value, Endianness Endian)
        {
            Span<byte> Buffer = stackalloc byte[sizeof(ulong)];
            if (Endian == Endianness.Little)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(Buffer, Value);
                OutStream.Write(Buffer);
            }
            else
            {
                BinaryPrimitives.WriteUInt64BigEndian(Buffer, Value);
                OutStream.Write(Buffer);
            }
        }
    }
}
