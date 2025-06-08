namespace IscsiTarget.Core.Scsi
{
    public enum ScsiOpCode : byte
    {
        TestUnitReady = 0x00,
        RequestSense = 0x03,
        Inquiry = 0x12,
        ModeSelect6 = 0x15,
        ModeSense6 = 0x1A,
        ReadCapacity10 = 0x25,
        Read10 = 0x28,
        Write10 = 0x2A,
        ReportLuns = 0xA0,
        Read16 = 0x88,
        Write16 = 0x8A,
        ReadCapacity16 = 0x9E, // Service Action: 0x10
        // Add other common SCSI opcodes as needed
    }
}