namespace ProtoLurker
{
    public static class Reader
    {
        public static int readVarInt(byte[] buffer, int offset = 0)
        {
            var value = (buffer[offset] & 127); if (buffer[offset++] < 128) return value;
            value = (value | (buffer[offset] & 127) << 7); if (buffer[offset++] < 128) return value;
            return value;
        }
    }

    public static class WireFormat
    {
        public enum WireType : uint
        {
            /// <summary>
            /// Variable-length integer.
            /// </summary>
            Varint = 0,
            /// <summary>
            /// A fixed-length 64-bit value.
            /// </summary>
            Fixed64 = 1,
            /// <summary>
            /// A length-delimited value, i.e. a length followed by that many bytes of data.
            /// </summary>
            LengthDelimited = 2,
            /// <summary>
            /// A "start group" value
            /// </summary>
            StartGroup = 3,
            /// <summary>
            /// An "end group" value
            /// </summary>
            EndGroup = 4,
            /// <summary>
            /// A fixed-length 32-bit value.
            /// </summary>
            Fixed32 = 5
        }

        private const int TagTypeBits = 3;
        private const uint TagTypeMask = (1 << TagTypeBits) - 1;

        /// <summary>
        /// Given a tag value, determines the wire type (lower 3 bits).
        /// </summary>
        public static WireType GetTagWireType(int tag)
        {
            return (WireType)(tag & TagTypeMask);
        }

        /// <summary>
        /// Given a tag value, determines the field number (the upper 29 bits).
        /// </summary>
        public static int GetTagFieldNumber(int tag)
        {
            return (int)(tag >> TagTypeBits);
        }

        /// <summary>
        /// Makes a tag value given a field number and wire type.
        /// </summary>
        public static uint MakeTag(int fieldNumber, WireType wireType)
        {
            return (uint)(fieldNumber << TagTypeBits) | (uint)wireType;
        }
    }
}
