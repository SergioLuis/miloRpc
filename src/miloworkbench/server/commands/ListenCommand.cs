using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

using miloRPC.Channels.Quic;
using miloRPC.Channels.Tcp;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRpc.TestWorkBench.Rpc.Server;

namespace miloRpc.TestWorkBench.Server.Commands;

public class ListenCommand : AsyncCommand<ListenCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--tcp <IPEndPoint>")]
        [Description("The endpoint where the TCP port will be binded. Use 'none' to skip this port. Default is '0.0.0.0:8070'")]
        public string TcpBindEndPoint { get; set; } = "0.0.0.0:8070";

        [CommandOption("--ssl <IPEndPoint>")]
        [Description("The endpoint where the SSL over TCP port will be binded. Use 'none' to skip this port. Default is '0.0.0.0:8080'")]
        public string TcpSslBindEndPoint { get; set; } = "0.0.0.0:8080";

        [CommandOption("--quic <IPEndPoint>")]
        [Description("The endpoint where the QUIC port will be binded. Use 'none' to skip this port. Default is '0.0.0.0:8090'")]
        public string QuicBindEndPoint { get; set; } = "0.0.0.0:8090";
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.TcpBindEndPoint is not "none")
        {
            if (!IPEndPoint.TryParse(settings.TcpBindEndPoint, out _))
                return ValidationResult.Error("'tcp' is not a valid IPEndPoint (example: '127.0.0.1:8080' or 'none')");
        }

        if (settings.TcpSslBindEndPoint is not "none")
        {
            if (!IPEndPoint.TryParse(settings.TcpSslBindEndPoint, out _))
                return ValidationResult.Error("'ssl' is not a valid IPEndPoint (example: '127.0.0.1:8080' or 'none')");
        }

        if (settings.QuicBindEndPoint is not "none")
        {
            if (!IPEndPoint.TryParse(settings.QuicBindEndPoint, out _))
                return ValidationResult.Error("'quic' is not a valid IPEndPoint (example: '127.0.0.1:8080' or 'none')");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            Console.WriteLine("CTRL + C pressed, stopping listening servers...");
            cts.Cancel();
            e.Cancel = true;
        };

        StubCollection stubCollection = new();
        stubCollection.RegisterStub(new SpeedTestStub(new SpeedTestService()));

        List<Task> serverTasks = new();
        if (settings.TcpBindEndPoint is not "none")
        {
            IPEndPoint tcpEndPoint = IPEndPoint.Parse(settings.TcpBindEndPoint);
            serverTasks.Add(
                StartTcp(
                    stubCollection,
                    tcpEndPoint,
                    CancellationTokenSource.CreateLinkedTokenSource(cts.Token).Token));
            Console.WriteLine($"Listening on tcp://{tcpEndPoint}");
        }

        if (settings.TcpSslBindEndPoint is not "none")
        {
            IPEndPoint sslEndPoint = IPEndPoint.Parse(settings.TcpSslBindEndPoint);
            serverTasks.Add(
                StartSslOverTcp(
                    stubCollection,
                    sslEndPoint,
                    CancellationTokenSource.CreateLinkedTokenSource(cts.Token).Token));
            Console.WriteLine($"Listening on ssl://{sslEndPoint}");
        }

        if (settings.QuicBindEndPoint is not "none")
        {
            IPEndPoint quicEndPoint = IPEndPoint.Parse(settings.QuicBindEndPoint); 
            serverTasks.Add(
                StartQuic(
                    stubCollection,
                    quicEndPoint,
                    CancellationTokenSource.CreateLinkedTokenSource(cts.Token).Token));
            Console.WriteLine($"Listening on quic://{quicEndPoint}");
        }
        
        Console.WriteLine("Press CTRL + C to exit...");

        await Task.WhenAll(serverTasks);
        return 0;
    }

    static Task StartTcp(StubCollection stubCollection, IPEndPoint bindEndPoint, CancellationToken ct)
    {
        ct.Register(() => Console.WriteLine("Stopping TCP server"));

        IServer<IPEndPoint> tcpServer = new TcpServer(
            bindEndPoint,
            stubCollection,
            new DefaultServerProtocolNegotiation(ConnectionSettings.None));

        return tcpServer.ListenAsync(ct);
    }

    static Task StartSslOverTcp(StubCollection stubCollection, IPEndPoint bindEndPoint, CancellationToken ct)
    {
        ct.Register(() => Console.WriteLine("Stopping SSL over TCP server"));

        ConnectionSettings connectionSettings = new()
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                Status = SharedCapabilityEnablement.EnabledMandatory,
                CertificatePath = string.Empty,
                CertificatePassword = "c3rtp4ssw0rd"
            },
            Buffering = ConnectionSettings.BufferingSettings.Disabled,
            Compression = ConnectionSettings.CompressionSettings.Disabled
        };

        IServer<IPEndPoint> sslServer = new TcpServer(
            bindEndPoint,
            stubCollection,
            new DefaultServerProtocolNegotiation(connectionSettings));

        return sslServer.ListenAsync(ct);
    }

    static Task StartQuic(StubCollection stubCollection, IPEndPoint bindEndPoint, CancellationToken ct)
    {
        ct.Register(() => Console.WriteLine("Stopping QUIC server"));
        
        ConnectionSettings connectionSettings = new()
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                Status = SharedCapabilityEnablement.EnabledMandatory,
                CertificatePath = string.Empty,
                CertificatePassword = "c3rtp4ssw0rd",
                ApplicationProtocols = new []
                {
                    new SslApplicationProtocol("miloworkbench")
                }
            },
            Buffering = ConnectionSettings.BufferingSettings.Disabled,
            Compression = ConnectionSettings.CompressionSettings.Disabled
        };

        IServer<IPEndPoint> quicServer = new QuicServer(
            bindEndPoint,
            stubCollection,
            new DefaultQuicServerProtocolNegotiation(connectionSettings));

        return quicServer.ListenAsync(ct);
    }
}
