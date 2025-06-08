namespace IscsiTarget.Core.Storage
{
    public interface IStorageBackend
    {
        byte[] InquiryData();
        bool TestUnitReady();
        (long MaxLba, int BlockSize) GetCapacityDetails();
        byte[] Read(long offset, int length);
        void Write(long offset, byte[] data);
    }
}