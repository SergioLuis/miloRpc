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

    public ulong? GetNextOfferedConnection()
        => Directory.EnumerateFiles(
                mDirectory,
                GetSearchPattern(FileExtensions.Offered))
            .Select(Path.GetFileName)
            .Select(ParseConnectionId)
            .FirstOrDefault();

    public string BuildConnectionBeginningFilePath(ulong connectionId)
        => Path.Combine(mDirectory, string.Concat(mPrefix, connectionId, FileExtensions.Beginning));

    public string BuildConnectionOfferedFilePath(ulong connectionId)
        => Path.Combine(mDirectory, string.Concat(mPrefix, connectionId, FileExtensions.Offered));

    public string BuildConnectionReservedFilePath(ulong connectionId)
        => Path.Combine(mDirectory, string.Concat(mPrefix, connectionId, FileExtensions.Reserved));

    public string BuildConnectionRequestedFilePath(ulong connectionId)
        => Path.Combine(mDirectory, string.Concat(mPrefix, connectionId, FileExtensions.Requested));

    public string BuildConnectionEstablishedFilePath(ulong connectionId)
        => Path.Combine(mDirectory, string.Concat(mPrefix, connectionId, FileExtensions.Established));

    public void SetConnectionOffered(ulong connectionId)
        => File.Move(
            BuildConnectionBeginningFilePath(connectionId),
            BuildConnectionOfferedFilePath(connectionId),
            false);

    public bool SetConnectionReserved(
        ulong connectionId, out string connRequestingFilePath)
    {
        try
        {
            connRequestingFilePath = BuildConnectionReservedFilePath(connectionId);
            File.Move(
                BuildConnectionOfferedFilePath(connectionId),
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
            BuildConnectionReservedFilePath(connectionId),
            BuildConnectionRequestedFilePath(connectionId),
            false);

    public void SetConnectionEstablished(ulong connectionId)
        => File.Move(
            BuildConnectionRequestedFilePath(connectionId),
            BuildConnectionEstablishedFilePath(connectionId),
            false);

    public bool IsConnectionRequestedFilePath(ReadOnlySpan<char> filePath)
        => filePath.StartsWith(mPrefix) && filePath.EndsWith(FileExtensions.Requested);

    public bool IsConnectionEstablishedFilePath(ReadOnlySpan<char> filePath)
        => filePath.StartsWith(mPrefix) && filePath.EndsWith(FileExtensions.Established);

    public ulong ParseConnectionId(string? filePath)
        => string.IsNullOrEmpty(filePath) ? 0 : ParseConnectionId(filePath.AsSpan());

    public ulong ParseConnectionId(ReadOnlySpan<char> filePath)
        => ulong.Parse(filePath[mPrefix.Length..filePath.IndexOf('.')]);

    internal FileSystemWatcher BuildListenerWatcher()
    {
        FileSystemWatcher result = new(mDirectory);
        result.Filters.Add(GetSearchPattern(FileExtensions.Reserved));
        result.Filters.Add(GetSearchPattern(FileExtensions.Requested));
        result.IncludeSubdirectories = false;

        return result;
    }

    internal FileSystemWatcher BuildClientWatcher()
    {
        FileSystemWatcher result = new(mDirectory);
        result.Filters.Add(GetSearchPattern(FileExtensions.Established));
        result.IncludeSubdirectories = false;

        return result;
    }

    string GetSearchPattern(string extension)
        => string.IsNullOrEmpty(mPrefix) ? $"*{extension}" : $"{mPrefix}*{extension}";

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
