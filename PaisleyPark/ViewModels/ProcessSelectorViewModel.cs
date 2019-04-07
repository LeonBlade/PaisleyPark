using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
    public class ProcessSelectorViewModel : BindableBase
    {
        public ObservableCollection<Process> ProcessList { get; set; }
        public Process SelectedProcess { get; set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand OKCommand { get; private set; }
        public bool? DialogResult { get; set; }

        public ProcessSelectorViewModel()
        {
            RefreshCommand = new DelegateCommand(OnRefresh);
            OKCommand = new DelegateCommand<Window>(OnOK);
        }

        private void OnRefresh()
        {
            // Get list of processes for the game.
            var procs = Process.GetProcessesByName("ffxiv_dx11");
            // Set the process list to this array.
            ProcessList = new ObservableCollection<Process>(procs);
        }

        private void OnOK(Window window)
        {
            if (SelectedProcess == null)
            {
                MessageBox.Show("Please select a process before clicking OK.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            window.Close();
        }
    }
}
