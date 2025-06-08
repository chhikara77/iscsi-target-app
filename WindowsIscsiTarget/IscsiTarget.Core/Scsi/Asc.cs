namespace IscsiTarget.Core.Scsi
{
    // Additional Sense Codes (ASC)
    // This is a partial list. Refer to SCSI specifications (e.g., SPC, SBC) for a complete list.
    public enum Asc : byte
    {
        LogicalUnitNotReady = 0x04,
        InvalidCommandOperationCode = 0x20,
        LbaOutOfRange = 0x21,
        InvalidFieldInCdb = 0x24,
        LogicalUnitNotSupported = 0x25,
        InvalidFieldInParameterList = 0x26,
        WriteProtected = 0x27,
        UnrecoveredReadError = 0x11,
        InternalTargetFailure = 0x44,
        // Add other common ASCs as needed
    }
}