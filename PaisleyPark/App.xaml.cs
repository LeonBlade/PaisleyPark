using PaisleyPark.Views;
using Prism.Ioc;
using System.Windows;

namespace PaisleyPark
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		protected override Window CreateShell() => Container.Resolve<MainWindow>();

		protected override void RegisterTypes(IContainerRegistry containerRegistry)
		{

		}
	}
}
