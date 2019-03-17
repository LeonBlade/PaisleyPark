using Prism.Commands;
using Prism.Mvvm;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class EditPresetViewModel : BindableBase
	{
		public string Name { get; set; }
		public ICommand OKCommand { get; private set; }
		public ICommand CancelCommand { get; private set; }
		public bool DidOK { get; private set; }
		public bool? DialogResult { get; set; } = false;

		public EditPresetViewModel()
		{
			OKCommand = new DelegateCommand<Window>(OnOK);
			CancelCommand = new DelegateCommand<Window>(OnCancel);
		}

		private void OnOK(Window win)
		{
			DialogResult = true;
			win.Close();
		}

		private void OnCancel(Window win)
		{
			DialogResult = false;
			win.Close();
		}
	}
}
