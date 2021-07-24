using Nancy.Hosting.Self;
using PaisleyPark.Common;
using PaisleyPark.Models;
using PaisleyPark.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;

namespace PaisleyPark.ViewModels
{
	public class MainWindowViewModel : BindableBase
	{
		public static IEventAggregator EventAggregator { get; private set; }
		private Memory.Mem mem { get; set; } = new Memory.Mem();
		private BackgroundWorker Worker;
		public static Models.Memory GameMemory { get; set; } = new Models.Memory();
		public Settings UserSettings { get; set; }
		public Preset CurrentPreset { get; set; }
		public string WindowTitle { get; set; } = "Paisley Park";
		public bool IsServerStarted { get; set; } = false;
		public bool IsServerStopped { get => !IsServerStarted; }
		private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
		private NancyHost Host;
		private Thread WaymarkThread;
		private readonly Version CurrentVersion;
		private long WaymarkAddr;
		public string DiscordUri { get; private set; } = "https://discord.gg/hq3DnBa";

		public DelegateCommand LoadPresetCommand { get; private set; }
		public DelegateCommand ManagePresetsCommand { get; private set; }
		public DelegateCommand ClosingCommand { get; private set; }
		public DelegateCommand StartServerCommand { get; private set; }
		public DelegateCommand StopServerCommand { get; private set; }
		public DelegateCommand DiscordCommand { get; private set; }

