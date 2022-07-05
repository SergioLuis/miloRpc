using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Rpc.Shared;

public static class Methods
{
    public enum Id : byte
    {
        SpeedTestUpload = 1,
        SpeedTestDownload = 2
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
}
