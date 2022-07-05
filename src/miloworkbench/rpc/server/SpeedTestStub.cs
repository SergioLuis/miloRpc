using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Serialization;
using miloRpc.TestWorkBench.Rpc.Shared;
using miloRpc.TestWorkBench.Server;

namespace miloRpc.TestWorkBench.Rpc.Server;

public class SpeedTestStub : IStub
{
    internal SpeedTestStub(SpeedTestService service)
    {
        mService = service;
    }
    
    bool IStub.CanHandleMethod(IMethodId method)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(method);
        return dmi >= Methods.SpeedTestService.First
               && dmi <= Methods.SpeedTestService.Last;
    }

    IEnumerable<IMethodId> IStub.GetHandledMethods()
    {
        return new[]
        {
            Methods.SpeedTestService.SpeedTestUpload,
            Methods.SpeedTestService.SpeedTestDownload
        };
    }

    Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(methodId);
        Func<BinaryReader, IConnectionContext, Func<CancellationToken>, RpcNetworkMessages> fn =
            (Methods.Id) dmi.Id switch
            {
                Methods.Id.SpeedTestUpload => RunSpeedTestUploadAsync,
                Methods.Id.SpeedTestDownload => RunSpeedTestDownloadAsync,
                _ => throw new NotImplementedException()
            };

        return Task.FromResult(fn.Invoke(reader, connectionContext, beginMethodRunCallback));
    }
    
    RpcNetworkMessages RunSpeedTestDownloadAsync(
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        NetworkMessage<int> req = new();
        req.Deserialize(reader);

        CancellationToken _ = beginMethodRunCallback();
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(req.Val1);
        mService.DownloadAsync(req.Val1, buffer);

        ByteArrayMessage res = new(buffer, req.Val1);
        res.SetDisposedCallback(() => ArrayPool<byte>.Shared.Return(buffer));

        return new RpcNetworkMessages(req, res);
    }

    RpcNetworkMessages RunSpeedTestUploadAsync(
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SpeedTestService.MaxBlockSize);
        try
        {
            ByteArrayMessage req = new ByteArrayMessage(buffer, 0);
            req.Deserialize(reader);

            ReadOnlyMemory<byte> data = new(req.Bytes, 0, req.Length);

            CancellationToken _ = beginMethodRunCallback();

            mService.UploadAsync(data);

            VoidNetworkMessage res = new();

            return new RpcNetworkMessages(req, res);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    readonly SpeedTestService mService;
}
