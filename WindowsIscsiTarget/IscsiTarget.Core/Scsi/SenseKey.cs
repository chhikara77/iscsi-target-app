namespace IscsiTarget.Core.Scsi
{
    public enum SenseKey : byte
    {
        NoSense = 0x00,
        RecoveredError = 0x01,
        NotReady = 0x02,
        MediumError = 0x03,
        HardwareError = 0x04,
        IllegalRequest = 0x05,
        UnitAttention = 0x06,
        DataProtect = 0x07,
        BlankCheck = 0x08,
        VendorSpecific = 0x09,
        CopyAborted = 0x0A,
        AbortedCommand = 0x0B,
        VolumeOverflow = 0x0D,
        Miscompare = 0x0E
        // Add other sense keys as needed
    }
}