using System;
using System.IO;

using NUnit.Framework;

using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;

namespace miloRPC.Tests;

[TestFixture]
public class DefaultMethodCallResultTests
{
    [Test]
    public void MethodCall_Finished_Ok()
    {
        IWriteMethodCallResult writeMethodCallResult = DefaultWriteMethodCallResult.Instance;
        IReadMethodCallResult readMethodCallResult = DefaultReadMethodCallResult.Instance;

        using MemoryStream ms = new();
        using BinaryReader reader = new(ms);
        using BinaryWriter writer = new(ms);

        writeMethodCallResult.Write(
            writer,
            MethodCallResult.Ok);

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out RpcException? ex);

        Assert.That(result, Is.EqualTo(MethodCallResult.Ok));
        Assert.That(isResultAvailable, Is.True);
        Assert.That(ex, Is.Null);
    }

    [Test]
    public void MethodCall_Was_Not_Supported()
    {
        IWriteMethodCallResult writeMethodCallResult = DefaultWriteMethodCallResult.Instance;
        IReadMethodCallResult readMethodCallResult = DefaultReadMethodCallResult.Instance;

        using MemoryStream ms = new();
        using BinaryReader reader = new(ms);
        using BinaryWriter writer = new(ms);

        writeMethodCallResult.Write(
            writer,
            MethodCallResult.NotSupported);

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out RpcException? ex);

        Assert.That(result, Is.EqualTo(MethodCallResult.NotSupported));
        Assert.That(isResultAvailable, Is.False);
        Assert.That(ex, Is.Null);
    }

    [Test]
    public void MethodCall_Finished_With_An_Exception()
    {
        InvalidOperationException ex = new("This exception is custom");

        IWriteMethodCallResult writeMethodCallResult = DefaultWriteMethodCallResult.Instance;
        IReadMethodCallResult readMethodCallResult = DefaultReadMethodCallResult.Instance;

        using MemoryStream ms = new();
        using BinaryReader reader = new(ms);
        using BinaryWriter writer = new(ms);

        writeMethodCallResult.Write(
            writer,
            MethodCallResult.Failed,
            RpcException.FromException(ex));

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out RpcException? propagatedEx);

        Assert.That(result, Is.EqualTo(MethodCallResult.Failed));
        Assert.That(isResultAvailable, Is.False);
        Assert.That(propagatedEx, Is.Not.Null);
        Assert.That(propagatedEx, Has.Message.EqualTo("This exception is custom"));
        Assert.That(propagatedEx!.ExceptionType, Contains.Substring("InvalidOperationException"));
    }
}
