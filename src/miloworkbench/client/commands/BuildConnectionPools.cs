using System.Net.Security;
using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Client.Commands;

public static class BuildConnectionPools
{
    public static MiloConnectionPools Build(BaseSettings settings)
    {
        ConnectionSettings.SslSettings sslSettings = new()
        {
            Status = SharedCapabilityEnablement.EnabledMandatory,
            CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates,
            ApplicationProtocols = new[]
            {
                new SslApplicationProtocol("miloworkbench")
            }
        };

        ConnectionSettings.BufferingSettings bufferingSettings = settings.BufferSize <= 0
            ? ConnectionSettings.BufferingSettings.Disabled
            : new ConnectionSettings.BufferingSettings
            {
                Status = PrivateCapabilityEnablement.Enabled,
                BufferSize = settings.BufferSize
            };

        ConnectionSettings tcpConnectionSettings = new()
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Buffering = bufferingSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled
        };

        ConnectionSettings sslConnectionSettings = new()
        {
            Ssl = sslSettings,
            Buffering = bufferingSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled
        };

        ConnectionSettings quicConnectionSettings = new()
        {
            Ssl = sslSettings,
            Buffering = bufferingSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled
        };

        return new MiloConnectionPools(
            tcpConnectionSettings,
            sslConnectionSettings,
            quicConnectionSettings);
    }
}
