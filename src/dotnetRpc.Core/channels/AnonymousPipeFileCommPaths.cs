using System;
using System.IO;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeFileCommPaths
{
    public string Directory { get; }

    public string Prefix { get; }

    public AnonymousPipeFileCommPaths(string directory, string prefix)
    {
        Directory = directory;
        Prefix = prefix;
    }

    public string GetConnBeginningFilePath(ulong connectionId) => Path.Combine(
        Directory,
        string.Concat(Prefix, connectionId, FileExtensions.Beginning));

    public string GetConnOfferedFilePath(ulong connectionId) => Path.Combine(
        Directory,
        string.Concat(Prefix, connectionId, FileExtensions.Offered));

    public string GetConnRequestingFilePath(ulong connectionId) => Path.Combine(
        Directory,
        string.Concat(Prefix, connectionId, FileExtensions.Requesting));

    public string GetConnRequestedFilePath(ulong connectionId) => Path.Combine(
        Directory,
        string.Concat(Prefix, connectionId, FileExtensions.Requested));

    public string GetConnEstablishedFilePath(ulong connectionId) => Path.Combine(
        Directory,
        string.Concat(Prefix, connectionId, FileExtensions.Established));

    public void SetConnectionAsOffered(ulong connectionId)
        => File.Move(
            GetConnBeginningFilePath(connectionId),
            GetConnOfferedFilePath(connectionId),
            false);

    public void SetConnectionAsEstablished(ulong connectionId)
        => File.Move(
            GetConnRequestedFilePath(connectionId),
            GetConnEstablishedFilePath(connectionId),
            false);

    public bool IsConnRequestedFilePath(ReadOnlySpan<char> filePath)
        => filePath.StartsWith(Prefix) && filePath.EndsWith(FileExtensions.Requested);

    public ulong ParseConnRequestedId(ReadOnlySpan<char> filePath)
        => ulong.Parse(filePath[Prefix.Length..filePath.IndexOf('.')]);

    internal FileSystemWatcher BuildWatcherMonitorRequestedConns()
    {
        FileSystemWatcher result = new FileSystemWatcher(Directory);
        result.Filters.Add($"*{FileExtensions.Requested}");
        if (!string.IsNullOrEmpty(Prefix))
            result.Filters.Add($"{Prefix}*");
        result.IncludeSubdirectories = false;

        return result;
    }

    static class FileExtensions
    {
        public const string Beginning = ".conn_begining";
        public const string Offered = ".conn_offered";
        public const string Requesting = ".conn_requesting";
        public const string Requested = ".conn_requested";
        public const string Established = ".conn_established";
    }


}
