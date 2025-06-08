namespace IscsiTarget.Core.Storage
{
    public class Lun
    {
        public byte LunId { get; }
        public string Name { get; set; } // Optional: for easier management
        public IStorageBackend StorageBackend { get; }

        public Lun(byte lunId, IStorageBackend storageBackend, string name = null)
        {
            LunId = lunId;
            StorageBackend = storageBackend;
            Name = name ?? $"LUN{lunId}";
        }

        // Convenience methods that delegate to the storage backend
        public byte[] InquiryData() => StorageBackend.InquiryData();
        public bool TestUnitReady() => StorageBackend.TestUnitReady();
        public (long MaxLba, int BlockSize) GetCapacityDetails() => StorageBackend.GetCapacityDetails();
        public byte[] Read(long lba, int numberOfBlocks) => StorageBackend.Read(lba, numberOfBlocks);
        public void Write(long lba, byte[] data) => StorageBackend.Write(lba, data);
    }
}