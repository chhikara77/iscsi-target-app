using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace IscsiTarget.Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            // InitializeComponent(); // If you have a designer for components

            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // ServiceProcessInstaller properties
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem; // Or NetworkService, LocalService depending on needs
            serviceProcessInstaller.Password = null;
            serviceProcessInstaller.Username = null;

            // ServiceInstaller properties
            serviceInstaller.ServiceName = "IscsiTargetService";
            serviceInstaller.DisplayName = "iSCSI Target Service";
            serviceInstaller.Description = "Manages iSCSI target functionality.";
            serviceInstaller.StartType = ServiceStartMode.Automatic; // Or Manual, Disabled

            // Add installers to collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}