using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace IscsiTarget.Service
{
    public class IpcServer
    {
        private const string PipeName = "IscsiTargetServicePipe";

        public async Task StartAsync()
        {
            Console.WriteLine("IPC Server starting...");
            while (true) // Keep server running to accept multiple client connections
            {
                try
                {
                    using (var serverStream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        Console.WriteLine($"NamedPipeServerStream '{PipeName}' created. Waiting for connection...");
                        await serverStream.WaitForConnectionAsync();
                        Console.WriteLine("Client connected.");

                        // TODO: Implement message handling logic
                        // Example: Read command from client, process, send response
                        byte[] buffer = new byte[1024];
                        int bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"Received from client: {message}");

                        string response = $"Server acknowledges: {message}";
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                        await serverStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        Console.WriteLine("Response sent to client.");

                        serverStream.Disconnect(); // Disconnect this client, ready for next
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"IPC Server Error: {ex.Message}");
                    // Consider adding a delay before retrying to avoid tight loop on persistent errors
                    await Task.Delay(1000);
                }
            }
        }

        public void Stop()
        {
            // TODO: Implement any cleanup if necessary
            Console.WriteLine("IPC Server stopping...");
        }
    }
}