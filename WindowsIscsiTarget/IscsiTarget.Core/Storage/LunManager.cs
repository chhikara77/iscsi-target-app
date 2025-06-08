using System.Collections.Generic;
using System.Linq;
using IscsiTarget.Core.Configuration;
using Serilog; // Add Serilog

namespace IscsiTarget.Core.Storage
{
    public class LunManager
    {
        private readonly Dictionary<byte, Lun> _luns = new Dictionary<byte, Lun>();
        private readonly TargetConfiguration _configuration;

        public LunManager(TargetConfiguration configuration)
        {
            _configuration = configuration;
            LoadLunsFromConfiguration();
        }

        public Lun AddLun(string storagePath, byte lunId, string name = null)
        {
            if (_luns.ContainsKey(lunId))
            {
                throw new ArgumentException($"LUN with ID {lunId} already exists.", nameof(lunId));
            }

            // For now, we assume VHDX backend. This could be made more flexible.
            IStorageBackend backend = new VhdxStorageBackend(storagePath);
            Lun lun = new Lun(lunId, backend, name);
            _luns[lunId] = lun;

            // Update configuration object
            var lunConfigEntry = _configuration.Luns.FirstOrDefault(l => l.LunId == lunId);
            if (lunConfigEntry == null)
            {
                lunConfigEntry = new LunConfigurationEntry { LunId = lunId, FilePath = storagePath, /* Other properties like ReadOnly, AllowedInitiatorIQNs can be set here or via UI */ };
                _configuration.Luns.Add(lunConfigEntry);
            }
            else
            {
                lunConfigEntry.FilePath = storagePath;
                // Potentially update other properties if they can be changed post-creation
            }
            // TODO: Persist _configuration changes (e.g., _configuration.SaveConfiguration(...))
            return lun;
        }

        public bool RemoveLun(byte lunId)
        {
            if (_luns.TryGetValue(lunId, out Lun lunToDispose))
            {
                // Dispose the backend if it's disposable
                if (lunToDispose.StorageBackend is IDisposable disposableBackend)
                {
                    disposableBackend.Dispose();
                }
                var result = _luns.Remove(lunId);
                if (result)
                {
                    _configuration.Luns.RemoveAll(l => l.LunId == lunId);
                    // TODO: Persist _configuration changes
                }
                return result;
            }
            return false;
        }

        public Lun GetLun(byte lunId)
        {
            _luns.TryGetValue(lunId, out Lun lun);
            return lun; // Returns null if not found
        }

        public List<Lun> GetAllLuns()
        {
            return _luns.Values.ToList();
        }

        public List<byte> GetConfiguredLunIds()
        {
            return _luns.Keys.OrderBy(id => id).ToList();
        }

        private void LoadLunsFromConfiguration()
        {
            _luns.Clear(); // Clear existing LUNs before loading from config
            if (_configuration?.Luns == null) return;

            foreach (var lunConfig in _configuration.Luns)
            {
                try
                {
                    // We assume VHDX backend for now. This could be made more flexible.
                    IStorageBackend backend = new VhdxStorageBackend(lunConfig.FilePath);
                    // The LUN name could also come from configuration if desired.
                    Lun lun = new Lun(lunConfig.LunId, backend, $"LUN{lunConfig.LunId}"); 
                    _luns[lunConfig.LunId] = lun;
                    Log.Information($"Loaded LUN {lunConfig.LunId} from path {lunConfig.FilePath}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error loading LUN {lunConfig.LunId} from {lunConfig.FilePath}");
                    // Optionally, handle this more gracefully, e.g., mark LUN as unavailable
                }
            }
        }

        // Call this method if the configuration is updated externally (e.g., by UI)
        public void RefreshLunsFromConfiguration()
        {
            // Dispose old LUNs before reloading to release file handles etc.
            foreach (var lunEntry in _luns.Values)
            {
                if (lunEntry.StorageBackend is IDisposable disposableBackend)
                {
                    disposableBackend.Dispose();
                }
            }
            LoadLunsFromConfiguration();
        }
    }
}