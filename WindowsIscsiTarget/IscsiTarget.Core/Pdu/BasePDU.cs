using System;

namespace IscsiTarget.Core.Pdu
{
    public abstract class BasePDU
    {
        // Common PDU Header Fields (as per RFC 7143)
        protected byte Opcode;
        protected byte ImmediateDelivery;
        protected byte FinalBit;
        protected byte TransitBit;
        protected byte ContinueBit;
        protected byte Reserved;
        protected byte TotalAHSLength;
        protected uint DataSegmentLength;
        protected uint InitiatorTaskTag;

        // Methods for handling network byte order (Big-Endian)
        protected uint ConvertToNetworkByteOrder(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                return ((value & 0x000000FF) << 24) |
                       ((value & 0x0000FF00) << 8) |
                       ((value & 0x00FF0000) >> 8) |
                       ((value & 0xFF000000) >> 24);
            }
            return value;
        }

        protected uint ConvertFromNetworkByteOrder(uint value)
        {
            return ConvertToNetworkByteOrder(value); // Same operation for both directions
        }

        // Abstract methods to be implemented by derived classes
        public abstract byte[] Serialize();
        public abstract void Deserialize(byte[] data);
    }
}