using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Serialization;
using miloRpc.TestWorkBench.Rpc.Shared;

namespace miloRpc.TestWorkBench.Rpc.Server;

public class FileTransferServiceStub : IStub
{
    public FileTransferServiceStub(IFileTransferService service)
    {
        mService = service;
    }

    bool IStub.CanHandleMethod(IMethodId method)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(method);
        return dmi >= Methods.FileTransferService.First
               && dmi <= Methods.FileTransferService.Last;
    }

    IEnumerable<IMethodId> IStub.GetHandledMethods()
    {
        return new[]
        {
            Methods.FileTransferService.UploadFile,
            Methods.FileTransferService.DownloadFile
        };
    }

    async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(methodId);
        Func<BinaryReader, IConnectionContext, Func<CancellationToken>, Task<RpcNetworkMessages>> fn =
            (Methods.Id) dmi.Id switch
            {
                Methods.Id.UploadFile => RunUploadFileAsync,
                Methods.Id.DownloadFile => RunDownloadFileAsync,
                _ => throw new NotImplementedException()
            };

        return await fn(reader, connectionContext, beginMethodRunCallback);
    }

    async Task<RpcNetworkMessages> RunUploadFileAsync(
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        DestinationStreamMessage<string> req = new();
        req.Deserialize(reader);

        Contract.Assert(req.Val1 is not null);
        Contract.Assert(req.Stream is not null);

        await mService.UploadFileAsync(req.Val1, req.Stream, beginMethodRunCallback());

        VoidNetworkMessage res = new();

        return new RpcNetworkMessages(req, res);
    }

    async Task<RpcNetworkMessages> RunDownloadFileAsync(
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        NetworkMessage<string> req = new();
        req.Deserialize(reader);

        Contract.Assert(req.Val1 is not null);

        Stream st = await mService.DownloadFileAsync(req.Val1, beginMethodRunCallback());

        SourceStreamMessage res = new();
        res.Stream = st;

        return new RpcNetworkMessages(req, res);
    }

    readonly IFileTransferService mService;
}
