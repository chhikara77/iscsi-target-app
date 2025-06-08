using System;
using System.IO;
using System.Net;
using System.Text;

namespace IscsiTarget.Core.Pdu
{
    /// <summary>
    /// Represents an iSCSI Login Request PDU.
    /// Refer to RFC 7143 Section 10.12 for details.
    /// </summary>
    public class LoginRequestPDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes --- 
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU, but for Login Request, this is TransitFlag)
        // FinalBit (from BasePDU, but for Login Request, this is ContinueFlag)
        // TransitBit (from BasePDU, corresponds to TransitFlag)
        // ContinueFlag (from BasePDU, corresponds to ContinueFlag)
        // Reserved (from BasePDU)
        // TotalAHSLength (from BasePDU)
        // DataSegmentLength (from BasePDU)

        public bool TransitFlag { get; private set; }       // Bit 0 of Byte 1 (MSB of byte 1 if byte 0 is Opcode)
        public bool ContinueFlagPdu { get; private set; }    // Bit 1 of Byte 1
        public byte CurrentStage { get; private set; }      // Bits 2-3 of Byte 1 (CSG)
        public byte NextStage { get; private set; }         // Bits 0-1 of Byte 2 (NSG)
        public byte VersionMax { get; private set; }        // Byte 3
        public byte VersionMin { get; private set; }        // Byte 4
        // TotalAHSLength is Byte 5 (already in BasePDU)
        // DataSegmentLength is Bytes 6-7 (already in BasePDU, but needs 3-byte handling)
        public ulong Lun { get; private set; }              // Bytes 8-15 (typically 0 for Login Request)
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU)
        public ushort CID { get; private set; }             // Bytes 20-21 (Connection ID)
        public ushort CommandSequenceNumber { get; private set; } // Bytes 22-23 (CmdSN)
        public uint ExpectedStatusSequenceNumber { get; private set; } // Bytes 24-27 (ExpStatSN)
        // Bytes 28-47 are reserved

        // Data Segment - Key-Value pairs
        public string DataSegmentKeyValuePairs { get; private set; }

        private const byte LoginRequestOpcodeMask = 0x03;

        public LoginRequestPDU()
        {
            Opcode = LoginRequestOpcodeMask;
        }

        /// <summary>
        /// Deserializes a LoginRequestPDU from a byte array.
        /// </summary>
        /// <param name="buffer">The byte array containing PDU data, starting at the BHS.</param>
        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48) // Minimum BHS size
                throw new ArgumentException("Buffer too short for LoginRequestPDU BHS");

            using (var reader = new BinaryReader(new MemoryStream(buffer))) // Assumes BigEndianReader or manual handling
            {
                // Byte 0: Opcode and Immediate bit
                byte byte0 = reader.ReadByte();
                Opcode = (byte)(byte0 & 0x3F);
                ImmediateDelivery = (byte)((byte0 >> 7) & 0x01); // This is TransitFlag for Login
                TransitFlag = (ImmediateDelivery == 1);

                // Byte 1: Flags and CSG
                byte byte1 = reader.ReadByte();
                FinalBit = (byte)((byte1 >> 7) & 0x01); // This is ContinueFlag for Login
                ContinueFlagPdu = (FinalBit == 1);
                CurrentStage = (byte)((byte1 >> 2) & 0x03);
                // Bits 0-1 of byte 1 are reserved

                // Byte 2: NSG and Reserved
                byte byte2 = reader.ReadByte();
                NextStage = (byte)((byte2 >> 6) & 0x03); // Bits 0-1 of byte 2 (NSG)
                // Bits 2-7 of byte 2 are reserved

                // Byte 3: VersionMax
                VersionMax = reader.ReadByte();

                // Byte 4: VersionMin
                VersionMin = reader.ReadByte();

                // Byte 5: TotalAHSLength
                TotalAHSLength = reader.ReadByte();

                // Bytes 6-7: DataSegmentLength (upper 2 bytes of 3-byte field)
                // The actual DataSegmentLength is 3 bytes (Bytes 5,6,7 of PDU, but here it's split)
                // For Login PDU, DataSegmentLength is actually bytes 5,6,7 of the PDU structure.
                // Our BasePDU.DataSegmentLength is uint (4 bytes). We need to read 3 bytes.
                byte[] dslBytes = new byte[4]; // Read into a 4-byte array for easy conversion
                dslBytes[0] = 0; // Most significant byte is 0 for a 3-byte length
                dslBytes[1] = reader.ReadByte(); // Byte 6 of PDU
                dslBytes[2] = reader.ReadByte(); // Byte 7 of PDU
                dslBytes[3] = reader.ReadByte(); // Byte 8 of PDU (this is actually part of LUN in spec)
                                                 // Correcting DataSegmentLength parsing for 3 bytes (bytes 5,6,7 of BHS in RFC)
                                                 // Byte 5 is TotalAHSLength
                                                 // Bytes 6,7,8 are DataSegmentLength
                reader.BaseStream.Position = 5; // Reset to read TotalAHSLength, then DataSegmentLength
                TotalAHSLength = reader.ReadByte();
                byte dsl_b0 = reader.ReadByte();
                byte dsl_b1 = reader.ReadByte();
                byte dsl_b2 = reader.ReadByte();
                DataSegmentLength = (uint)((dsl_b0 << 16) | (dsl_b1 << 8) | dsl_b2);

                // Bytes 8-15: LUN (handled as ulong)
                Lun = reader.ReadUInt64BigEndian();

                // Bytes 16-19: InitiatorTaskTag
                InitiatorTaskTag = reader.ReadUInt32BigEndian();

                // Bytes 20-21: CID
                CID = reader.ReadUInt16BigEndian();

                // Bytes 22-23: CmdSN
                CommandSequenceNumber = reader.ReadUInt16BigEndian();

                // Bytes 24-27: ExpStatSN
                ExpectedStatusSequenceNumber = reader.ReadUInt32BigEndian();

                // Bytes 28-47 are reserved, skip them
                reader.ReadBytes(20);

                // AHS Deserialization (if TotalAHSLength > 0)
                if (TotalAHSLength > 0)
                {
                    // TODO: Implement AHS deserialization if needed for Login
                    reader.ReadBytes(TotalAHSLength * 4); // Skip AHS for now
                }

                // Data Segment Deserialization
                if (DataSegmentLength > 0)
                {
                    byte[] dataBytes = reader.ReadBytes((int)DataSegmentLength);
                    DataSegmentKeyValuePairs = Encoding.UTF8.GetString(dataBytes).TrimEnd('\0');
                }
            }
        }

        /// <summary>
        /// Serializes the LoginRequestPDU to a byte array.
        /// </summary>
        /// <returns>A byte array representing the PDU.</returns>
        public override byte[] Serialize()
        {
            byte[] dataSegmentBytes = null;
            int actualDataSegmentLength = 0;
            if (!string.IsNullOrEmpty(DataSegmentKeyValuePairs))
            {
                dataSegmentBytes = Encoding.UTF8.GetBytes(DataSegmentKeyValuePairs + "\0"); // Null terminate
                actualDataSegmentLength = dataSegmentBytes.Length;
            }
            
            // DataSegmentLength in BHS must be padded to a multiple of 4 bytes
            int paddedDataSegmentLength = (actualDataSegmentLength + 3) & ~3;
            DataSegmentLength = (uint)actualDataSegmentLength; // The BHS field is the logical length

            using (var writer = new BinaryWriter(new MemoryStream())) // Assumes BigEndianWriter or manual handling
            {
                // Byte 0: Opcode and Immediate bit (TransitFlag)
                byte byte0 = (byte)(Opcode & 0x3F);
                if (TransitFlag) byte0 |= 0x80;
                writer.Write(byte0);

                // Byte 1: Flags (ContinueFlag) and CSG
                byte byte1 = (byte)((CurrentStage & 0x03) << 2);
                if (ContinueFlagPdu) byte1 |= 0x80; // Bit 7 is ContinueFlag
                writer.Write(byte1);

                // Byte 2: NSG and Reserved
                byte byte2 = (byte)((NextStage & 0x03) << 6);
                writer.Write(byte2);

                // Byte 3: VersionMax
                writer.Write(VersionMax);

                // Byte 4: VersionMin
                writer.Write(VersionMin);

                // Byte 5: TotalAHSLength
                writer.Write(TotalAHSLength);

                // Bytes 6-8: DataSegmentLength (3 bytes)
                writer.Write((byte)((DataSegmentLength >> 16) & 0xFF));
                writer.Write((byte)((DataSegmentLength >> 8) & 0xFF));
                writer.Write((byte)(DataSegmentLength & 0xFF));

                // Bytes 8-15: LUN
                writer.WriteUInt64BigEndian(Lun);

                // Bytes 16-19: InitiatorTaskTag
                writer.WriteUInt32BigEndian(InitiatorTaskTag);

                // Bytes 20-21: CID
                writer.WriteUInt16BigEndian(CID);

                // Bytes 22-23: CmdSN
                writer.WriteUInt16BigEndian(CommandSequenceNumber);

                // Bytes 24-27: ExpStatSN
                writer.WriteUInt32BigEndian(ExpectedStatusSequenceNumber);

                // Bytes 28-47: Reserved (write 20 zero bytes)
                writer.Write(new byte[20]);

                // AHS Serialization (if TotalAHSLength > 0)
                if (TotalAHSLength > 0)
                {
                    // TODO: Implement AHS serialization if needed for Login
                    writer.Write(new byte[TotalAHSLength * 4]); // Write placeholder for AHS
                }

                // Data Segment Serialization
                if (actualDataSegmentLength > 0 && dataSegmentBytes != null)
                {
                    writer.Write(dataSegmentBytes);
                    // Pad to 4-byte boundary if necessary
                    if (actualDataSegmentLength < paddedDataSegmentLength)
                    {
                        writer.Write(new byte[paddedDataSegmentLength - actualDataSegmentLength]);
                    }
                }
                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
    }

    // Helper extension methods for BinaryReader/Writer for Big-Endian
    public static class BinaryExtensions
    {
        public static ushort ReadUInt16BigEndian(this BinaryReader reader)
        {
            return (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
        }

        public static uint ReadUInt32BigEndian(this BinaryReader reader)
        {
            return (uint)IPAddress.NetworkToHostOrder(reader.ReadInt32());
        }

        public static ulong ReadUInt64BigEndian(this BinaryReader reader)
        {
            return (ulong)IPAddress.NetworkToHostOrder(reader.ReadInt64());
        }

        public static void WriteUInt16BigEndian(this BinaryWriter writer, ushort value)
        {
            writer.Write(IPAddress.HostToNetworkOrder((short)value));
        }

        public static void WriteUInt32BigEndian(this BinaryWriter writer, uint value)
        {
            writer.Write(IPAddress.HostToNetworkOrder((int)value));
        }

        public static void WriteUInt64BigEndian(this BinaryWriter writer, ulong value)
        {
            writer.Write(IPAddress.HostToNetworkOrder((long)value));
        }
    }
}