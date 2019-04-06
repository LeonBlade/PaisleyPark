using Prism.Commands;
using Prism.Mvvm;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class NewPresetViewModel : BindableBase
    {
		public string PresetName { get; set; }
		public bool UseCurrentWaymarks { get; set; }
		public bool DialogResult { get; private set; }

		public ICommand CreateCommand { get; private set; }
		public ICommand CloseCommand { get; private set; }

		public NewPresetViewModel()
		{
			CreateCommand = new DelegateCommand<Window>(OnCreate);
			CloseCommand = new DelegateCommand<Window>(OnCancel);
		}

		/// <summary>
		/// When clicking the create button.
		/// </summary>
		/// <param name="window">Window this was called from.</param>
		private void OnCreate(Window window)
		{
			// Set DidCreate to true.
			DialogResult = true;
			// Close the window.
			window.Close();
		}

		/// <summary>
		/// When clicking cancel button.
		/// </summary>
		/// <param name="window">Window this was called from.</param>
		private void OnCancel(Window window)
		{
			// Set DidCreate to false.
			DialogResult = false;
			// Close the window.
			window.Close();
		}
	}
}
