using Ionic.Zip;
using Markdig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace PaisleyParkUpdater
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public string UpdateString { get; set; }
		public string HTML { get; set; }
		private readonly JObject json;
		public string ApplicationPath;
		private readonly string temp = Path.Combine(Path.GetTempPath(), "PaisleyPark");

		public MainWindow()
		{
			InitializeComponent();

			DataContext = this;

			// Set the security protocol, mainly for Windows 7 users.
			ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

			// Initialize variable for the current PP version.
			var forceCheckUpdate = false;

			// Get the current version of the application.
			var result = Version.TryParse(FileVersionInfo.GetVersionInfo(Path.Combine(Environment.CurrentDirectory, "PaisleyPark.exe")).FileVersion, out Version CurrentVersion);
			if (!result)
			{
				MessageBox.Show(
					"There was an error when trying to read the current version of Paisley Park, you will be prompted to download the latest version.",
					"Paisley Park Updater",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				// Force to check the update.
				forceCheckUpdate = true;
			}

			// Create request for Github REST API for the latest release of Paisley Park.
			if (WebRequest.Create("https://api.github.com/repos/LeonBlade/PaisleyPark/releases/latest") is HttpWebRequest request)
			{
				request.Method = "GET";
				request.UserAgent = "PaisleyPark";
				request.ServicePoint.Expect100Continue = false;

				try
				{
					using (var r = new StreamReader(request.GetResponse().GetResponseStream()))
					{
						// Get the JSON as a JObject to get the properties dynamically.
						json = JsonConvert.DeserializeObject<JObject>(r.ReadToEnd());
						// Get tag name and remove the v in front.
						var tag_name = json["tag_name"].Value<string>().Substring(1);
						// Form release version from this string.
						var releaseVersion = new Version(tag_name);
						// Check if the release is newer.
						if (releaseVersion > CurrentVersion || forceCheckUpdate)
						{
							// Create HTML out of the markdown in body.
							var html = Markdown.ToHtml(json["body"].Value<string>());
							// Set the update string
							UpdateString = $"Paisley Park {releaseVersion.VersionString()} is now available, you have {CurrentVersion.VersionString()}. Would you like to download it now?";
							// Set HTML in the window.
							HTML = "<style>body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji,Segoe UI Symbol;ul{margin:0;padding:0;list-style-position:inside;}</style>" + html;
						}
						else
						{
							// MessageBox.Show("You're up to date!", "Paisley Park Updater", MessageBoxButton.OK, MessageBoxImage.Information);
							Application.Current.Shutdown();
						}
					}
				}
				catch (Exception)
				{
					var response = MessageBox.Show(
						"Failed to fetch the latest version! Would you like to visit the page manually to check for the latest release manually?",
						"Paisley Park Updater",
						MessageBoxButton.YesNo,
						MessageBoxImage.Error
					);
					if (response == MessageBoxResult.Yes)
					{
						// Visit the latest releases page on GitHub to download the latest Paisley Park.
						Process.Start("https://github.com/LeonBlade/PaisleyPark/releases/latest");
					}
				}
			}
		}

		/// <summary>
		/// Ensure the temp path exists.
		/// </summary>
		private void ValidateTempPath()
		{
			// Create temp diretory if it doesn't exist.
			if (!Directory.Exists(temp))
				Directory.CreateDirectory(temp);
		}

		/// <summary>
		/// When clicking the install button.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnInstallClick(object sender, RoutedEventArgs e)
		{
			// Use web client to download the update.
			using (var wc = new WebClient())
			{
				// Ensure the temp path exists.
				ValidateTempPath();

				// Temporary Paisley Park zip path.
				var tPPZip = Path.Combine(temp, "PaisleyPark.zip");

				// Delete existing zip file.
				if (File.Exists(tPPZip))
					File.Delete(tPPZip);

				// Download the file. 
				wc.DownloadFile(new Uri(json["assets"][0]["browser_download_url"].Value<string>()), tPPZip);

				try
				{
					// Close Paisley Park if it's running.
					var pp = Process.GetProcessesByName("PaisleyPark")[0];
					// Close the mainwindow shutting down the process.
					pp.CloseMainWindow();
					// Try to wait for it to shut down gracefully.
					if (!pp.WaitForExit(10000))
					{
						// If the application is still alive.
						MessageBox.Show(
							"Paisley Park can't be shutdown gracefully. You may need to restart FFXIV if it's currently open after the update is completed.",
							"Paisley Park Updater",
							MessageBoxButton.OK,
							MessageBoxImage.Warning
						);
						// Kill the process.
						pp.Kill();
					}
				}
				catch (Exception) { }

				// Rename the updater file to allow for overwrite.
				if (File.Exists(Path.Combine(Environment.CurrentDirectory, ".PPU.old")))
					File.Delete(Path.Combine(Environment.CurrentDirectory, ".PPU.old"));
				File.Move(Path.Combine(Environment.CurrentDirectory, "PaisleyParkUpdater.exe"), Path.Combine(Environment.CurrentDirectory, ".PPU.old"));

				// Unzip and overwrite all files.
				using (var zip = ZipFile.Read(tPPZip))
				{
					foreach (var z in zip)
						z.Extract(Environment.CurrentDirectory, ExtractExistingFileAction.OverwriteSilently);
				}

				// Inform the user to manually start Paisley Park again.
				MessageBox.Show("Update complete! Please start Paisley Park to use the latest version!", "Paisley Park Updater", MessageBoxButton.OK, MessageBoxImage.Information);

				// Shutdown the application we're done here.
				Application.Current.Shutdown();
			}
		}

		/// <summary>
		/// Clicking the no button.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnNoClick(object sender, RoutedEventArgs e)
		{
			// Close the updater.
			Application.Current.Shutdown();
		}
	}
}
