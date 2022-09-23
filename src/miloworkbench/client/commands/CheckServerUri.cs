using System;
using System.Linq;

namespace miloRpc.TestWorkBench.Client.Commands;

public static class CheckServerUri
{
    public static bool Check(Uri serverUri)
    {
        string[] acceptedSchemes = {"tcp", "ssl", "quic"};
        if (acceptedSchemes.Contains(serverUri.Scheme, StringComparer.InvariantCultureIgnoreCase))
            return true;

        Console.Error.WriteLine($"Invalid scheme '{serverUri.Scheme}'");
        Console.Error.WriteLine($"Accepted schemes are {string.Join(", ", acceptedSchemes)}.");
        return false;
    }
}
