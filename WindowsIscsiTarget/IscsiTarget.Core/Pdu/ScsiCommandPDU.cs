namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.3 SCSI Command PDU
    public class ScsiCommandPDU : BasePDU
    {
        // TODO: Implement fields and methods for ScsiCommandPDU
        // Basic Header Segment (BHS) - 48 bytes
        // Additional Header Segments (AHS) - Optional (e.g., for bidirectional commands)
        // Header-Digest - Optional
        // Data Segment - Optional (e.g., for WRITE commands, if ImmediateData is not used)
        // Data-Digest - Optional

        public ulong Lun { get; private set; } // Logical Unit Number
        public uint InitiatorTaskTag { get; private set; } // Initiator assigned
        public uint ExpectedDataTransferLength { get; private set; } // For READ or WRITE
        public byte[] Cdb { get; private set; } // SCSI Command Descriptor Block (16 bytes typical)

        public bool IsRead { get; private set; } // R bit (Byte 1 of BHS)
        public bool IsWrite { get; private set; } // W bit (Byte 1 of BHS)
        // Other flags like Attr (Task Attributes), NoUnsolicitedData, etc.

        public ScsiCommandPDU()
        {
            // Constructor logic
            Opcode = PduOpcode.ScsiCommand;
            Cdb = new byte[16]; // Initialize CDB, actual length can vary but 16 is common
        }

        public override void Parse(byte[] pduData)
        {
            // TODO: Implement parsing logic from byte array
            base.Parse(pduData); // Parse common BHS fields

            // Example: Extracting LUN (Bytes 8-15 of BHS)
            // Lun = (ulong)pduData[8] << 56 | ... | pduData[15];

            // Example: Extracting Initiator Task Tag (Bytes 16-19 of BHS)
            // InitiatorTaskTag = (uint)pduData[16] << 24 | ... | pduData[19];

            // Example: Extracting Expected Data Transfer Length (Bytes 20-23 of BHS)
            // ExpectedDataTransferLength = (uint)pduData[20] << 24 | ... | pduData[23];

            // Example: Extracting R and W bits (Byte 1 of BHS)
            // IsRead = (pduData[1] & 0x40) != 0;
            // IsWrite = (pduData[1] & 0x20) != 0;

            // Example: Extracting CDB (Bytes 32-47 of BHS)
            // Array.Copy(pduData, 32, Cdb, 0, 16);
        }

        public override byte[] ToBytes()
        {
            // TODO: Implement serialization logic to byte array
            byte[] bhs = new byte[48];
            // Populate BHS fields
            // ... (Opcode, Flags, LUN, InitiatorTaskTag, ExpectedDataTransferLength, CDB, etc.)
            return bhs; // Placeholder
        }
    }
}