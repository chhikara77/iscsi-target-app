namespace IscsiTarget.Core.Pdu
{
    public enum PduOpcode : byte
    {
        NopOut = 0x00,
        ScsiCommand = 0x01,
        TaskManagementFunctionRequest = 0x02,
        LoginRequest = 0x03,
        TextRequest = 0x04,
        DataOut = 0x05,
        LogoutRequest = 0x06,
        SnackRequest = 0x10,

        NopIn = 0x20,
        ScsiResponse = 0x21,
        TaskManagementFunctionResponse = 0x22,
        LoginResponse = 0x23,
        TextResponse = 0x24,
        DataIn = 0x25,
        LogoutResponse = 0x26,
        ReadyToTransfer = 0x31,
        AsynchronousMessage = 0x32,
        Reject = 0x3F
    }
}