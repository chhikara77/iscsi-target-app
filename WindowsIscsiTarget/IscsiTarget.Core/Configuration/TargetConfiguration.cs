using System.Net;
using System.Collections.Generic; // For future LUN list

namespace IscsiTarget.Core.Configuration
{
    public class TargetConfiguration
    {
        public string TargetNameIQN { get; set; } = "iqn.2023-10.com.example:target0"; // Default IQN
        public IPAddress ListeningIPAddress { get; set; } = IPAddress.Any;
        public int ListeningPort { get; set; } = 3260;

        // CHAP credentials for the target (when an initiator challenges this target - Mutual CHAP)
        public string TargetChapNameForMutualAuth { get; set; } // Typically the TargetNameIQN
        public string TargetChapSecretForMutualAuth { get; set; }

        // CHAP credentials for authenticating initiators (target challenges initiator)
        public List<ChapInitiatorCredential> InitiatorChapCredentials { get; set; } = new List<ChapInitiatorCredential>();

        // LUN configurations, including masking (Phase 2 & 4)
        public List<LunConfigurationEntry> Luns { get; set; } = new List<LunConfigurationEntry>();

        public TargetConfiguration()
        {
            // Initialize with default values.
            // In a real application, this would load from a persistent store.
        }

        public void LoadConfiguration(string filePath)
        {
            // TODO: Implement loading configuration from a file (e.g., JSON or XML)
            // Example: 
            // if (File.Exists(filePath))
            // {
            //     string json = File.ReadAllText(filePath);
            //     var loadedConfig = System.Text.Json.JsonSerializer.Deserialize<TargetConfiguration>(json);
            //     if (loadedConfig != null) {
            //         this.TargetNameIQN = loadedConfig.TargetNameIQN;
            //         this.ListeningIPAddress = loadedConfig.ListeningIPAddress;
            //         this.ListeningPort = loadedConfig.ListeningPort;
            //         this.TargetChapNameForMutualAuth = loadedConfig.TargetChapNameForMutualAuth;
            //         this.TargetChapSecretForMutualAuth = loadedConfig.TargetChapSecretForMutualAuth;
            //         this.InitiatorChapCredentials = loadedConfig.InitiatorChapCredentials ?? new List<ChapInitiatorCredential>();
            //         this.Luns = loadedConfig.Luns ?? new List<LunConfigurationEntry>();
            //         // Load other properties
            //     }
            // }
            System.Console.WriteLine($"Configuration loading from {filePath} is not yet implemented.");
        }

        public void SaveConfiguration(string filePath)
        {
            // TODO: Implement saving configuration to a file (e.g., JSON or XML)
            // Example:
            // string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            // File.WriteAllText(filePath, json);
            System.Console.WriteLine($"Configuration saving to {filePath} is not yet implemented.");
        }
    }

    public class LunConfigurationEntry
    {
        public byte LunId { get; set; }
        public string FilePath { get; set; } // Path to VHDX or image file
        public long SizeBytes { get; set; } // Optional, could be derived from file
        public bool ReadOnly { get; set; }
        /// <summary>
        /// List of Initiator IQNs allowed to access this LUN.
        /// If null or empty, LUN is accessible by all authenticated initiators (default behavior if masking not configured).
        /// If populated, only listed initiators can access.
        /// </summary>
        public List<string> AllowedInitiatorIQNs { get; set; } = new List<string>();
    }

    public class ChapInitiatorCredential
    {
        public string InitiatorName { get; set; } // Initiator's IQN or name used in CHAP
        public string Secret { get; set; }
    }
}