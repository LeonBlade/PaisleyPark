using PaisleyPark.Views;
using Prism.Ioc;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaisleyPark
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		protected override Window CreateShell() => Container.Resolve<MainWindow>();

		protected override void RegisterTypes(IContainerRegistry containerRegistry) { }

		private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		protected override void OnStartup(StartupEventArgs e)
		{
			RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
			Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			NLog.LogManager.Shutdown();
			base.OnExit(e);
		}

		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			if (e.Exception != null)
			{
				logger.Error(e.Exception, "Unhandled Exception");
			}
			e.Handled = true;
		}
	}
}
