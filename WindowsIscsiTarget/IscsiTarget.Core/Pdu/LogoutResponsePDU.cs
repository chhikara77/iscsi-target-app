using System;
using System.IO;
using System.Net;

namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.16 Logout Response PDU
    public class LogoutResponsePDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes ---
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU)
        // FinalBit (from BasePDU) - Must be 1
        // TotalAHSLength (from BasePDU) - Must be 0
        // DataSegmentLength (from BasePDU) - Must be 0

        public byte Response { get; private set; } // Byte 1, bits 0-7
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU)
        public uint StatusSequenceNumber { get; private set; }         // Bytes 24-27: StatSN
        public uint ExpectedCommandSequenceNumber { get; private set; } // Bytes 28-31: ExpCmdSN
        public uint MaximumCommandSequenceNumber { get; private set; }  // Bytes 32-35: MaxCmdSN
        public ushort Time2Wait { get; private set; }                   // Bytes 40-41
        public ushort Time2Retain { get; private set; }                 // Bytes 42-43
        // Other bytes are reserved or handled by BasePDU

        public LogoutResponsePDU()
        {
            Opcode = (byte)PduOpcode.LogoutResponse;
            FinalBit = 1; // Must be 1 for Logout Response
            TotalAHSLength = 0; // Must be 0
            DataSegmentLength = 0; // Must be 0
        }

        public LogoutResponsePDU(byte response, uint initiatorTaskTag, uint statSN, uint expCmdSN, uint maxCmdSN, ushort time2Wait, ushort time2Retain)
            : this()
        {
            Response = response;
            InitiatorTaskTag = initiatorTaskTag;
            StatusSequenceNumber = statSN;
            ExpectedCommandSequenceNumber = expCmdSN;
            MaximumCommandSequenceNumber = maxCmdSN;
            Time2Wait = time2Wait;
            Time2Retain = time2Retain;
        }

        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48)
                throw new ArgumentException("Buffer too short for LogoutResponsePDU BHS");

            base.Deserialize(buffer); // Handles first 8 bytes (Opcode, Flags, TotalAHSLength, DataSegmentLength)

            // Validate constraints for Logout Response
            if (FinalBit != 1)
                throw new ArgumentException("FinalBit must be 1 for LogoutResponsePDU");
            if (TotalAHSLength != 0)
                throw new ArgumentException("TotalAHSLength must be 0 for LogoutResponsePDU");
            if (DataSegmentLength != 0)
                throw new ArgumentException("DataSegmentLength must be 0 for LogoutResponsePDU");

            using (var reader = new BinaryReader(new MemoryStream(buffer)))
            {
                // Byte 1: Response (bits 0-7), FinalBit (bit 7) is handled by base.Deserialize
                Response = buffer[1]; // Entire byte 1 is the response code

                reader.BaseStream.Seek(16, SeekOrigin.Begin); // Seek to InitiatorTaskTag
                InitiatorTaskTag = reader.ReadUInt32BigEndian();
                // Bytes 20-23 are reserved
                reader.BaseStream.Seek(24, SeekOrigin.Begin); // Seek to StatSN
                StatusSequenceNumber = reader.ReadUInt32BigEndian();
                ExpectedCommandSequenceNumber = reader.ReadUInt32BigEndian();
                MaximumCommandSequenceNumber = reader.ReadUInt32BigEndian();
                // Bytes 36-39 are reserved
                reader.BaseStream.Seek(40, SeekOrigin.Begin); // Seek to Time2Wait
                Time2Wait = reader.ReadUInt16BigEndian();
                Time2Retain = reader.ReadUInt16BigEndian();
                // Bytes 44-47 are reserved
            }
        }

        public override byte[] Serialize()
        {
            // Ensure constraints are met before serialization
            FinalBit = 1;
            TotalAHSLength = 0;
            DataSegmentLength = 0;

            using (var writer = new BinaryWriter(new MemoryStream()))
            {
                // Byte 0: Opcode and ImmediateDelivery flag
                byte byte0 = (byte)(Opcode & 0x3F);
                if (ImmediateDelivery == 1) byte0 |= 0x80;
                writer.Write(byte0);

                // Byte 1: FinalBit (must be 1) and Response
                byte byte1 = Response; // Response is the full byte
                byte1 |= 0x80; // Set FinalBit (bit 7)
                writer.Write(byte1);

                // Bytes 2-3: Reserved
                writer.Write((ushort)0);

                // Byte 4: TotalAHSLength (must be 0)
                writer.Write(TotalAHSLength);

                // Bytes 5-7: DataSegmentLength (must be 0)
                writer.Write((byte)0); // Byte 5
                writer.Write((byte)0); // Byte 6
                writer.Write((byte)0); // Byte 7
                
                // Bytes 8-15: Reserved (write zeros)
                writer.WriteUInt64BigEndian(0);

                // Bytes 16-19: InitiatorTaskTag
                writer.WriteUInt32BigEndian(InitiatorTaskTag);

                // Bytes 20-23: Reserved
                writer.Write((uint)0);

                // Bytes 24-27: StatSN
                writer.WriteUInt32BigEndian(StatusSequenceNumber);

                // Bytes 28-31: ExpCmdSN
                writer.WriteUInt32BigEndian(ExpectedCommandSequenceNumber);

                // Bytes 32-35: MaxCmdSN
                writer.WriteUInt32BigEndian(MaximumCommandSequenceNumber);

                // Bytes 36-39: Reserved
                writer.Write((uint)0);

                // Bytes 40-41: Time2Wait
                writer.WriteUInt16BigEndian(Time2Wait);

                // Bytes 42-43: Time2Retain
                writer.WriteUInt16BigEndian(Time2Retain);

                // Bytes 44-47: Reserved
                writer.Write((uint)0);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
    }
}