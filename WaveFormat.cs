using static TCCS.Handlers.XWBHandler;

namespace TCCS
{
	public class WaveFormat
	{
		protected EncodingType EncodingTag;
		protected short ChannelCount;
		protected int SamplesPerSec;
		protected int AvgBytesPerSec;
		protected short Alignment;
		protected short BitsPerSample;
		protected byte[] ExtraHeader;

		public WaveFormat() : this(44100, 16, 2) { } // Configuration for relevant songs

		public WaveFormat(int SampleRate, int BitDepth, int Channels)
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(Channels, 1);

			EncodingTag = BitDepth < 32 ? EncodingType.PCM : EncodingType.IEEE;
			ChannelCount = (short)Channels;
			SamplesPerSec = SampleRate;
			Alignment = (short)(Channels * (BitDepth / 8));
			AvgBytesPerSec = SamplesPerSec * Alignment;
			BitsPerSample = (short)BitDepth;
			ExtraHeader = [];
		}

		public static WaveFormat SetupNonPCM(EncodingType Encoding, int SampleRate, int Channels, int ByteRate, int BlockAlignment, int BitDepth, byte[] AddedHeader)
		{
			var Format = new WaveFormat
			{
				EncodingTag = Encoding,
				ChannelCount = (short)Channels,
				SamplesPerSec = SampleRate,
				Alignment = (short)BlockAlignment,
				AvgBytesPerSec = ByteRate,
				BitsPerSample = (short)BitDepth,
				ExtraHeader = AddedHeader
			};
			return Format;
		}

		public EncodingType Encoding
		{
			get
			{
				return EncodingTag;
			}
		}

		public int Channels
		{
			get
			{
				return ChannelCount;
			}
		}

		public int SampleRate
		{
			get
			{
				return SamplesPerSec;
			}
		}

		public int BlockAlignment
		{
			get
			{
				return Alignment;
			}
		}

		public int AvgBytesPerSecond
		{
			get
			{
				return AvgBytesPerSec;
			}
		}

		public int BitDepth
		{
			get
			{
				return BitsPerSample;
			}
		}

		public byte[] ExtraSize
		{
			get
			{
				return ExtraHeader;
			}
		}
	}
}
