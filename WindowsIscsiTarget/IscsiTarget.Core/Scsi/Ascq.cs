namespace IscsiTarget.Core.Scsi
{
    // Additional Sense Code Qualifiers (ASCQ)
    // This is a partial list. Refer to SCSI specifications for a complete list.
    // Values are often specific to the ASC they qualify.
    public enum Ascq : byte
    {
        Default = 0x00, // Often used when no specific qualifier is applicable
        BecomingReady = 0x01, // Used with ASC LOGICAL UNIT NOT READY (0x04)
        // Add other common ASCQs as needed
    }
}