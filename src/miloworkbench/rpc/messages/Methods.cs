using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Rpc.Shared;

public static class Methods
{
    public enum Id : byte
    {
        SpeedTestUpload = 1,
        SpeedTestDownload = 2,

        UploadFile = 5,
        DownloadFile = 6
    }

    public static class SpeedTestService
    {
        public static readonly DefaultMethodId SpeedTestUpload =
            new((byte)Id.SpeedTestUpload, "SpeedTestUpload");

        public static readonly DefaultMethodId SpeedTestDownload =
            new((byte)Id.SpeedTestDownload, "SpeedTestDownload");

        public static readonly DefaultMethodId First = SpeedTestUpload;
        public static readonly DefaultMethodId Last = SpeedTestDownload;
    }

    public static class FileTransferService
    {
        public static readonly DefaultMethodId UploadFile =
            new((byte) Id.UploadFile, "UploadFile");

        public static readonly DefaultMethodId DownloadFile =
            new((byte)Id.DownloadFile, "DownloadFile");
        
        public static readonly DefaultMethodId First = UploadFile;
        public static readonly DefaultMethodId Last = DownloadFile;
    }
}
