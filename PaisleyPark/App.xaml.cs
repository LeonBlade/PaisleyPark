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

		protected override void RegisterTypes(IContainerRegistry containerRegistry) {}

		protected override void OnStartup(StartupEventArgs e)
		{
			Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;

			base.OnStartup(e);
		}

		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			if (e.Exception != null)
			{
				var logger = NLog.LogManager.GetCurrentClassLogger();
				logger.Error(e.Exception, "Unhandled Exception");
			}
			e.Handled = true;
		}
	}
}
