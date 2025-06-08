using System;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace IscsiTarget.UI
{
    public class IpcClient
    {
        private const string PipeName = "IscsiTargetServicePipe";

        public async Task<string> SendCommandAsync(string command)
        {
            try
            {
                using (var clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation))
                {
                    Console.WriteLine($"Attempting to connect to pipe '{PipeName}'...");
                    await clientStream.ConnectAsync(5000); // Timeout after 5 seconds
                    Console.WriteLine("Connected to pipe.");

                    byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                    await clientStream.WriteAsync(commandBytes, 0, commandBytes.Length);
                    Console.WriteLine($"Sent to server: {command}");

                    byte[] buffer = new byte[1024];
                    int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Received from server: {response}");

                    return response;
                }
            }
            catch (TimeoutException tex)
            {
                Console.WriteLine($"IPC Client Error: Connection timed out. {tex.Message}");
                return "Error: Connection to service timed out.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC Client Error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }
    }
}