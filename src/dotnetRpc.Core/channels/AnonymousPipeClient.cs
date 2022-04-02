using System;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Core.Channels;

/**
 * AnonymousPipe channel flow (this class implements client-side behavior):
 *
 * 0) Server creates file [prefix]_[ID].conn_beginning
 *    - 'prefix' will be a string shared with the client. By filtering files
 *      by their prefix, several NamedPipeListener instances can work with the
 *      same directory
 *    - 'ID' is a rotatory unsigned long, auto-incremented with each connection
 *
 * 1) Server side creates an AnonymousPipeServerStream ServerToClient-Server (PipeDirection.Out)
 * 2) Server side writes the Pipe's ClientHandle in file [prefix]_[ID].conn_beginning
 * 3) Server side renames file [prefix]_[ID].conn_beginning to [prefix]_[ID].conn_offered
 *    - First creating the file, then renaming it prevents race conditions with
 *      the clients while writing the Pipe's ClientHandle
 *
 * 4) Client side scans for files starting with [prefix] and extension 'conn_offered'
 *    - Client sorts the files by their [ID]
 *    - Client will try to choose first the one with the lowest [ID]
 * 5) Client side renames file [prefix]_[ID].conn_offered to [prefix]_[ID].conn_requesting
 *    - Because a rename is an atomic operation, this prevents more than one
 *      thread/process requesting the same NamedPipe connection
 *      The first one that renames the file succeeds
 * 6) Client side reads ClientHandle from [prefix]_[ID].conn_requesting
 * 7) Client side creates an AnonymousPipeClientStream ServerToClient-Client (PipeDirection.In)
 *    with the ClientHandle read from [prefix]_[ID].conn_requesting
 * 7) Client side creates an AnonymousPipeServerStream ClientToServer-Client (PipeDirection.Out)
 * 8) Client side writes the Pipe's ClientHandle in file [prefix]_[ID].conn_requesting
 * 9) Client side renames file [prefix]_[ID].conn_requesting to [prefix]_[ID].conn_requested
 *
 * 10) Server side reads ClientHandle from [prefix]_[ID].conn_requested
 * 11) Server side creates an AnonymousPipeClientStream ClientToServer-Server (PipeDirection.In)
 *     with the ClientHandle read from [prefix]_[ID].conn_requested
 * 12) Server side renames file [prefix]_[ID].conn_requested to [prefix]_[ID].conn_established
 *
 * Now both parties are ready to communicate!
 */
public class AnonymousPipeClient
{
    public async Task<IRpcChannel> ConnectAsync()
        => await ConnectAsync(CancellationToken.None);

    public async Task<IRpcChannel> ConnectAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
