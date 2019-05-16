using Prism.Commands;
using Prism.Mvvm;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
    public class UpdaterViewModel : BindableBase
    {
        private string Html { get; set; }
        public string HTML { get => Html; set { Html = "<style>body{font-family:Helvetica,Arial,sans-serif;}ul{margin:0;padding:0;list-style-position: inside;}</style>" + value; } }
        public string UpdateString { get; set; }
        public bool? DialogResult { get; private set; }
        public ICommand NoCommand { get; private set; }
        public ICommand InstallCommand { get; private set; }

        public UpdaterViewModel()
        {
            NoCommand = new DelegateCommand<Window>(OnClose);
            InstallCommand = new DelegateCommand<Window>(OnInstall);
        }

        private void OnClose(Window window)
        {
            DialogResult = false;
            window.Close();
        }

        private void OnInstall(Window window)
        {
            DialogResult = true;
            window.Close();
        }
    }
}
