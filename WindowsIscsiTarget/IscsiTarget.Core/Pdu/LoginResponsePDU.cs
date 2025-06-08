using System;
using System.IO;
using System.Net;
using System.Text;

namespace IscsiTarget.Core.Pdu
{
    /// <summary>
    /// Represents an iSCSI Login Response PDU.
    /// Refer to RFC 7143 Section 10.13 for details.
    /// </summary>
    public class LoginResponsePDU : BasePDU
    {
        // --- Basic Header Segment (BHS) - 48 bytes ---
        // Opcode (from BasePDU)
        // ImmediateDelivery (from BasePDU, but for Login Response, this is TransitFlag)
        // FinalBit (from BasePDU, but for Login Response, this is ContinueFlag)
        // TransitBit (from BasePDU, corresponds to TransitFlag)
        // ContinueFlag (from BasePDU, corresponds to ContinueFlag)
        // Reserved (from BasePDU)
        // TotalAHSLength (from BasePDU)
        // DataSegmentLength (from BasePDU)

        public bool TransitFlag { get; set; }       // Bit 0 of Byte 1 (MSB of byte 1 if byte 0 is Opcode)
        public bool ContinueFlagPdu { get; set; }    // Bit 1 of Byte 1
        public byte CurrentStage { get; private set; }      // Bits 2-3 of Byte 1 (CSG)
        public byte NextStage { get; private set; }         // Bits 0-1 of Byte 2 (NSG)
        public byte VersionMax { get; private set; }        // Byte 3 (Active version)
        public byte VersionMin { get; private set; }        // Byte 4 (Min supported version)
        // TotalAHSLength is Byte 5 (already in BasePDU)
        // DataSegmentLength is Bytes 6-7 (already in BasePDU, but needs 3-byte handling)
        public ulong Lun { get; private set; }              // Bytes 8-15 (typically 0 for Login Response)
        // InitiatorTaskTag is Bytes 16-19 (already in BasePDU, copied from LoginRequest)
        public uint StatusSequenceNumber { get; set; }    // Bytes 24-27 (StatSN)
        public uint ExpectedCommandSequenceNumber { get; set; } // Bytes 28-31 (ExpCmdSN)
        public uint MaximumCommandSequenceNumber { get; set; } // Bytes 32-35 (MaxCmdSN)
        public byte StatusClass { get; set; }           // Byte 36
        public byte StatusDetail { get; set; }          // Byte 37
        // Bytes 38-47 are reserved

        // Data Segment - Key-Value pairs
        public string DataSegmentKeyValuePairs { get; set; }

        private const byte LoginResponseOpcodeMask = (byte)PduOpcode.LoginResponse;

        public LoginResponsePDU()
        {
            Opcode = LoginResponseOpcodeMask;
            VersionMax = 0x00; // Default active version
            VersionMin = 0x00; // Default min supported version
            DataSegmentKeyValuePairs = string.Empty;
        }

        public override void Deserialize(byte[] buffer)
        {
            if (buffer.Length < 48) 
                throw new ArgumentException("Buffer too short for LoginResponsePDU BHS");

            base.Deserialize(buffer); // Handles first 8 bytes (Opcode, Flags, TotalAHSLength, DataSegmentLength)

            using (var reader = new BinaryReader(new MemoryStream(buffer)))
            {
                // Byte 0: Opcode and TransitFlag
                // Opcode = (byte)(byte0 & 0x3F); // Done in base
                // TransitFlag = (byte0 & 0x80) != 0; // Done in base as ImmediateDelivery
                TransitFlag = ImmediateDelivery == 1;

                // Byte 1: ContinueFlag and CSG
                // ContinueFlagPdu = (byte1 & 0x80) != 0; // Done in base as FinalBit
                ContinueFlagPdu = FinalBit == 1;
                CurrentStage = (byte)((buffer[1] >> 2) & 0x03);

                // Byte 2: NSG
                NextStage = (byte)((buffer[2] >> 6) & 0x03);

                VersionMax = buffer[3];
                VersionMin = buffer[4];
                // TotalAHSLength = buffer[5]; // Done in base
                // DataSegmentLength = (uint)((buffer[6] << 16) | (buffer[7] << 8) | buffer[8]); // Done in base

                reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip common BHS part already handled by base.Deserialize
                Lun = reader.ReadUInt64BigEndian();
                InitiatorTaskTag = reader.ReadUInt32BigEndian();
                // Skip 4 bytes (Target Transfer Tag for other PDUs, not used here as per RFC for LoginResponse)
                reader.ReadUInt32(); // Effectively skipping bytes 20-23
                StatusSequenceNumber = reader.ReadUInt32BigEndian();
                ExpectedCommandSequenceNumber = reader.ReadUInt32BigEndian();
                MaximumCommandSequenceNumber = reader.ReadUInt32BigEndian();
                StatusClass = reader.ReadByte();
                StatusDetail = reader.ReadByte();

                // Bytes 38-47 are reserved, skip them
                reader.ReadBytes(10);

                if (TotalAHSLength > 0)
                {
                    reader.ReadBytes(TotalAHSLength * 4); // Skip AHS
                }

                if (DataSegmentLength > 0)
                {
                    byte[] dataBytes = reader.ReadBytes((int)DataSegmentLength);
                    DataSegmentKeyValuePairs = Encoding.UTF8.GetString(dataBytes).TrimEnd('\0');
                }
            }
        }

