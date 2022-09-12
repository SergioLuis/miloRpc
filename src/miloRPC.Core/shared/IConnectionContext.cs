using System.Collections.Generic;
using System.Net;

namespace miloRPC.Core.Shared;

public interface IConnectionContext
{
    uint ConnectionId { get; }
    string UnderlyingProtocol { get; }
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint RemoteEndPoint { get; }

    void Add(object key, object value);
    bool ContainsKey(object key);
    bool Remove(object key);
    void Clear();
    bool TryGetValue(object key, out object? value);
}

internal class ConnectionContext : IConnectionContext
{
    uint IConnectionContext.ConnectionId => mConnectionId;
    string IConnectionContext.UnderlyingProtocol => mUnderlyingProtocolName;
    IPEndPoint IConnectionContext.LocalEndPoint => mLocalEndPoint;
    IPEndPoint IConnectionContext.RemoteEndPoint => mRemoteEndPoint;

    internal ConnectionContext(
        uint connectionId,
        string underlyingProtocolName,
        IPEndPoint localEndPoint,
        IPEndPoint remoteEndPoint)
    {
        mConnectionId = connectionId;
        mUnderlyingProtocolName = underlyingProtocolName;
        mLocalEndPoint = localEndPoint;
        mRemoteEndPoint = remoteEndPoint;
    }

    void IConnectionContext.Add(object key, object value)
    {
        lock (mSyncLock)
        {
            mData.Add(key, value);
        }
    }

    bool IConnectionContext.ContainsKey(object key)
    {
        lock (mSyncLock)
        {
            return mData.ContainsKey(key);
        }
    }

    bool IConnectionContext.Remove(object key)
    {
        lock (mSyncLock)
        {
            return mData.Remove(key);
        }
    }

    void IConnectionContext.Clear()
    {
        lock (mSyncLock)
        {
            mData.Clear();
        }
    }

    bool IConnectionContext.TryGetValue(object key, out object? value)
    {
        lock (mSyncLock)
        {
            return mData.TryGetValue(key, out value);
        }
    }

    internal bool UpdateRemoteEndPoint(IPEndPoint newRemoteEndPoint)
    {
        if (newRemoteEndPoint.Equals(mRemoteEndPoint))
            return false;

        // In some protocols (at least QUIC) the client is free to change
        // its IP address whilst maintaining the connection
        mRemoteEndPoint = newRemoteEndPoint;
        return true;
    }

    readonly uint mConnectionId;
    readonly string mUnderlyingProtocolName;
    readonly IPEndPoint mLocalEndPoint;
    IPEndPoint mRemoteEndPoint;

    readonly Dictionary<object, object> mData = new();
    readonly object mSyncLock = new();
}
