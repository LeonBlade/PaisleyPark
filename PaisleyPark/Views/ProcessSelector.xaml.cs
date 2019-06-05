using System.Windows;
using PaisleyPark.ViewModels;

namespace PaisleyPark.Views
{
    /// <summary>
    /// Interaction logic for ProcessSelector.xaml
    /// </summary>
    public partial class ProcessSelector : Window
    {
        public ProcessSelector()
        {
            InitializeComponent();

            this.Closed += (_, __) => (this.DataContext as ProcessSelectorViewModel).CloseCommand.Execute(null);
        }
    }
}
