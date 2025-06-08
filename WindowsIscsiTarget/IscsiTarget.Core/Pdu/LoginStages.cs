namespace IscsiTarget.Core.Pdu
{
    public enum LoginStages : byte
    {
        SecurityNegotiation = 0,
        LoginOperationalNegotiation = 1,
        FullFeaturePhase = 3
    }
}