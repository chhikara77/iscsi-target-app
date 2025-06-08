using System;

namespace IscsiTarget.Shared
{
    [Serializable] // Important for some IPC mechanisms if objects are serialized directly
    public class LunDto
    {
        public byte LunId { get; set; }
        public string VhdxPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Status { get; set; } = string.Empty; // e.g., "Online", "Offline", "Error"
        public string Name { get; set; } = string.Empty; // Optional friendly name

        // Add other relevant properties as needed, e.g., InitiatorIQNs for masking
    }
}