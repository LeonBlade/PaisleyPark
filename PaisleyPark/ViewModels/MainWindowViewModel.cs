using Markdig;
using Nancy.Hosting.Self;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nhaama.FFXIV;
using Nhaama.Memory;
using Nhaama.Memory.Native;
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PaisleyPark.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public static IEventAggregator EventAggregator { get; private set; }
        private NhaamaProcess GameProcess { get; set; }
        private Definitions GameDefinitions { get; set; }
        private BackgroundWorker Worker;
        public static Memory GameMemory { get; set; } = new Memory();
        public Settings UserSettings { get; set; }
        public Preset CurrentPreset { get; set; }
        public string WindowTitle { get; set; }
        public bool IsServerStarted { get; set; }
        public bool IsServerStopped { get => !IsServerStarted; }
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private NancyHost Host;
        private Thread WaymarkThread;
        private Offsets Offsets;
        private readonly Version CurrentVersion;
		private string GameVersion;
		public string DiscordUri { get; private set; } = "https://discord.gg/hq3DnBa";
		private static readonly Uri OffsetUrl = new Uri("https://raw.githubusercontent.com/LeonBlade/PaisleyPark/master/Offsets/");

#pragma warning disable IDE1006 // Naming Styles

		// Memory addresses for our injection.
		public ulong _newmem { get; private set; }
        public ulong _inject { get; private set; }

