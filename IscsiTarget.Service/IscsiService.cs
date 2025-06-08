using System.ServiceProcess;
using System.Threading.Tasks;
// using IscsiTarget.Core; // Assuming IscsiTargetServer and LunManager are in this namespace

namespace IscsiTarget.Service
{
    public partial class IscsiService : ServiceBase
    {
        private IpcServer _ipcServer;
        // private IscsiTargetServer _iscsiTargetServer;
        // private LunManager _lunManager;

        public IscsiService()
        {
            // InitializeComponent(); // If you have a designer for service components
            ServiceName = "IscsiTargetService";
            _ipcServer = new IpcServer();
            // _iscsiTargetServer = new IscsiTargetServer(); // Placeholder
            // _lunManager = new LunManager(); // Placeholder
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Load configuration for IscsiTargetServer and LunManager
            // Example: 
            // var config = TargetConfiguration.Load(); 
            // _lunManager.Load(config);
            // _iscsiTargetServer.Configure(config, _lunManager);

            // Start the iSCSI target server
            // Task.Run(() => _iscsiTargetServer.Start());
            System.Diagnostics.EventLog.WriteEntry(ServiceName, "iSCSI Target Server starting... (Placeholder)", System.Diagnostics.EventLogEntryType.Information);

            // Start the IPC server
            Task.Run(() => _ipcServer.StartAsync());
            System.Diagnostics.EventLog.WriteEntry(ServiceName, "IPC Server starting...", System.Diagnostics.EventLogEntryType.Information);
        }

        protected override void OnStop()
        {
            _ipcServer.Stop();
            System.Diagnostics.EventLog.WriteEntry(ServiceName, "IPC Server stopping...", System.Diagnostics.EventLogEntryType.Information);

            // Stop the iSCSI target server
            // _iscsiTargetServer.Stop();
            System.Diagnostics.EventLog.WriteEntry(ServiceName, "iSCSI Target Server stopping... (Placeholder)", System.Diagnostics.EventLogEntryType.Information);
            
            // TODO: Release any other resources
        }
    }
}