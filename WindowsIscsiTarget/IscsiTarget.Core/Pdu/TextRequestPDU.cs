namespace IscsiTarget.Core.Pdu
{
    // RFC 7143 - 10.5 Text Request PDU
    public class TextRequestPDU : BasePDU
    {
        // TODO: Implement fields and methods for TextRequestPDU
        // Basic Header Segment (BHS) - 48 bytes
        // Additional Header Segments (AHS) - Optional
        // Header-Digest - Optional
        // Data Segment - Text parameters
        // Data-Digest - Optional

        public ulong Lun { get; private set; } // Logical Unit Number
        public uint TargetTransferTag { get; private set; } // Initiator assigned
        public bool Final { get; private set; } // F bit
        public bool Continue { get; private set; } // C bit

        public TextRequestPDU()
        {
            // Constructor logic
        }

        public override void Parse(byte[] pduData)
        {
            // TODO: Implement parsing logic from byte array
            base.Parse(pduData); // Parse common BHS fields

            // Example: Extracting LUN (Bytes 8-15 of BHS for Text Request)
            // Lun = (ulong)pduData[8] << 56 | (ulong)pduData[9] << 48 | ... | pduData[15];

            // Example: Extracting Target Transfer Tag (Bytes 20-23 of BHS)
            // TargetTransferTag = (uint)pduData[20] << 24 | (uint)pduData[21] << 16 | ... | pduData[23];

            // Example: Extracting F and C bits (Byte 1 of BHS)
            // Final = (pduData[1] & 0x80) != 0;
            // Continue = (pduData[1] & 0x40) != 0;
        }

        public override byte[] ToBytes()
        {
            // TODO: Implement serialization logic to byte array
            byte[] bhs = new byte[48];
            // Populate BHS fields
            // ... (Opcode, Flags, LUN, TargetTransferTag, etc.)
            return bhs; // Placeholder
        }
    }
}