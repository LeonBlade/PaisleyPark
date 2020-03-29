using Newtonsoft.Json;
using PaisleyPark.Common;
using PaisleyPark.Models;
using PaisleyPark.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class PresetManagerViewModel : BindableBase
    {
		public Preset SelectedItem { get; set; }
		public ObservableCollection<Preset> Presets { get; set; } = new ObservableCollection<Preset>();
		public ICommand AddCommand { get; private set; }
		public ICommand RemoveCommand { get; private set; }
		public ICommand OKCommand { get; private set; }
		public ICommand EditCommand { get; private set; }
		public ICommand ImportCommand { get; private set; }
		public ICommand ExportCommand { get; private set; }

		public bool DialogResult { get; private set; }
		private Memory GameMemory;
		private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		public PresetManagerViewModel(IEventAggregator ea)
		{
			ea.GetEvent<GameMemoryUpdateEvent>().Subscribe(OnGameMemoryUpdate);

			// Create our commands.
			AddCommand		= new DelegateCommand(OnAddPreset);
			RemoveCommand	= new DelegateCommand(OnRemovePreset);
			OKCommand		= new DelegateCommand<Window>(OnOK);
			EditCommand		= new DelegateCommand(OnEdit);
			ImportCommand	= new DelegateCommand(OnImport);
			ExportCommand	= new DelegateCommand(OnExport);
		}

		/// <summary>
		/// Called when GameMemory is updated.
		/// </summary>
		/// <param name="gameMemory"></param>
		private void OnGameMemoryUpdate(object gameMemory)
		{
			// Assign the GameMemory to the memory from the update method.
			GameMemory = gameMemory as Memory;
		}

		/// <summary>
		/// Used to check if you can do a certain function on this view.
		/// </summary>
		/// <returns></returns>
		private bool CheckCanDo()
		{
			if (Application.Current.MainWindow == null || !Application.Current.MainWindow.IsInitialized)
			{
				MessageBox.Show("You can't create or modify a preset right now.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Adding new preset.
		/// </summary>
		private void OnAddPreset()
		{
			// Can't do unless game is loaded.
			if (!CheckCanDo())
				return;

			try 
			{
				// Create window for add preset.
				var win = new NewPreset
				{
					// Owner set to MainWindow.
					Owner = Application.Current.MainWindow
				};

				// Get the VM for this window.
				var vm = win.DataContext as NewPresetViewModel;

				// Show the dialog for the preset window.
				if (win.ShowDialog() == true)
				{
					// Initialize the creation of our preset with the preset name.
					var p = new Preset() { Name = vm.Name };

					// If we use the current waymarks, set them in our preset.
					if (vm.UseCurrentWaymarks)
					{
						p.A = GameMemory.A;
						p.B = GameMemory.B;
						p.C = GameMemory.C;
						p.D = GameMemory.D;
						p.One = GameMemory.One;
						p.Two = GameMemory.Two;
						p.Three = GameMemory.Three;
						p.Four = GameMemory.Four;
					}

					// Add the preset.
					Presets.Add(p);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Something happened while creating your preset!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				logger.Error(ex, "Exception while adding a new preset.");
			}
		}

		/// <summary>
		/// Removing selected preset.
		/// </summary>
		private void OnRemovePreset()
		{
			if (SelectedItem != null)
				if (MessageBox.Show(
					"Are you sure you want to delete this preset?", 
					"Paisley Park", 
					MessageBoxButton.YesNo, 
					MessageBoxImage.Warning) == MessageBoxResult.Yes)
					Presets.Remove(SelectedItem);
		}

		/// <summary>
		/// When editing the selected preset.
		/// </summary>
		private void OnEdit()
		{
			// Can't do unless game is loaded.
			if (!CheckCanDo())
				return;

			try
			{
				if (SelectedItem == null)
					return;

                // Create Edit preset window.
				var win = new EditPreset
                {
                    // Owner set to MainWindow.
                    Owner = Application.Current.MainWindow
                };

                // Get the view model.
                var vm = win.DataContext as EditPresetViewModel;

                // Set the name to the selected name in the preset list.
				vm.Name = SelectedItem.Name;

				// Dialog comes back as true.
				if (win.ShowDialog() == true)
				{
                    // Set the selected name to the viewmodel's name.
					SelectedItem.Name = vm.Name;

					// If we use the current waymarks, set them in our preset.
					if (vm.UseCurrentWaymarks)
					{
						SelectedItem.A = GameMemory.A;
						SelectedItem.B = GameMemory.B;
						SelectedItem.C = GameMemory.C;
						SelectedItem.D = GameMemory.D;
						SelectedItem.One = GameMemory.One;
						SelectedItem.Two = GameMemory.Two;
						SelectedItem.Three = GameMemory.Three;
						SelectedItem.Four = GameMemory.Four;
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Something happened while editing your preset!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				logger.Error(ex, "Exception while editing selected item.");
			}
		}

		/// <summary>
		/// When you click OK button.
		/// </summary>
		/// <param name="window"></param>
		private void OnOK(Window window)
		{
			// Set that we're saving changes.
			DialogResult = true;
			// Close the window.
			window.Close();
		}

        /// <summary>
        /// Clicking import button.
        /// </summary>
		private void OnImport()
		{
			// Create new import window.
			var import = new Import
            {
                // Owner set to MainWindow.
                Owner = Application.Current.MainWindow
            };

            // Get the view model.
            var vm = import.DataContext as ImportViewModel;

			// Add the imported preset if it came back okay.
			if (import.ShowDialog() == true && vm.ImportedPreset != null)
				Presets.Add(vm.ImportedPreset);
		}

        /// <summary>
        /// Clicking export button.
        /// </summary>
		private void OnExport()
		{
			if (SelectedItem == null)
				return;

            // Serialized string initialize.
			string cereal = "";
			try
			{
                // Serialize the selected item.
				cereal = JsonConvert.SerializeObject(SelectedItem);
                // Set in the clipboard (need to use SetDataObject because SetText crashes).
				Clipboard.SetDataObject(cereal);
				MessageBox.Show(
					string.Format("Copied preset \"{0}\" to your clipboard!", SelectedItem.Name),
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Tried to copy serialized object to clipboard.\n---BEGIN---\n{0}\n---END---\n", cereal);
				MessageBox.Show(
					"An error occured while trying to copy this preset to your clipboard.",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);

			}
		}
    }
}