#pragma warning restore IDE1006 // Naming Styles

        public DelegateCommand ManagePreset { get; private set; }
        public DelegateCommand LoadPresetCommand { get; private set; }
        public DelegateCommand ManagePresetsCommand { get; private set; }
        public DelegateCommand ClosingCommand { get; private set; }
        public DelegateCommand StartServerCommand { get; private set; }
        public DelegateCommand StopServerCommand { get; private set; }
        public DelegateCommand DiscordCommand { get; private set; }

		public MainWindowViewModel(IEventAggregator ea)
        {
            // Store reference to the event aggregator.
            EventAggregator = ea;

            logger.Info("--- PAISLEY PARK START ---");
            logger.Info("Fetching update.");

            // Get the version from the assembly.
            CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            // Set window title.
            WindowTitle = string.Format("Paisley Park {0}", CurrentVersion.VersionString());

            // Fetch an update.
            FetchUpdate();

			// Load the settings file.
			UserSettings = Settings.Load();

			// Get the offsets.
			GetOffsets();

            // Subscribe to the waymark event from the REST server.
            EventAggregator.GetEvent<WaymarkEvent>().Subscribe(waymarks =>
            {
                WriteWaymark(waymarks.A, 0);
                WriteWaymark(waymarks.B, 1);
                WriteWaymark(waymarks.C, 2);
                WriteWaymark(waymarks.D, 3);
                WriteWaymark(waymarks.One, 4);
                WriteWaymark(waymarks.Two, 5);
            });

            // Create the commands.
            LoadPresetCommand = new DelegateCommand(LoadPreset);
            ClosingCommand = new DelegateCommand(OnClose);
            ManagePresetsCommand = new DelegateCommand(OnManagePresets);
            StartServerCommand = new DelegateCommand(OnStartServer).ObservesCanExecute(() => IsServerStopped);
            StopServerCommand = new DelegateCommand(OnStopServer).ObservesCanExecute(() => IsServerStarted);
			DiscordCommand = new DelegateCommand(OnDiscord);

            // Listen for property changed.
            UserSettings.PropertyChanged += OnPropertyChanged;

            // Prepare for new game launch.
            if (!Initialize())
                return;
        }

        /// <summary>
        /// Starts everything needed for this process.
        /// </summary>
        /// <returns>Successful initialization.</returns>
        private bool Initialize()
        {
            // Initialize Nhaama.
            if (!InitializeNhaama())
                return false;

            // Inject our code.
            InjectCode();

            // Check autostart and start the HTTP server if it's true.
            if (UserSettings.HTTPAutoStart)
                OnStartServer();

            return true;
        }

		/// <summary>
		/// Gets the offsets for the program, also checks for a new version for this game version.
		/// </summary>
		private void GetOffsets()
		{
			// Get the current version of FFXIV.
			var gameDirectory = new DirectoryInfo(GameProcess.BaseProcess.MainModule.FileName);
			GameVersion = File.ReadAllText(Path.Combine(gameDirectory.Parent.FullName, "ffxivgame.ver"));

			logger.Debug($"Game version is {GameVersion}");

			// Check the game version against what we have saved in settings.
			if (UserSettings.LatestGameVersion != GameVersion)
			{
				logger.Info($"Latest version {GameVersion} does not match the latest game version in settings {UserSettings.LatestGameVersion}");

				var result = MessageBox.Show("There are new offsets available from the web. Would you like to use these offsets?", "Paisley Park", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (result == MessageBoxResult.Yes)
				{
					logger.Info("User is downloading latest offsets.");
					// Create client to fetch latest version of offsets.
					try
					{
						using (var client = new WebClient())
						{
							// Form the URI for the game version's offsets file.
							var uri = new Uri(OffsetUrl, $"{GameVersion}.json");
							// Write the JSON to the disk overwriting the Offsets.json file used locally.
							File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "Offsets.json"), client.DownloadString(uri));
							// Set the lateste version to the version downloaded.
							UserSettings.LatestGameVersion = GameVersion;
							// Save the settings.
							Settings.Save(UserSettings);
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show("Couldn't fetch or save offsets from the server. Your offsets could be out of date, and if so, may cause the game to crash.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
						logger.Error(ex, "Couldn't fetch or save offsets from the server!");
					}
				}
			}

			// Read the offsets.json file.
			try
			{
				using (var r = new StreamReader(Path.Combine(Environment.CurrentDirectory, "Offsets.json")))
				{
					Offsets = JsonConvert.DeserializeObject<Offsets>(r.ReadToEnd());
				}
			}
			catch (Exception)
			{
				MessageBox.Show("Couldn't load the offsets file!  Please select the offsets file manually.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				var dlg = new Microsoft.Win32.OpenFileDialog
				{
					InitialDirectory = Environment.CurrentDirectory,
					DefaultExt = ".json",
					Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*"
				};

				// Show dialog.
				var result = dlg.ShowDialog();

				if (result == true)
				{
					try
					{
						using (var r = new StreamReader(dlg.FileName))
						{
							Offsets = JsonConvert.DeserializeObject<Offsets>(r.ReadToEnd());
						}
					}
					catch (Exception)
					{
						MessageBox.Show("Could not open this offset file. Shutting down.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
						Application.Current.Shutdown();
					}
				}
			}
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
                // Save the settings file.
                Settings.Save(UserSettings);
            }
        }

        /// <summary>
        /// Initialize Nhaama for use in the application.
        /// </summary>
        private bool InitializeNhaama()
        {
            // Get the processes of XIV.
            var procs = Process.GetProcessesByName("ffxiv_dx11");

            // More than one process.
            if (procs.Length > 1 || procs.Length == 0)
            {
                // Show the process selector window.
                if (!ShowProcessSelector(procs))
                    return false;
            }
            else
                // Get the Nhaama process from the first process that matches for XIV.
                GameProcess = procs[0].GetNhaamaProcess();

            // Enable raising events.
            GameProcess.BaseProcess.EnableRaisingEvents = true;

            // Listen to some stuff.
            GameProcess.BaseProcess.Exited += (_, e) =>
            {
                MessageBox.Show(
                    "Looks like FINAL FANTASY XIV crashed or shut down.",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                logger.Info("FFXIV Shutdown or Crashed!");

                // Start the initialization process again.
                Application.Current.Dispatcher.Invoke(() => { Initialize(); });
            };

            // Get FFXIV game folder.
            var ffxiv_folder = Path.GetDirectoryName(GameProcess.BaseProcess.MainModule.FileName);
            // Read the version file.
            var gameVersion = File.ReadAllLines(Path.Combine(ffxiv_folder, "ffxivgame.ver"))[0];

            // Load in the definitions file.
            try
            {
                GameDefinitions = Definitions.Get(GameProcess, gameVersion.ToString(), Game.GameType.Dx11);
            }
            catch (Exception)
            {
				// Fallback to last known version.
                GameDefinitions = Definitions.Get(GameProcess, "2019.08.21.0000.0000", Game.GameType.Dx11);
            }

            // Create new worker.
            Worker = new BackgroundWorker();
            // Set worker loop.
            Worker.DoWork += OnWork;
            // Support cancellation.
            Worker.WorkerSupportsCancellation = true;
            // Begin the loop.
            Worker.RunWorkerAsync();

            // Success!
            return true;
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
            GameProcess = vm.SelectedProcess.GetNhaamaProcess();

            // We did it.
            return true;
        }

        /// <summary>
        /// Injects code into the game.
        /// </summary>
        private void InjectCode()
        {
            // Ensure process is valid.
            if (GameProcess == null)
            {
                MessageBox.Show(
                    "FINAL FANTASY XIV is not running or something bad happened!",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                logger.Error("FFXIV is not running during injection. This should never be seen!");
                Application.Current.Shutdown();
            }

            try
            {
                // Get xiv's base address.
                var ffxiv_dx11 = GameProcess.BaseProcess.MainModule.BaseAddress;
                // Waymark function address.
                // TODO: AoB!
                // 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 8B EA 49 8B F0 48 8B F9 83 FA 06
                var waymarkFunc = (ffxiv_dx11 + Offsets.WaymarkFunc).ToUint64();
				// Waymark class instance. (?)
				// 45 33 c0 8d 57 ff 48 8d 0d (lea rcx offset before call to function) 
				var waymarkClassPointer = (ffxiv_dx11 + Offsets.WaymarkClassPtr).ToUint64();

                logger.Debug("FFXIV Base Address: {0}", ffxiv_dx11.ToUint64().AsHex());
                logger.Debug("Waymark Function: {0}", waymarkFunc.AsHex());
                logger.Debug("Waymark Pointer: {0}", waymarkClassPointer.AsHex());

                // Allocate new memory for our function's data.
                _newmem = GameProcess.Alloc(14, ffxiv_dx11.ToUint64());

                logger.Info("_newmem: {0}", _newmem.AsHex());

                // Assembly instructions.
                string asm = string.Format(string.Join("\n", new string[]
                {
                    "sub rsp, 40",          // give room in stack
                    "xor rdx, rdx",			// zero out rdx and r8
                    "xor r8, r8",
                    "mov rax, {0}",			// memory allocated
                    "mov rbx, [rax+0xD]",	// active state
                    "mov dl, [rax+0xC]",	// waypoint ID
                    "test rbx, rbx",
                    "jz skip",
                    "lea r8, [rax]",		// waypoint x,y,z coordinates
                    "skip:",
                    "mov rax, {1}",			// waymark class pointer
                    "lea rcx, [rax]",
                    "mov rax, {2}",			// waymark function
                    "call rax",
                    "add rsp, 40",          // move stack pointer back
                    "ret"
                }), _newmem.AsHex(), waymarkClassPointer.AsHex(), waymarkFunc.AsHex());

                // Get bytes from AsmjitCSharp.
                var bytes = AsmjitCSharp.Assemble(asm);

                // log bytes as hex
                logger.Debug("Bytes: {0}", BitConverter.ToString(bytes).Replace("-", " "));

                // Allocate bytes for our code injection near waymark function.
                _inject = GameProcess.Alloc((uint)bytes.LongLength, waymarkFunc);

                logger.Info("_inject: {0}", _inject.AsHex());

                // Write our injection bytes into the process.
                GameProcess.WriteBytes(_inject, bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something happened while injecting into FINAL FANTASY XIV!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(
                    ex,
                    "Injection Failed! newmem: {0}, inject: {1}",
                    _newmem.AsHex(),
                    _inject.AsHex()
                );
                OnClose();
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Worker loop for reading memory.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWork(object sender, DoWorkEventArgs e)
        {
            // Initialize pointers and addresses to the memory we're going to read.
            var ffxiv = GameProcess.BaseProcess.MainModule.BaseAddress;

            var WaymarkAddr = new IntPtr();

            // pointers for waymark positions
            var wayA = (ffxiv + Offsets.Waymarks + 0x00).ToUint64();
            var wayB = (ffxiv + Offsets.Waymarks + 0x20).ToUint64();
            var wayC = (ffxiv + Offsets.Waymarks + 0x40).ToUint64();
            var wayD = (ffxiv + Offsets.Waymarks + 0x60).ToUint64();
            var wayOne = (ffxiv + Offsets.Waymarks + 0x80).ToUint64();
            var wayTwo = (ffxiv + Offsets.Waymarks + 0xA0).ToUint64();

            // Worker loop runs indefinitely.
            while (true)
            {
                // Pointers for player position, start of the actor table (first actor in the table is you)
                // NOTE: needs to be addressed in the loop because it changes dynamically.
                var playerPosition = new Pointer(GameProcess, GameDefinitions.ActorTable + 0x8, 0xF0, 0x50);

                // Supporting cancellation.
                if (Worker.CancellationPending)
                    e.Cancel = true;

                // ReadWaymark local function to read multiple waymarks with.
                Waymark ReadWaymark(ulong addr, WaymarkID id) => new Waymark
                {
                    X = GameProcess.ReadFloat(addr),
                    Y = GameProcess.ReadFloat(addr + 0x4),
                    Z = GameProcess.ReadFloat(addr + 0x8),
                    Active = GameProcess.ReadByte(addr + 0x10) == 1,
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

                    // Read the map ID.
                    GameMemory.MapID = GameProcess.ReadUInt32(new Pointer(GameProcess, 0x1AE6A88, 0x5C4));

                    // Read in player position.
                    GameMemory.PlayerX = GameProcess.ReadFloat(playerPosition);
                    GameMemory.PlayerY = GameProcess.ReadFloat(playerPosition + 0x4);
                    GameMemory.PlayerZ = GameProcess.ReadFloat(playerPosition + 0x8);

                    // Publish our event on the EventAggregator.
                    EventAggregator.GetEvent<GameMemoryUpdateEvent>().Publish(GameMemory);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception while reading game memory. Waymark Address: {0}", WaymarkAddr.ToString("X4"));
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

            // Write the X, Y and Z coordinates.
            GameProcess.Write(_newmem, waymark.X);
            GameProcess.Write(_newmem + 0x4, waymark.Y);
            GameProcess.Write(_newmem + 0x8, waymark.Z);

            // Write the waymark ID.
            GameProcess.Write(_newmem + 0xC, (byte)(id == -1?(byte)waymark.ID:id));

            // Write the enable state
            GameProcess.Write(_newmem + 0xD, (byte)(waymark.Active ? 1 : 0));

            // Create a thread to call our injected function.
            var threadHandle = GameProcess.CreateRemoteThread(new IntPtr((long)_inject), out _);

            // Ensure the delay is at least 10 ms.
            var delay = Math.Max(UserSettings.PlacementDelay, 10);

            // Wait a selected number of ms
            Task.Delay(delay).Wait();

            // Wait for the thread.
            Kernel32.WaitForSingleObject(threadHandle, unchecked((uint)-1));

            // Close the thread handle.
            Kernel32.CloseHandle(threadHandle);
        }

        /// <summary>
        /// Loads the preset using our injected function.
        /// </summary>
        private void LoadPreset()
        {
            // Ensure that our injection and newmem addresses are set.
            if (_inject == 0 || _newmem == 0)
            {
                MessageBox.Show(
                    "Code is not injected for placing waymarks!",
                    "Paisley Park",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                logger.Error("Injection somehow failed yet wasn't caught by an earlier error. You should not see this!");
                OnClose();
                Application.Current.Shutdown();
            }

            // Ensure we have a preset selected.
            if (CurrentPreset == null)
                return;

            // Check if user coordinates are all 0.  This likely means that we're still loading into the zone.
            if (GameMemory.PlayerX == 0 && GameMemory.PlayerY == 0 && GameMemory.PlayerZ == 0)
            {
                // Ask the user if they want to still place based on the XYZ being all 0.
                var result = MessageBox.Show(
                    "There is a problem loading your current position, this may cause crashing. Are you sure you want to do this?",
                    "Paisley Park",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                // If we didn't say yes then return.
                if (result != MessageBoxResult.Yes)
                    return;
            }

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
            // Deallocate memory before closing.
            if (_inject != 0)
                GameProcess.Dealloc(_inject);
            if (_newmem != 0)
                GameProcess.Dealloc(_newmem);

			// Save the settings.
			Settings.Save(UserSettings);

            NLog.LogManager.Shutdown();
        }
    }
}