        public override byte[] Serialize()
        {
            byte[] dataSegmentBytes = null;
            int actualDataSegmentLength = 0;
            if (!string.IsNullOrEmpty(DataSegmentKeyValuePairs))
            {
                dataSegmentBytes = Encoding.UTF8.GetBytes(DataSegmentKeyValuePairs + "\0");
                actualDataSegmentLength = dataSegmentBytes.Length;
            }
            DataSegmentLength = (uint)actualDataSegmentLength;
            int paddedDataSegmentLength = (actualDataSegmentLength + 3) & ~3;

            // Set flags for base serialization
            ImmediateDelivery = (byte)(TransitFlag ? 1 : 0);
            FinalBit = (byte)(ContinueFlagPdu ? 1 : 0);

            using (var writer = new BinaryWriter(new MemoryStream()))
            {
                // Custom BHS serialization for LoginResponse due to specific flag meanings
                // Byte 0: Opcode and Transit flag
                byte byte0 = (byte)(Opcode & 0x3F);
                if (TransitFlag) byte0 |= 0x80;
                writer.Write(byte0);

                // Byte 1: Continue flag and CSG
                byte byte1 = (byte)((CurrentStage & 0x03) << 2);
                if (ContinueFlagPdu) byte1 |= 0x80;
                writer.Write(byte1);

                // Byte 2: NSG and Reserved
                byte byte2 = (byte)((NextStage & 0x03) << 6);
                writer.Write(byte2);

                writer.Write(VersionMax);
                writer.Write(VersionMin);
                writer.Write(TotalAHSLength);
                writer.Write((byte)((DataSegmentLength >> 16) & 0xFF));
                writer.Write((byte)((DataSegmentLength >> 8) & 0xFF));
                writer.Write((byte)(DataSegmentLength & 0xFF));
                
                writer.WriteUInt64BigEndian(Lun);
                writer.WriteUInt32BigEndian(InitiatorTaskTag);
                writer.Write(0xFFFFFFFF); // Target Transfer Tag (set to 0xFFFFFFFF for Login Response)
                writer.WriteUInt32BigEndian(StatusSequenceNumber);
                writer.WriteUInt32BigEndian(ExpectedCommandSequenceNumber);
                writer.WriteUInt32BigEndian(MaximumCommandSequenceNumber);
                writer.Write(StatusClass);
                writer.Write(StatusDetail);
                writer.Write(new byte[10]); // Reserved bytes 38-47

                if (TotalAHSLength > 0)
                {
                    writer.Write(new byte[TotalAHSLength * 4]); // Placeholder for AHS
                }

                if (actualDataSegmentLength > 0 && dataSegmentBytes != null)
                {
                    writer.Write(dataSegmentBytes);
                    if (actualDataSegmentLength < paddedDataSegmentLength)
                    {
                        writer.Write(new byte[paddedDataSegmentLength - actualDataSegmentLength]);
                    }
                }
                return ((MemoryStream)writer.BaseStream).ToArray();
            }
        }
    }
}