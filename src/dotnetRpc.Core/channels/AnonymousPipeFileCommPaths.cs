using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeFileCommPaths
{
    public const ulong INVALID_CONN_ID = 0;

    public string BaseDirectory { get; }
    public string Prefix { get; }

    public AnonymousPipeFileCommPaths(string baseDirectory, string prefix)
    {
        BaseDirectory = baseDirectory;
        Prefix = prefix;
    }

    public ulong GetNextOfferedConnection()
        => Directory.EnumerateFiles(BaseDirectory, GetSearchPattern(FileExtensions.Offered))
            .Select(s => Path.GetFileName(s))
            .Select(ParseConnectionId)
            .Min();

    public ulong GetNextRequestedConnection()
    {
        List<ulong> requestedConnectionIds =
            Directory.EnumerateFiles(BaseDirectory, GetSearchPattern(FileExtensions.Requested))
                .Select(s => Path.GetFileName(s))
                .Select(ParseConnectionId)
                .ToList();

        return !requestedConnectionIds.Any()
            ? INVALID_CONN_ID
            : requestedConnectionIds.Min();
    }

    public ulong GetNextEstablishedConnection()
    {
        List<ulong> establishedConnectionIds =
            Directory.EnumerateFiles(BaseDirectory, GetSearchPattern(FileExtensions.Established))
                .Select(s => Path.GetFileName(s))
                .Select(ParseConnectionId)
                .ToList();

        return !establishedConnectionIds.Any()
            ? INVALID_CONN_ID
            : establishedConnectionIds.Min();
    }

    public string BuildConnectionBeginningFilePath(ulong connectionId)
        => Path.Combine(BaseDirectory, string.Concat(Prefix, connectionId, FileExtensions.Beginning));

    public string BuildConnectionOfferedFilePath(ulong connectionId)
        => Path.Combine(BaseDirectory, string.Concat(Prefix, connectionId, FileExtensions.Offered));

    public string BuildConnectionReservedFilePath(ulong connectionId)
        => Path.Combine(BaseDirectory, string.Concat(Prefix, connectionId, FileExtensions.Reserved));

    public string BuildConnectionRequestedFilePath(ulong connectionId)
        => Path.Combine(BaseDirectory, string.Concat(Prefix, connectionId, FileExtensions.Requested));

    public string BuildConnectionEstablishedFilePath(ulong connectionId)
        => Path.Combine(BaseDirectory, string.Concat(Prefix, connectionId, FileExtensions.Established));

    public void SetConnectionOffered(ulong connectionId)
        => File.Move(
            BuildConnectionBeginningFilePath(connectionId),
            BuildConnectionOfferedFilePath(connectionId),
            false);

    public bool SetConnectionReserved(
        ulong connectionId, out string connReservedFilePath)
    {
        try
        {
            string connOfferedFilePath = BuildConnectionOfferedFilePath(connectionId);
            connReservedFilePath = BuildConnectionReservedFilePath(connectionId);

            File.Move(
                connOfferedFilePath,
                connReservedFilePath,
                false);
            return true;
        }
        catch
        {
            connReservedFilePath = string.Empty;
            return false;
        }
    }

    public void SetConnectionRequested(ulong connectionId)
        => File.Move(
            BuildConnectionReservedFilePath(connectionId),
            BuildConnectionRequestedFilePath(connectionId),
            false);

    public void SetConnectionEstablished(ulong connectionId)
        => File.Move(
            BuildConnectionRequestedFilePath(connectionId),
            BuildConnectionEstablishedFilePath(connectionId),
            false);

    public bool IsConnectionReservedFileName(ReadOnlySpan<char> fileName)
        => fileName.StartsWith(Prefix) && fileName.EndsWith(FileExtensions.Reserved);

    public bool IsConnectionRequestedFileName(ReadOnlySpan<char> fileName)
        => fileName.StartsWith(Prefix) && fileName.EndsWith(FileExtensions.Requested);

    public bool IsConnectionEstablishedFileName(ReadOnlySpan<char> fileName)
        => fileName.StartsWith(Prefix) && fileName.EndsWith(FileExtensions.Established);

    public ulong ParseConnectionId(string? fileName)
        => string.IsNullOrEmpty(fileName) ? 0 : ParseConnectionId(fileName.AsSpan());

    public ulong ParseConnectionId(ReadOnlySpan<char> fileName)
        => ulong.Parse(fileName[Prefix.Length..fileName.IndexOf('.')]);

    public string GetSearchPattern(string extension)
        => string.IsNullOrEmpty(Prefix) ? $"*{extension}" : $"{Prefix}*{extension}";

    public static class FileExtensions
    {
        public const string Beginning = ".conn_begining";
        public const string Offered = ".conn_offered";
        public const string Reserved = ".conn_reserved";
        public const string Requested = ".conn_requested";
        public const string Established = ".conn_established";
    }
}
