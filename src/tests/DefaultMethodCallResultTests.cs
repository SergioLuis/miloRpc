using System;
using System.IO;

using NUnit.Framework;

using dotnetRpc.Core.Client;
using dotnetRpc.Core.Server;
using dotnetRpc.Core.Shared;

namespace dotnetRpc.Tests;

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
            MethodCallResult.OK,
            null);

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out Exception? ex);

        Assert.That(result, Is.EqualTo(MethodCallResult.OK));
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
            MethodCallResult.NotSupported,
            null);

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out Exception? ex);

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
            ex);

        ms.Position = 0;

        MethodCallResult result = readMethodCallResult.Read(
            reader, out bool isResultAvailable, out Exception? propagatedEx);

        Assert.That(result, Is.EqualTo(MethodCallResult.Failed));
        Assert.That(isResultAvailable, Is.False);
        Assert.That(propagatedEx, Is.Not.Null.And.TypeOf<InvalidOperationException>());

        InvalidOperationException? deserEx = propagatedEx as InvalidOperationException;
        Assert.That(deserEx!.Message, Is.EqualTo("This exception is custom"));
    }
}
