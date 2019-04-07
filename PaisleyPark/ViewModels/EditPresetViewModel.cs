using Prism.Commands;
using Prism.Mvvm;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class EditPresetViewModel : BindableBase
	{
		public string Name              { get; set; }
		public bool UseCurrentWaymarks  { get; set; }
        public bool? DialogResult       { get; private set; }

        public ICommand OKCommand     { get; private set; }
		public ICommand CancelCommand   { get; private set; }

		public EditPresetViewModel()
		{
            OKCommand = new DelegateCommand<Window>(OnEdit);
			CancelCommand = new DelegateCommand<Window>(OnCancel);
		}

        /// <summary>
		/// When clicking the edit button.
		/// </summary>
		/// <param name="window">Window this was called from.</param>
		private void OnEdit(Window window)
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
