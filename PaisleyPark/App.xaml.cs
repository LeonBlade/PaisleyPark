using PaisleyPark.Views;
using Prism.Ioc;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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

		protected override void OnStartup(StartupEventArgs e)
		{
			Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;

			base.OnStartup(e);
		}

		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			if (e.Exception != null)
			{
				using (StreamWriter writer = new StreamWriter("error.log", true))
				{
					writer.WriteLine("-----------Start-----------" + DateTime.Now);
					writer.WriteLine("Error Message: " + e.Exception.Message);
					writer.WriteLine("Stack Trace: " + e.Exception.StackTrace);
					if (e.Exception.InnerException != null)
					{
						writer.WriteLine("-----------Inner Exception-----------" + DateTime.Now);
						writer.WriteLine("Inner Exception Message: " + e.Exception.InnerException.Message);
						writer.WriteLine("Inner Exception Message: " + e.Exception.InnerException.StackTrace);
					}
					writer.WriteLine("-----------End-----------" + DateTime.Now);
				}
			}
			e.Handled = true;
		}
	}
}
