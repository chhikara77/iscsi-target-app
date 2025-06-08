using System.IO;

namespace IscsiTarget.Core.Storage
{
    public class VhdxStorageBackend : IStorageBackend
    {
        private readonly string _filePath;
        private readonly FileStream _fileStream;
        private readonly long _capacity; // In bytes
        private const int BlockSize = 512; // Standard block size

        public VhdxStorageBackend(string filePath)
        {
            _filePath = filePath;
            // For simplicity, we'll treat the file as a raw image.
            // VHDX parsing would be more complex.
            _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _capacity = _fileStream.Length;
        }

        public byte[] InquiryData()
        {
            // Standard Inquiry Data (36 bytes)
            // Device Type: 0x00 (Direct Access Block Device)
            // Other fields can be set to default/generic values.
            byte[] inquiryData = new byte[36];
            inquiryData[0] = 0x00; // Peripheral Device Type: Direct-access block device
            inquiryData[1] = 0x80; // RMB: Removable Medium Bit (0 for non-removable, 0x80 for removable - VHDX can be seen as removable)
            inquiryData[2] = 0x05; // Version: SPC-3 (adjust as needed)
            inquiryData[3] = 0x02; // Response Data Format: 2
            inquiryData[4] = 31;   // Additional Length (36-5 = 31)
            // Bytes 5-7: Reserved
            // Bytes 8-15: Vendor Identification (e.g., "TraeAI  ")
            System.Text.Encoding.ASCII.GetBytes("TraeAI  ").CopyTo(inquiryData, 8);
            // Bytes 16-31: Product Identification (e.g., "iSCSI Target    ")
            System.Text.Encoding.ASCII.GetBytes("iSCSI Target    ").CopyTo(inquiryData, 16);
            // Bytes 32-35: Product Revision Level (e.g., "1.0 ")
            System.Text.Encoding.ASCII.GetBytes("1.0 ").CopyTo(inquiryData, 32);
            return inquiryData;
        }

        public bool TestUnitReady()
        {
            // If the file stream is open and valid, the unit is ready.
            return _fileStream != null && _fileStream.CanRead;
        }

        public (long MaxLba, int BlockSize) GetCapacityDetails()
        {
            long maxLba = (_capacity / BlockSize) - 1;
            return (maxLba, BlockSize);
        }

        public byte[] Read(long lba, int numberOfBlocks)
        {
            long offset = lba * BlockSize;
            int length = numberOfBlocks * BlockSize;

            if (offset + length > _capacity)
            {
                // Handle read beyond capacity - this should ideally be caught earlier
                // or return appropriate SCSI sense data.
                throw new IOException("Read request exceeds LUN capacity.");
            }

            byte[] buffer = new byte[length];
            _fileStream.Seek(offset, SeekOrigin.Begin);
            int bytesRead = _fileStream.Read(buffer, 0, length);
            if (bytesRead < length)
            {
                // Handle partial read if necessary, though for block devices full read is expected.
                // For simplicity, we assume full read or throw.
                throw new IOException("Partial read from storage backend.");
            }
            return buffer;
        }

        public void Write(long lba, byte[] data)
        {
            long offset = lba * BlockSize;
            int length = data.Length;

            if (offset + length > _capacity)
            {
                // Handle write beyond capacity
                throw new IOException("Write request exceeds LUN capacity.");
            }
            if (length % BlockSize != 0)
            {
                 throw new ArgumentException("Data length must be a multiple of block size.", nameof(data));
            }

            _fileStream.Seek(offset, SeekOrigin.Begin);
            _fileStream.Write(data, 0, length);
            _fileStream.Flush(); // Ensure data is written to disk
        }

        public void Dispose()
        {
            _fileStream?.Dispose();
        }
    }
}