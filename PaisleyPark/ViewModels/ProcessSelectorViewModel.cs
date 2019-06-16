using PaisleyPark.Models;
using PaisleyPark.Views;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        public ICommand SwitchCommand { get; private set; }
		public ICommand ManageCommand { get; private set; }
        public bool? DialogResult { get; set; }

        public ProcessSelectorViewModel()
        {
            RefreshCommand = new DelegateCommand(OnRefresh);
            OKCommand = new DelegateCommand<Window>(OnOK);
            SwitchCommand = new DelegateCommand(OnSwitch);
			ManageCommand = new DelegateCommand(OnManage);
        }

        private void OnRefresh()
        {
            // Get list of processes for the game.
            var procs = Process.GetProcessesByName("ffxiv_dx11");
            // Set the process list to this array.
            ProcessList = new ObservableCollection<Process>(procs);
        }

        /// <summary>
        /// Switches to the selected process.
        /// </summary>
        private void OnSwitch()
        {
            // Ensure process is selected.
            if (SelectedProcess == null)
                return;

            // Switch to the main window.
            SwitchToThisWindow(SelectedProcess.MainWindowHandle, false);
        }

		/// <summary>
		/// Opens the preset manager to import/export when game isn't open.
		/// </summary>
		private void OnManage()
		{
			var settings = Settings.Load();

			// Create new preset manager window.
			var win = new PresetManager();

			// Pull view model from window.
			var vm = win.DataContext as PresetManagerViewModel;

			// Populate the presets with our current presets as a new instance.
			vm.Presets = new ObservableCollection<Preset>(settings.Presets);

			// Check if we're saving changes.
			if (win.ShowDialog() == true)
			{
				// Reassign presets in user settings to the ones managed by the window.
				settings.Presets = vm.Presets;
				// Save the settings.
				Settings.Save(settings);
			}
		}

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool turnon);

        /// <summary>
        /// When you click OK.
        /// </summary>
        /// <param name="window"></param>
        private void OnOK(Window window)
        {
            // Ensure process is selected.
            if (SelectedProcess == null)
            {
                MessageBox.Show("Please select a process before clicking OK.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Successful close.
            DialogResult = true;
            // Close the window.
            window.Close();
        }
    }
}
