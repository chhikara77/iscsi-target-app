using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using IscsiTarget.Core.Configuration;
using IscsiTarget.Core.Storage;
using Serilog;

namespace IscsiTarget.Core
{
    public class IscsiTargetServer : IDisposable
    {
        private readonly TargetConfiguration _configuration;
        private readonly LunManager _lunManager;
        private TcpListener? _listener;
        private readonly List<Task> _sessionTasks = new List<Task>();
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        static IscsiTargetServer()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("iscsi-target-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        public IscsiTargetServer(TargetConfiguration configuration, LunManager lunManager)
        {
            _configuration = configuration;
            _lunManager = lunManager;
            // Static constructor handles logger initialization
        }

        public void Start()
        {
            if (_isRunning)
            {
                Log.Warning("iSCSI Target Server is already running.");
                return;
            }

            try
            {
                _listener = new TcpListener(_configuration.ListeningIPAddress, _configuration.ListeningPort);
                _listener.Start();
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource(); // Reset cancellation token source
                Log.Information($"iSCSI Target Server started on {_configuration.ListeningIPAddress}:{_configuration.ListeningPort}");

                // Start accepting client connections asynchronously
                Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start iSCSI Target Server");
                _isRunning = false;
                // Optionally rethrow or handle more gracefully
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            if (_listener == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Log.Information("Waiting for a client connection...");
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    Log.Information($"Client connected: {client.Client.RemoteEndPoint}");

                    // Create a new session for the client
                    var session = new IscsiSession(client, _configuration, _lunManager);
                    var sessionTask = session.ProcessAsync(cancellationToken);
                    _sessionTasks.Add(sessionTask);

                    // Optionally, remove completed tasks to free resources
                    _sessionTasks.RemoveAll(t => t.IsCompleted);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information("Client acceptance loop was canceled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accepting client connection");
            }
            finally
            {
                Log.Information("Stopped accepting new client connections.");
            }
        }

        public void Stop()
        {
            if (!_isRunning || _listener == null)
            {
                Log.Warning("iSCSI Target Server is not running or listener is not initialized.");
                return;
            }

            Log.Information("Stopping iSCSI Target Server...");
            _isRunning = false;
            _cancellationTokenSource.Cancel(); // Signal cancellation to all operations

            try
            {
                _listener.Stop();
                Log.Information("TCP listener stopped.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping TCP listener");
            }

            // Wait for all session tasks to complete with a timeout
            try
            {
                Task.WaitAll(_sessionTasks.ToArray(), TimeSpan.FromSeconds(10)); // Adjust timeout as needed
                Log.Information("All client sessions terminated.");
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Log.Warning(ex, "Exception during session termination");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during shutdown of session tasks");
            }
            finally
            {
                _sessionTasks.Clear();
                Log.Information("iSCSI Target Server stopped.");
            }
        }

        public void Dispose()
        {
            Stop(); // Ensure server is stopped before disposing
            _cancellationTokenSource.Dispose();
            Log.CloseAndFlush(); // Ensure all logs are written
            GC.SuppressFinalize(this);
        }
    }
}