		public MainWindowViewModel(IEventAggregator ea)
		{
			// Test if the Event Aggregator is null.
			if (ea == null)
			{
				MessageBox.Show("Event Aggregator is null, unable to start.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				logger.Error("Event Aggregator is null");
				Application.Current.Shutdown();
			}

			// Set the security protocol, mainly for Windows 7 users.
			ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

			// Store reference to the event aggregator.
			EventAggregator = ea;

			logger.Info("--- PAISLEY PARK START ---");

			// Deleting any old updater file.
			if (File.Exists(".PPU.old"))
				File.Delete(".PPU.old");

			try
			{
				// Get the version from the assembly.
				CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				logger.Debug($"Current Version: {CurrentVersion}");

				// Set window title.
				WindowTitle = string.Format("Paisley Park {0}", CurrentVersion.VersionString());
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Couldn't get the application version.");
				MessageBox.Show("Couldn't get Paisley Park's version to set the title.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				WindowTitle = "Paisley Park";
			}

			// Fetch an update.
			logger.Info("Fetching update...");
			FetchUpdate();

			// Load the settings file.
			logger.Info("Loading settings...");
			try
			{
				UserSettings = Settings.Load();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error trying to load settings file.");
				MessageBox.Show("Could not load your settings file!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
			}

			logger.Debug("Setting up the events.");
			// Subscribe to the waymark event from the REST server.
			EventAggregator.GetEvent<WaymarkEvent>().Subscribe(waymarks =>
			{
				WriteWaymark(waymarks.A, 0);
				WriteWaymark(waymarks.B, 1);
				WriteWaymark(waymarks.C, 2);
				WriteWaymark(waymarks.D, 3);
				WriteWaymark(waymarks.One, 4);
				WriteWaymark(waymarks.Two, 5);
				WriteWaymark(waymarks.Three, 6);
				WriteWaymark(waymarks.Four, 7);
			});

			logger.Debug("Subscribing to Load Preset event.");
			try
			{
				// Subscribe to the load preset event from the REST server.
				var loadPresetEvent = EventAggregator.GetEvent<LoadPresetEvent>();
				if (loadPresetEvent == null)
					throw new Exception("Couldn't get LoadPresetEvent");
				loadPresetEvent.Subscribe(name =>
				{
					var preset = UserSettings.Presets.FirstOrDefault(x =>
						string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

					if (preset == null)
					{
						logger.Info($"Unkown preset {name}.");
						return;
					}

					WriteWaymark(preset.A, 0);
					WriteWaymark(preset.B, 1);
					WriteWaymark(preset.C, 2);
					WriteWaymark(preset.D, 3);
					WriteWaymark(preset.One, 4);
					WriteWaymark(preset.Two, 5);
					WriteWaymark(preset.Three, 6);
					WriteWaymark(preset.Four, 7);
				});
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Couldn't subscribe to LoadPresetEvent.");
				MessageBox.Show("Couldn't subscribe to Load Preset event.", "Paisely Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
			}

			logger.Debug("Subscribing to Save Preset event.");
			try
			{
				// Subscribe to the save preset event from the REST server.
				var savePresetEvent = EventAggregator.GetEvent<SavePresetEvent>();
				if (savePresetEvent == null)
					throw new Exception("Couldn't get SavePresetEvent");
				savePresetEvent.Subscribe(name =>
				{
					var preset = UserSettings.Presets.FirstOrDefault(x =>
						string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

					try
					{
						if (preset == null)
						{
							preset = new Preset();
							Application.Current.Dispatcher.Invoke(() => UserSettings.Presets.Add(preset));
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show("Could not save the preset.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
						logger.Error(ex, "Could not save preset");
					}

					preset.Name = name;
					preset.A = GameMemory.A;
					preset.B = GameMemory.B;
					preset.C = GameMemory.C;
					preset.D = GameMemory.D;
					preset.One = GameMemory.One;
					preset.Two = GameMemory.Two;
					preset.Three = GameMemory.Three;
					preset.Four = GameMemory.Four;

					Settings.Save(UserSettings);
				});
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Couldn't subscribe to SavePresetEvent.");
				MessageBox.Show("Couldn't subscribe to Save Preset event.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
			}

			logger.Debug("Creating commands.");
			try
			{
				// Create the commands.
				LoadPresetCommand = new DelegateCommand(LoadPreset);
				ClosingCommand = new DelegateCommand(OnClose);
				ManagePresetsCommand = new DelegateCommand(OnManagePresets);
				StartServerCommand = new DelegateCommand(OnStartServer).ObservesCanExecute(() => IsServerStopped);
				StopServerCommand = new DelegateCommand(OnStopServer).ObservesCanExecute(() => IsServerStarted);
				DiscordCommand = new DelegateCommand(OnDiscord);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Couldn't create a command.");
				MessageBox.Show("Couldn't create commands.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
			}

			// Listen for property changed.
			UserSettings.PropertyChanged += OnPropertyChanged;

			logger.Info("Initializing...");
			// Prepare for new game launch.
			if (!Initialize())
			{
				Application.Current.Shutdown();
			}
		}

		/// <summary>
		/// Starts everything needed for this process.
		/// </summary>
		/// <returns>Successful initialization.</returns>
		private bool Initialize()
		{
			logger.Info("Initializing Memory.dll...");
			// Initialize Nhaama.
			InitializeMemory();

			logger.Info("Starting server...");
			// Check autostart and start the HTTP server if it's true.
			if (UserSettings.HTTPAutoStart)
				OnStartServer();

			return true;
		}

		/// <summary>
		/// Fetch an update for the applicaton.
		/// </summary>
		private void FetchUpdate()
		{
			try
			{
				Process.Start("PaisleyParkUpdater.exe");
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Updater didn't work.");
				var result = MessageBox.Show(
					"Could not run the updater. Would you like to visit the releases page to check for a new update manually?",
					"Paisley Park",
					MessageBoxButton.YesNo,
					MessageBoxImage.Error
				);
				// Launch the web browser to the latest release.
				if (result == MessageBoxResult.Yes)
				{
					Process.Start("https://github.com/LeonBlade/PaisleyPark/releases/latest");
				}
			}
		}

		/// <summary>
		/// Clicking the Discord link.
		/// </summary>
		private void OnDiscord()
		{
			Process.Start(new ProcessStartInfo(DiscordUri));
		}

		/// <summary>
		/// User Settings changed.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// When specific properties change we save them immediately.
			if (e.PropertyName == "PlacementDelay" || e.PropertyName == "Port" || e.PropertyName == "HTTPAutoStart")
			{
				try
				{
					// Save the settings file.
					Settings.Save(UserSettings);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Couldn't save settings");
					MessageBox.Show("Couldn't save settings!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		/// <summary>
		/// Initialize Nhaama for use in the application.
		/// </summary>
		private async void InitializeMemory()
		{
			// Get the processes of XIV.
			var procs = Process.GetProcessesByName("ffxiv_dx11");

			// More than one process.
			if (procs.Length > 1 || procs.Length == 0)
			{
				// Show the process selector window.
				if (!ShowProcessSelector(procs))
					return;
			}
			else
				// Get the Nhaama process from the first process that matches for XIV.
				mem.OpenProcess(procs[0].Id);

			if (mem.theProc == null)
			{
				logger.Error("Couldn't get Nhaama process");
				MessageBox.Show("Coult not get the Nhaama Process for FFXIV.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Enable raising events.
			mem.theProc.EnableRaisingEvents = true;

			// Listen to some stuff.
			mem.theProc.Exited += (_, e) =>
			{
				MessageBox.Show(
					"Looks like FINAL FANTASY XIV crashed or shut down.",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Exclamation
				);
				logger.Info("FFXIV Shutdown or Crashed!");

				// Start the initialization process again.
				Application.Current.Dispatcher.Invoke(() => Initialize());
			};

			// Console.WriteLine($"{mem.theProc.MainModule.BaseAddress:X} {(mem.theProc.MainModule.BaseAddress + mem.theProc.MainModule.ModuleMemorySize):X}");
			var start = mem.theProc.MainModule.BaseAddress.ToInt64();
			var stop = (start + mem.theProc.MainModule.ModuleMemorySize);

			var asmAddr = (await mem.AoBScan(start, stop, "48 8d 0d ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 3b c3 75 ?? ff c7 3b fe")).FirstOrDefault();
			if (asmAddr == 0)
				throw new Exception("Couldn't find waymark address");

			var read = mem.ReadBytes((asmAddr + 3).ToString("X"), 8);
			var offset = BitConverter.ToInt32(read, 0);
			WaymarkAddr = asmAddr + offset + 4 + 3 + 0x1B0;

			// Create new worker.
			Worker = new BackgroundWorker();
			// Set worker loop.
			Worker.DoWork += OnWork;
			// Support cancellation.
			Worker.WorkerSupportsCancellation = true;
			// Begin the loop.
			Worker.RunWorkerAsync();
		}

		/// <summary>
		/// Show the process selector.
		/// </summary>
		private bool ShowProcessSelector(Process[] procs)
		{
			// Create a new process selector window.
			var ps = new ProcessSelector();
			// Get the view model.
			var vm = ps.DataContext as ProcessSelectorViewModel;
			// Set the settings.
			vm.UserSettings = UserSettings;
			// Set the process list.
			vm.ProcessList = new System.Collections.ObjectModel.ObservableCollection<Process>(procs);

			// Show the dialog and if result comes back false we canceled the window.
			if (ps.ShowDialog() == false || vm.SelectedProcess == null)
			{
				logger.Info("User didn't select a process.");
				Application.Current.Shutdown();

				// Failed to select process.
				return false;
			}

			// Set the selected process.
			mem.theProc = vm.SelectedProcess;

			// We did it.
			return true;
		}

		/// <summary>
		/// Worker loop for reading memory.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnWork(object sender, DoWorkEventArgs e)
		{
			// pointers for waymark positions
			var wayA = WaymarkAddr + 0x00;
			var wayB = WaymarkAddr + 0x20;
			var wayC = WaymarkAddr + 0x40;
			var wayD = WaymarkAddr + 0x60;
			var wayOne = WaymarkAddr + 0x80;
			var wayTwo = WaymarkAddr + 0xA0;
			var wayThree = WaymarkAddr + 0xC0;
			var wayFour = WaymarkAddr + 0xE0;

			// Worker loop runs indefinitely.
			while (true)
			{
				// Supporting cancellation.
				if (Worker.CancellationPending)
					e.Cancel = true;

				// ReadWaymark local function to read multiple waymarks with.
				Waymark ReadWaymark(long addr, WaymarkID id) => new Waymark
				{
					X = mem.ReadFloat(addr.ToString("X")),
					Y = mem.ReadFloat((addr + 0x4).ToString("X")),
					Z = mem.ReadFloat((addr + 0x8).ToString("X")),
					Active = mem.ReadByte((addr + 0x1C).ToString("X")) == 1,
					ID = id
				};

				try
				{
					// Read waymarks in with our function.
					GameMemory.A = ReadWaymark(wayA, WaymarkID.A);
					GameMemory.B = ReadWaymark(wayB, WaymarkID.B);
					GameMemory.C = ReadWaymark(wayC, WaymarkID.C);
					GameMemory.D = ReadWaymark(wayD, WaymarkID.D);
					GameMemory.One = ReadWaymark(wayOne, WaymarkID.One);
					GameMemory.Two = ReadWaymark(wayTwo, WaymarkID.Two);
					GameMemory.Three = ReadWaymark(wayThree, WaymarkID.Three);
					GameMemory.Four = ReadWaymark(wayFour, WaymarkID.Four);

					// Publish our event on the EventAggregator.
					EventAggregator.GetEvent<GameMemoryUpdateEvent>().Publish(GameMemory);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Exception while reading game memory.");
				}

				// Sleep before next loop.
				Thread.Sleep(100);
			}
		}

		/// <summary>
		/// Write a waymark in memory and place it.
		/// </summary>
		/// <param name="waymark">Waymark to place.</param>
		private void WriteWaymark(Waymark waymark, int id = -1)
		{
			// Ensure the waymark isn't null.
			if (waymark == null)
				return;

			var wID = id == -1 ? (byte)waymark.ID : id;

			// pointers for waymark positions
			var wayA = WaymarkAddr + 0x00;
			var wayB = WaymarkAddr + 0x20;
			var wayC = WaymarkAddr + 0x40;
			var wayD = WaymarkAddr + 0x60;
			var wayOne = WaymarkAddr + 0x80;
			var wayTwo = WaymarkAddr + 0xA0;
			var wayThree = WaymarkAddr + 0xC0;
			var wayFour = WaymarkAddr + 0xE0;

			if (UserSettings.LocalOnly)
			{
				long markAddr = 0;
				if (wID == (int)WaymarkID.A)
					markAddr = wayA;
				else if (wID == (int)WaymarkID.B)
					markAddr = wayB;
				else if (wID == (int)WaymarkID.C)
					markAddr = wayC;
				else if (wID == (int)WaymarkID.D)
					markAddr = wayD;
				else if (wID == (int)WaymarkID.One)
					markAddr = wayOne;
				else if (wID == (int)WaymarkID.Two)
					markAddr = wayTwo;
				else if (wID == (int)WaymarkID.Three)
					markAddr = wayThree;
				else if (wID == (int)WaymarkID.Four)
					markAddr = wayFour;

				// Write the X, Y and Z coordinates
				mem.WriteBytes((UIntPtr)markAddr, BitConverter.GetBytes(waymark.X));
				mem.WriteBytes((UIntPtr)markAddr + 0x4, BitConverter.GetBytes(waymark.Y));
				mem.WriteBytes((UIntPtr)markAddr + 0x8, BitConverter.GetBytes(waymark.Z));

				mem.WriteBytes((UIntPtr)markAddr + 0x10, BitConverter.GetBytes((int)(waymark.X * 1000)));
				mem.WriteBytes((UIntPtr)markAddr + 0x14, BitConverter.GetBytes((int)(waymark.Y * 1000)));
				mem.WriteBytes((UIntPtr)markAddr + 0x18, BitConverter.GetBytes((int)(waymark.Z * 1000)));

				// Write the active state
				mem.WriteBytes((UIntPtr)markAddr + 0x1C, new byte[] { (byte)(waymark.Active ? 1 : 0) });

				// Return out of this function
				return;
			}
		}

		/// <summary>
		/// Loads the preset using our injected function.
		/// </summary>
		private void LoadPreset()
		{
			if (!UserSettings.LocalOnly)
			{
				MessageBox.Show("This version of Paisley Park only supports Local Only mode.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

			// Ensure we have a preset selected.
			if (CurrentPreset == null)
				return;

			// Calls the waymark function for all our waymarks.
			try
			{
				if (WaymarkThread != null && WaymarkThread.IsAlive)
				{
					MessageBox.Show("Please wait for the previous Load to finish.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				WaymarkThread = new Thread(() =>
				{
					WriteWaymark(CurrentPreset.A, 0);
					WriteWaymark(CurrentPreset.B, 1);
					WriteWaymark(CurrentPreset.C, 2);
					WriteWaymark(CurrentPreset.D, 3);
					WriteWaymark(CurrentPreset.One, 4);
					WriteWaymark(CurrentPreset.Two, 5);
					WriteWaymark(CurrentPreset.Three, 6);
					WriteWaymark(CurrentPreset.Four, 7);
				});

				WaymarkThread.Start();
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					"Something happened while attemping to load your preset!",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				logger.Error(ex, "An error occured while trying to call remote thread or writing waymarks into memory.");
				OnClose();
				Application.Current.Shutdown();
			}
		}

		/// <summary>
		/// Starts the HTTP server.
		/// <param name="alert">Alerts that the server started.</param>
		/// </summary>
		private void OnStartServer()
		{
			// Ignore if server is already started.
			if (IsServerStarted)
				return;

			// Initialize the host.
			Host = new NancyHost(new PaisleyParkBootstrapper(), new Uri($"http://localhost:{UserSettings.Port.ToString()}"));

			// Start the Nancy Host.
			try
			{
				Host.Start();
				IsServerStarted = true;
				StartServerCommand.RaiseCanExecuteChanged();
				StopServerCommand.RaiseCanExecuteChanged();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Could not start Nancy host.");
				MessageBox.Show($"Could not start the HTTP server on port {UserSettings.Port}!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// When the HTTP server stops.
		/// </summary>
		private void OnStopServer()
		{
			try
			{
				Host.Stop();
				IsServerStarted = false;
				StartServerCommand.RaiseCanExecuteChanged();
				StopServerCommand.RaiseCanExecuteChanged();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error stopping server.");
				MessageBox.Show("There was an error stopping the server.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// Click to manage the presets.
		/// </summary>
		private void OnManagePresets()
		{
			// Create new preset manager window.
			var win = new PresetManager
			{
				// Set owner to main window.
				Owner = Application.Current.MainWindow
			};

			// Pull view model from window.
			var vm = win.DataContext as PresetManagerViewModel;

			// Populate the presets with our current presets as a new instance.
			vm.Presets = new System.Collections.ObjectModel.ObservableCollection<Preset>(UserSettings.Presets);

			// Check if we're saving changes.
			if (win.ShowDialog() == true)
			{
				// Reassign presets in user settings to the ones managed by the window.
				UserSettings.Presets = vm.Presets;
				// Save the settings.
				Settings.Save(UserSettings);
			}
		}

		/// <summary>
		/// When the window is being closed.
		/// </summary>
		private void OnClose()
		{
			// Save the settings.
			Settings.Save(UserSettings);

			NLog.LogManager.Shutdown();
		}
	}
}
