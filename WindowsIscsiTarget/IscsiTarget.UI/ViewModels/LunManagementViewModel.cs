using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using IscsiTarget.Shared; // For LunDto

namespace IscsiTarget.UI.ViewModels
{
    // Basic ICommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }

    public class LunManagementViewModel : INotifyPropertyChanged
    {
        private readonly IpcClient _ipcClient;
        private ObservableCollection<LunDto> _luns;
        private LunDto _selectedLun;

        public ObservableCollection<LunDto> Luns
        {
            get => _luns;
            set
            {
                _luns = value;
                OnPropertyChanged();
            }
        }

        public LunDto SelectedLun
        {
            get => _selectedLun;
            set
            {
                _selectedLun = value;
                OnPropertyChanged();
                ((RelayCommand)RemoveLunCommand).CanExecuteChanged += (s, e) => { }; // Force re-evaluation
            }
        }

        public ICommand AddLunCommand { get; }
        public ICommand RemoveLunCommand { get; }
        public ICommand RefreshLunsCommand { get; }

        public LunManagementViewModel()
        {
            _ipcClient = new IpcClient();
            Luns = new ObservableCollection<LunDto>();
            AddLunCommand = new RelayCommand(async param => await AddLunAsync(), param => CanAddLun());
            RemoveLunCommand = new RelayCommand(async param => await RemoveLunAsync(param as LunDto), param => CanRemoveLun(param as LunDto));
            RefreshLunsCommand = new RelayCommand(async param => await LoadLunsAsync());

            // Load LUNs on initialization
            Task.Run(async () => await LoadLunsAsync());
        }

        private async Task LoadLunsAsync()
        {
            // Placeholder: Replace with actual IPC call to get LUNs
            // Example: string response = await _ipcClient.SendCommandAsync("GET_LUNS");
            // Deserialize response into List<LunDto> and update Luns collection
            // For now, add some dummy data
            await Task.Delay(100); // Simulate async work
            Luns.Clear();
            Luns.Add(new LunDto { LunId = 0, Name = "Sample LUN 1", VhdxPath = "C:\vhd\disk1.vhdx", SizeBytes = 10L * 1024 * 1024 * 1024, Status = "Online" });
            Luns.Add(new LunDto { LunId = 1, Name = "Sample LUN 2", VhdxPath = "C:\vhd\disk2.vhdx", SizeBytes = 20L * 1024 * 1024 * 1024, Status = "Offline" });
        }

        private bool CanAddLun()
        {
            // TODO: Implement logic, e.g., check if service is running
            return true;
        }

        private async Task AddLunAsync()
        {
            // Placeholder: Implement LUN addition logic
            // 1. Show a dialog to get VHDX path and LUN ID
            // 2. Send "ADD_LUN <path> <id>" command via IPC
            // 3. Refresh LUN list
            // Example: await _ipcClient.SendCommandAsync($"ADD_LUN {newLunPath} {newLundId}");
            System.Windows.MessageBox.Show("Add LUN functionality not yet implemented.", "Add LUN", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            await LoadLunsAsync(); // Refresh list
        }

        private bool CanRemoveLun(LunDto lun)
        {
            return lun != null;
        }

        private async Task RemoveLunAsync(LunDto lun)
        {
            if (lun == null) return;

            // Placeholder: Implement LUN removal logic
            // 1. Confirm removal with user
            // 2. Send "REMOVE_LUN <lunId>" command via IPC
            // 3. Refresh LUN list
            // Example: await _ipcClient.SendCommandAsync($"REMOVE_LUN {lun.LunId}");
            var result = System.Windows.MessageBox.Show($"Are you sure you want to remove LUN {lun.LunId} ({lun.Name})?", "Remove LUN", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                System.Windows.MessageBox.Show($"Remove LUN {lun.LunId} functionality not yet implemented.", "Remove LUN", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            await LoadLunsAsync(); // Refresh list
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}