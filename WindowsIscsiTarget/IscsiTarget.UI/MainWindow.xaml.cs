using System.Windows;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace IscsiTarget.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IpcClient _ipcClient;
        private const string ServiceName = "IscsiTargetService";

        public MainWindow()
        {
            InitializeComponent();
            _ipcClient = new IpcClient();
            UpdateServiceStatus();
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MessagesTextBlock.Text += "Attempting to start service...\n";
            // This UI button won't directly start the service due to permissions.
            // It should ideally send a command to the service if it's running to perform an action,
            // or inform the user to start it via services.msc (if not already running).
            // For now, we'll simulate sending a command and checking status.
            
            // Check if service exists and try to start it (requires admin rights for UI to do this directly)
            try
            {
                ServiceController sc = new ServiceController(ServiceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, System.TimeSpan.FromSeconds(10));
                    MessagesTextBlock.Text += "Service start request sent.\n";
                }
                else
                {
                    MessagesTextBlock.Text += "Service is not stopped. Current status: " + sc.Status + "\n";
                }
            }
            catch (System.Exception ex)
            {
                MessagesTextBlock.Text += $"Error managing service: {ex.Message}. Try running UI as Administrator or manage service via services.msc.\n";
            }
            UpdateServiceStatus();
            string response = await _ipcClient.SendCommandAsync("STATUS");
            MessagesTextBlock.Text += $"IPC Response: {response}\n";
        }

        private async void StopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MessagesTextBlock.Text += "Attempting to stop service...\n";
            // Similar to start, direct stop from UI needs admin rights.
            try
            {
                ServiceController sc = new ServiceController(ServiceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, System.TimeSpan.FromSeconds(10));
                    MessagesTextBlock.Text += "Service stop request sent.\n";
                }
                else
                {
                    MessagesTextBlock.Text += "Service is not running. Current status: " + sc.Status + "\n";
                }
            }
            catch (System.Exception ex)
            {
                MessagesTextBlock.Text += $"Error managing service: {ex.Message}. Try running UI as Administrator or manage service via services.msc.\n";
            }
            UpdateServiceStatus();
            // Optionally send a command if needed, though stopping is usually direct.
            // string response = await _ipcClient.SendCommandAsync("STOP_OPERATIONS"); // Example command
            // MessagesTextBlock.Text += $"IPC Response: {response}\n";
        }

        private void UpdateServiceStatus()
        {
            try
            {
                ServiceController sc = new ServiceController(ServiceName);
                StatusTextBlock.Text = sc.Status.ToString();
                StartServiceButton.IsEnabled = (sc.Status == ServiceControllerStatus.Stopped);
                StopServiceButton.IsEnabled = (sc.Status == ServiceControllerStatus.Running);
            }
            catch (System.Exception ex)
            {
                StatusTextBlock.Text = "Error";
                MessagesTextBlock.Text += $"Could not get service status: {ex.Message}. Ensure service is installed.\n";
                StartServiceButton.IsEnabled = true; // Allow attempt if status unknown
                StopServiceButton.IsEnabled = true;  // Allow attempt if status unknown
            }
        }
    }
}