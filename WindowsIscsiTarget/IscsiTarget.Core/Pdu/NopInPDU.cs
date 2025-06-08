using System;
using System.IO;
using System.Net;

namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.14 NOP-In PDU
    public class NopInPDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes ---
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU)
        // FinalBit (from BasePDU)
        // TotalAHSLength (from BasePDU)
        // DataSegmentLength (from BasePDU)

        public ulong Lun { get; set; } // Bytes 8-15: Logical Unit Number (usually reserved, copied from NOP-Out)
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU, copied from NOP-Out)
        public uint TargetTransferTag { get; set; } // Bytes 20-23: Copied from NOP-Out (if it was not 0xffffffff)
        public uint StatusSequenceNumber { get; set; } // Bytes 24-27: StatSN
        public uint ExpectedCommandSequenceNumber { get; set; } // Bytes 28-31: ExpCmdSN
        public uint MaximumCommandSequenceNumber { get; set; } // Bytes 32-35: MaxCmdSN
        // Bytes 36-47 are reserved

        public byte[] PingData { get; set; } // Optional data segment, copied from NOP-Out

        public NopInPDU()
        {
            Opcode = (byte)PduOpcode.NopIn;
            TargetTransferTag = 0xFFFFFFFF; // Default, should be set if NOP-Out solicited it
            PingData = Array.Empty<byte>();
        }

        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48) 
                throw new ArgumentException("Buffer too short for NopInPDU BHS");

            base.Deserialize(buffer); // Handles first 8 bytes (Opcode, Flags, TotalAHSLength, DataSegmentLength)

            using (var reader = new BinaryReader(new MemoryStream(buffer))) 
            {
                reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip already parsed common fields

                Lun = reader.ReadUInt64BigEndian();
                InitiatorTaskTag = reader.ReadUInt32BigEndian(); // Copied from NOP-Out
                TargetTransferTag = reader.ReadUInt32BigEndian();
                StatusSequenceNumber = reader.ReadUInt32BigEndian();
                ExpectedCommandSequenceNumber = reader.ReadUInt32BigEndian();
                MaximumCommandSequenceNumber = reader.ReadUInt32BigEndian();

                // Bytes 36-47 are reserved, skip them
                reader.ReadBytes(12);

                // AHS Deserialization (if TotalAHSLength > 0)
                if (TotalAHSLength > 0)
                {
                    // TODO: Implement AHS deserialization if needed
                    reader.ReadBytes(TotalAHSLength * 4); // Skip AHS for now
                }

                // Data Segment Deserialization
                if (DataSegmentLength > 0)
                {
                    PingData = reader.ReadBytes((int)DataSegmentLength);
                }
            }
        }

        public override byte[] Serialize()
        {
            DataSegmentLength = (uint)PingData.Length;
            int paddedDataSegmentLength = (PingData.Length + 3) & ~3;

            using (var writer = new BinaryWriter(new MemoryStream()))
            {
                base.SerializeBHS(writer); // Serialize common BHS fields (first 8 bytes)
                
                writer.WriteUInt64BigEndian(Lun);
                writer.WriteUInt32BigEndian(InitiatorTaskTag); // Copied from NOP-Out
                writer.WriteUInt32BigEndian(TargetTransferTag);
                writer.WriteUInt32BigEndian(StatusSequenceNumber);
                writer.WriteUInt32BigEndian(ExpectedCommandSequenceNumber);
                writer.WriteUInt32BigEndian(MaximumCommandSequenceNumber);

                // Bytes 36-47: Reserved (write 12 zero bytes)
                writer.Write(new byte[12]);

                // AHS Serialization (if TotalAHSLength > 0)
                if (TotalAHSLength > 0)
                {
                    // TODO: Implement AHS serialization if needed
                    writer.Write(new byte[TotalAHSLength * 4]); // Write placeholder for AHS
                }

                // Data Segment Serialization
                if (DataSegmentLength > 0)
                {
                    writer.Write(PingData);
                    // Pad to 4-byte boundary if necessary
                    if (PingData.Length < paddedDataSegmentLength)
                    {
                        writer.Write(new byte[paddedDataSegmentLength - PingData.Length]);
                    }
                }
                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
    }
}