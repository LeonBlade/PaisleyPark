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
		/// Adding new preset.
		/// </summary>
		private void OnAddPreset()
		{
			// Create window for add preset.
			var win = new NewPreset();

			// Get the VM for this window.
			var vm = win.DataContext as NewPresetViewModel;

			// Show the dialog for the preset window.
			if (win.ShowDialog() == true)
			{
				// Initialize the creation of our preset with the preset name.
				var p = new Preset() { Name = vm.PresetName };

				// If we use the current waymarks, set them in our preset.
				if (vm.UseCurrentWaymarks)
				{
					p.A = GameMemory.A;
					p.B = GameMemory.B;
					p.C = GameMemory.C;
					p.D = GameMemory.D;
					p.One = GameMemory.One;
					p.Two = GameMemory.Two;
				}

				// Add the preset.
				Presets.Add(p);
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
			try
			{
				if (SelectedItem == null)
					return;

				var win = new EditPreset();
				var vm = win.DataContext as EditPresetViewModel;
				vm.Name = SelectedItem.Name;

				// Dialog comes back as true.
				if (win.ShowDialog() == true)
				{
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
		/// <param name="win"></param>
		private void OnOK(Window win)
		{
			// Set that we're saving changes.
			DialogResult = true;
			// Close the window.
			win.Close();
		}
    }
}
