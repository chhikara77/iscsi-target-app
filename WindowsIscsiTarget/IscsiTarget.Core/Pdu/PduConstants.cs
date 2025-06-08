namespace IscsiTarget.Core.Pdu
{
    public static class PduConstants
    {
        // General Keys
        public const string KeyInitiatorName = "InitiatorName";
        public const string KeyTargetName = "TargetName";
        public const string KeyMaxRecvDataSegmentLength = "MaxRecvDataSegmentLength";
        public const string KeyHeaderDigest = "HeaderDigest";
        public const string KeyDataDigest = "DataDigest";
        public const string KeyOFMarker = "OFMarker";
        public const string KeyIFMarker = "IFMarker";
        public const string KeyOFMarkInt = "OFMarkInt";
        public const string KeyIFMarkInt = "IFMarkInt";
        public const string KeyMaxConnections = "MaxConnections";
        public const string KeySendTargets = "SendTargets";
        public const string KeyTargetAlias = "TargetAlias";
        public const string KeyInitiatorAlias = "InitiatorAlias";
        public const string KeyMaxBurstLength = "MaxBurstLength";
        public const string KeyFirstBurstLength = "FirstBurstLength";
        public const string KeyDefaultTime2Wait = "DefaultTime2Wait";
        public const string KeyDefaultTime2Retain = "DefaultTime2Retain";
        public const string KeyMaxOutstandingR2T = "MaxOutstandingR2T";
        public const string KeyDataPDUInOrder = "DataPDUInOrder";
        public const string KeyDataSequenceInOrder = "DataSequenceInOrder";
        public const string KeyErrorRecoveryLevel = "ErrorRecoveryLevel";
        public const string KeySessionType = "SessionType";

        // Authentication Method Keys and Values
        public const string KeyAuthMethod = "AuthMethod";
        public const string AuthMethodCHAP = "CHAP";
        public const string AuthMethodNone = "None";
        // Potentially others like SRP, Kerberos, SPKM1, SPKM2

        // CHAP Specific Keys
        public const string KeyCHAPAlgorithm = "CHAP_A"; // CHAP_Algorithm
        public const string KeyCHAPIdent = "CHAP_I";     // CHAP_Ident
        public const string KeyCHAPChallenge = "CHAP_C"; // CHAP_Challenge
        public const string KeyCHAPName = "CHAP_N";      // CHAP_Name (for response)
        public const string KeyCHAPResponse = "CHAP_R";  // CHAP_Response

        // Digest Values
        public const string DigestNone = "None";
        public const string DigestCRC32C = "CRC32C";

        // SessionType Values
        public const string SessionTypeDiscovery = "Discovery";
        public const string SessionTypeNormal = "Normal";
    }
}