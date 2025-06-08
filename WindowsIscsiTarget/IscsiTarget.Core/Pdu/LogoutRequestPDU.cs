using System;
using System.IO;
using System.Net;

namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.15 Logout Request PDU
    public class LogoutRequestPDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes ---
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU)
        // FinalBit (from BasePDU) - Must be 1
        // TotalAHSLength (from BasePDU) - Must be 0
        // DataSegmentLength (from BasePDU) - Must be 0

        public byte ReasonCode { get; private set; } // Byte 1, bits 0-6
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU)
        public ushort CID { get; private set; }      // Bytes 20-21: Connection ID (only if reason is close connection)
        // Bytes 22-23 are reserved
        public uint CommandSequenceNumber { get; private set; } // Bytes 24-27: CmdSN
        public uint ExpectedStatusSequenceNumber { get; private set; } // Bytes 28-31: ExpStatSN
        // Bytes 32-47 are reserved

        public LogoutRequestPDU()
        {
            Opcode = (byte)PduOpcode.LogoutRequest;
            FinalBit = 1; // Must be 1 for Logout Request
            TotalAHSLength = 0; // Must be 0
            DataSegmentLength = 0; // Must be 0
        }

        public LogoutRequestPDU(byte reasonCode, uint initiatorTaskTag, ushort cid, uint cmdSN, uint expStatSN)
            : this()
        {
            ReasonCode = reasonCode;
            InitiatorTaskTag = initiatorTaskTag;
            CID = cid;
            CommandSequenceNumber = cmdSN;
            ExpectedStatusSequenceNumber = expStatSN;
        }

        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48)
                throw new ArgumentException("Buffer too short for LogoutRequestPDU BHS");

            base.Deserialize(buffer); // Handles first 8 bytes (Opcode, Flags, TotalAHSLength, DataSegmentLength)

            // Validate constraints for Logout Request
            if (FinalBit != 1)
                throw new ArgumentException("FinalBit must be 1 for LogoutRequestPDU");
            if (TotalAHSLength != 0)
                throw new ArgumentException("TotalAHSLength must be 0 for LogoutRequestPDU");
            if (DataSegmentLength != 0)
                throw new ArgumentException("DataSegmentLength must be 0 for LogoutRequestPDU");

            using (var reader = new BinaryReader(new MemoryStream(buffer)))
            {
                // Byte 1: ReasonCode (bits 0-6), bit 7 is reserved
                ReasonCode = (byte)(buffer[1] & 0x7F);

                reader.BaseStream.Seek(16, SeekOrigin.Begin); // Seek to InitiatorTaskTag
                InitiatorTaskTag = reader.ReadUInt32BigEndian();
                CID = reader.ReadUInt16BigEndian();
                reader.ReadUInt16(); // Skip reserved bytes 22-23
                CommandSequenceNumber = reader.ReadUInt32BigEndian();
                ExpectedStatusSequenceNumber = reader.ReadUInt32BigEndian();
                // Bytes 32-47 are reserved
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
                // Custom BHS serialization for LogoutRequest due to specific flag meanings and constraints
                // Byte 0: Opcode and ImmediateDelivery flag
                byte byte0 = (byte)(Opcode & 0x3F);
                if (ImmediateDelivery == 1) byte0 |= 0x80;
                writer.Write(byte0);

                // Byte 1: FinalBit (must be 1) and ReasonCode
                byte byte1 = (byte)(ReasonCode & 0x7F);
                byte1 |= 0x80; // Set FinalBit (bit 7)
                writer.Write(byte1);

                // Bytes 2-4: Reserved, TotalAHSLength (must be 0), VersionMax, VersionMin (not applicable here, write zeros)
                writer.Write((byte)0); // Byte 2 (Reserved)
                writer.Write((byte)0); // Byte 3 (Reserved)
                writer.Write((byte)0); // Byte 4 (Reserved)

                writer.Write(TotalAHSLength); // Byte 5 (Must be 0)

                // Bytes 6-8: DataSegmentLength (Must be 0)
                writer.Write((byte)0); // Byte 6
                writer.Write((byte)0); // Byte 7
                writer.Write((byte)0); // Byte 8

                // Bytes 8-15: LUN (Reserved for Logout, write zeros)
                writer.WriteUInt64BigEndian(0); 

                // Bytes 16-19: InitiatorTaskTag
                writer.WriteUInt32BigEndian(InitiatorTaskTag);

                // Bytes 20-21: CID
                writer.WriteUInt16BigEndian(CID);

                // Bytes 22-23: Reserved
                writer.Write((ushort)0);

                // Bytes 24-27: CmdSN
                writer.WriteUInt32BigEndian(CommandSequenceNumber);

                // Bytes 28-31: ExpStatSN
                writer.WriteUInt32BigEndian(ExpectedStatusSequenceNumber);

                // Bytes 32-47: Reserved (write 16 zero bytes)
                writer.Write(new byte[16]);

                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
    }
}