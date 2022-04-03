using System;
using System.IO;
using System.Linq;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeFileCommPaths
{
    public AnonymousPipeFileCommPaths(string directory, string prefix)
    {
        mDirectory = directory;
        mPrefix = prefix;
    }

    public ulong? GetNextConnectionOffered()
        => Directory.EnumerateFiles(
                mDirectory,
                GetSearchPattern(mPrefix, FileExtensions.Offered))
            .Select(ParseConnectionId)
            .FirstOrDefault();

    public string GetConnBeginningFilePath(ulong connectionId) => Path.Combine(
        mDirectory,
        string.Concat(mPrefix, connectionId, FileExtensions.Beginning));

    public string GetConnOfferedFilePath(ulong connectionId) => Path.Combine(
        mDirectory,
        string.Concat(mPrefix, connectionId, FileExtensions.Offered));

    public string GetConnReservedFilePath(ulong connectionId) => Path.Combine(
        mDirectory,
        string.Concat(mPrefix, connectionId, FileExtensions.Reserved));

    public string GetConnRequestedFilePath(ulong connectionId) => Path.Combine(
        mDirectory,
        string.Concat(mPrefix, connectionId, FileExtensions.Requested));

    public string GetConnEstablishedFilePath(ulong connectionId) => Path.Combine(
        mDirectory,
        string.Concat(mPrefix, connectionId, FileExtensions.Established));

    public void SetConnectionOffered(ulong connectionId)
        => File.Move(
            GetConnBeginningFilePath(connectionId),
            GetConnOfferedFilePath(connectionId),
            false);

    public bool SetConnectionReserved(
        ulong connectionId, out string connRequestingFilePath)
    {
        try
        {
            connRequestingFilePath = GetConnReservedFilePath(connectionId);
            File.Move(
                GetConnOfferedFilePath(connectionId),
                connRequestingFilePath,
                false);
            return true;
        }
        catch
        {
            connRequestingFilePath = string.Empty;
            return false;
        }
    }

    public void SetConnectionRequested(ulong connectionId)
        => File.Move(
            GetConnReservedFilePath(connectionId),
            GetConnRequestedFilePath(connectionId),
            false);

    public void SetConnectionEstablished(ulong connectionId)
        => File.Move(
            GetConnRequestedFilePath(connectionId),
            GetConnEstablishedFilePath(connectionId),
            false);

    public bool IsConnRequestedFilePath(ReadOnlySpan<char> filePath)
        => filePath.StartsWith(mPrefix) && filePath.EndsWith(FileExtensions.Requested);

    public bool IsConnEstablishedFilePath(ReadOnlySpan<char> filePath)
        => filePath.StartsWith(mPrefix) && filePath.EndsWith(FileExtensions.Established);

    public ulong ParseConnectionId(string filePath)
        => ParseConnectionId(filePath.AsSpan());

    public ulong ParseConnectionId(ReadOnlySpan<char> filePath)
        => ulong.Parse(filePath[mPrefix.Length..filePath.IndexOf('.')]);

    internal FileSystemWatcher BuildListenerWatcher()
    {
        FileSystemWatcher result = new(mDirectory);
        result.Filters.Add($"*{FileExtensions.Reserved}");
        result.Filters.Add($"*{FileExtensions.Requested}");
        if (!string.IsNullOrEmpty(mPrefix))
            result.Filters.Add($"{mPrefix}*");
        result.IncludeSubdirectories = false;

        return result;
    }

    internal FileSystemWatcher BuildClientWatcher()
    {
        FileSystemWatcher result = new(mDirectory);
        result.Filters.Add($"*{FileExtensions.Established}");
        if (!string.IsNullOrEmpty(mPrefix))
            result.Filters.Add($"{mPrefix}*");
        result.IncludeSubdirectories = false;

        return result;
    }

    static string GetSearchPattern(string prefix, string extension)
        => string.IsNullOrEmpty(prefix) ? $"*{extension}" : $"{prefix}*{extension}";

    readonly string mDirectory;
    readonly string mPrefix;

    static class FileExtensions
    {
        public const string Beginning = ".conn_begining";
        public const string Offered = ".conn_offered";
        public const string Reserved = ".conn_reserved";
        public const string Requested = ".conn_requested";
        public const string Established = ".conn_established";
    }
}
