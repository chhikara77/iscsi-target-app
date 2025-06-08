using System;
using System.IO;
using System.Net;

namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.13 NOP-Out PDU
    public class NopOutPDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes ---
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU)
        // FinalBit (from BasePDU)
        // TotalAHSLength (from BasePDU)
        // DataSegmentLength (from BasePDU)

        public ulong Lun { get; private set; } // Bytes 8-15: Logical Unit Number (usually reserved)
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU)
        public uint TargetTransferTag { get; private set; } // Bytes 20-23: Target assigned (0xffffffff if not soliciting NOP-In)
        public uint CommandSequenceNumber { get; private set; } // Bytes 24-27: CmdSN (only if soliciting NOP-In)
        public uint ExpectedStatusSequenceNumber { get; private set; } // Bytes 28-31: ExpStatSN (only if soliciting NOP-In)
        // Bytes 32-47 are reserved

        public byte[] PingData { get; set; } // Optional data segment

        public NopOutPDU()
        {
            Opcode = (byte)PduOpcode.NopOut;
            InitiatorTaskTag = 0xFFFFFFFF; // Should be set by initiator
            TargetTransferTag = 0xFFFFFFFF; // Default: not soliciting NOP-In
            PingData = Array.Empty<byte>();
        }

        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48) 
                throw new ArgumentException("Buffer too short for NopOutPDU BHS");

            base.Deserialize(buffer); // Handles first 8 bytes (Opcode, Flags, TotalAHSLength, DataSegmentLength)

            using (var reader = new BinaryReader(new MemoryStream(buffer))) 
            {
                reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip already parsed common fields

                Lun = reader.ReadUInt64BigEndian();
                InitiatorTaskTag = reader.ReadUInt32BigEndian();
                TargetTransferTag = reader.ReadUInt32BigEndian();
                CommandSequenceNumber = reader.ReadUInt32BigEndian();
                ExpectedStatusSequenceNumber = reader.ReadUInt32BigEndian();

                // Bytes 32-47 are reserved, skip them
                reader.ReadBytes(16);

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
                writer.WriteUInt32BigEndian(InitiatorTaskTag);
                writer.WriteUInt32BigEndian(TargetTransferTag);
                writer.WriteUInt32BigEndian(CommandSequenceNumber);
                writer.WriteUInt32BigEndian(ExpectedStatusSequenceNumber);

                // Bytes 32-47: Reserved (write 16 zero bytes)
                writer.Write(new byte[16]);

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