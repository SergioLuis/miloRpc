using System;
using System.IO;
using System.Linq;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeFileCommPaths
{
    public string BaseDirectory { get; }
    public string Prefix { get; }

    public AnonymousPipeFileCommPaths(string baseDirectory, string prefix)
    {
        BaseDirectory = baseDirectory;
        Prefix = prefix;
    }

    public ulong? GetNextOfferedConnection()
        => Directory.EnumerateFiles(
                BaseDirectory,
                GetSearchPattern(FileExtensions.Offered))
            .Select(Path.GetFileName)
            .Select(ParseConnectionId)
            .FirstOrDefault();

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

    internal FileSystemWatcher BuildListenerWatcher()
    {
        FileSystemWatcher result = new(BaseDirectory);
        result.Filters.Add(GetSearchPattern(FileExtensions.Reserved));
        result.Filters.Add(GetSearchPattern(FileExtensions.Requested));
        result.IncludeSubdirectories = false;

        return result;
    }

    internal FileSystemWatcher BuildClientWatcher()
    {
        FileSystemWatcher result = new(BaseDirectory);
        result.Filters.Add(GetSearchPattern(FileExtensions.Established));
        result.IncludeSubdirectories = false;

        return result;
    }

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
