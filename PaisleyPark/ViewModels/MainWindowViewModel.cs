using AutoUpdaterDotNET;
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
        public Memory GameMemory { get; set; } = new Memory();
        public Settings UserSettings { get; set; }
        public Preset CurrentPreset { get; set; }
        public string WindowTitle { get; set; }
        public bool IsServerStarted { get; set; }
        public bool IsServerStopped { get => !IsServerStarted; }
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private NancyHost Host;
        private Thread WaymarkThread;
        private readonly Offsets Offsets;
        private readonly Version CurrentVersion;

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

            // Read the offsets.json file.
            using (var r = new StreamReader("Offsets.json"))
            {
                Offsets = JsonConvert.DeserializeObject<Offsets>(r.ReadToEnd());
            }

            // Load the settings file.
            UserSettings = Settings.Load();

            // Subscribe to the waymark event from the REST server.
            EventAggregator.GetEvent<WaymarkEvent>().Subscribe(waymarks =>
            {
                WriteWaymark(waymarks.A);
                WriteWaymark(waymarks.B);
                WriteWaymark(waymarks.C);
                WriteWaymark(waymarks.D);
                WriteWaymark(waymarks.One);
                WriteWaymark(waymarks.Two);
            });

            // Create the commands.
            LoadPresetCommand = new DelegateCommand(LoadPreset);
            ClosingCommand = new DelegateCommand(OnClose);
            ManagePresetsCommand = new DelegateCommand(OnManagePresets);
            StartServerCommand = new DelegateCommand(OnStartServer).ObservesCanExecute(() => IsServerStopped);
            StopServerCommand = new DelegateCommand(OnStopServer).ObservesCanExecute(() => IsServerStarted);
            DiscordCommand = new DelegateCommand(() => { Process.Start("https://discord.gg/hq3DnBa"); });

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
        /// Fetch an update for the applicaton.
        /// </summary>
        private void FetchUpdate()
        {
            logger.Info("Fetching update.");

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
                        var json = JsonConvert.DeserializeObject<JObject>(r.ReadToEnd());
                        // Get tag name and remove the v in front.
                        var tag_name = json["tag_name"].Value<string>().Substring(1);
                        // Form release version from this string.
                        var releaseVersion = new Version(tag_name);
                        // Check if the release is newer.
                        if (releaseVersion > CurrentVersion)
                        {
                            // Create update window.
                            var updateWindow = new Updater();
                            // Get the view model.
                            var vm = updateWindow.DataContext as UpdaterViewModel;
                            // Create HTML out of the markdown in body.
                            var html = Markdown.ToHtml(json["body"].Value<string>());
                            // Set the update string
                            vm.UpdateString = $"Paisley Park {releaseVersion.VersionString()} is now available, you have {CurrentVersion.VersionString()}. Would you like to download it now?";
                            // Set HTML in the window.
                            vm.HTML = html;

                            // We want to install.
                            if (updateWindow.ShowDialog() == true)
                            {
                                using (var wc = new WebClient())
                                {
                                    // Delete existing zip file.
                                    if (File.Exists("PaisleyPark.zip"))
                                        File.Delete("PaisleyPark.zip");

                                    // Download the file. 
                                    wc.DownloadFile(new Uri(json["assets"][0]["browser_download_url"].Value<string>()), "PaisleyPark.zip");

                                    // Get temp path for update script to run on.
                                    var temp = Path.Combine(Path.GetTempPath(), "PaisleyPark");

                                    // Create temp diretory if it doesn't exist.
                                    if (!Directory.Exists(temp))
                                        Directory.CreateDirectory(temp);

                                    // Temp update file location.
                                    var script = Path.Combine(temp, "update.ps1");

                                    // Delete existing script just in case.
                                    if (File.Exists(script))
                                        File.Delete(script);

                                    // Copy the update script to the temp path.
                                    File.Copy(@".\update.ps1", script);

                                    // Run the update script.
                                    Process.Start("powershell.exe", $"{script} \"{Environment.CurrentDirectory}\"");
                                }                                
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateFailed(ex);
                }
            }
            else
            {
                UpdateFailed();
            }

            // Used for when update fails.
            void UpdateFailed(Exception ex = null)
            {
                var answer = MessageBox.Show(
                    "Unable to fetch the latest release of Paisley Park.  Would you like to visit the release page?",
                    "Paisley Park",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error
                );
                if (answer == MessageBoxResult.Yes)
                {
                    Process.Start("https://github.com/LeonBlade/PaisleyPark/releases/latest");
                }

                logger.Error(ex, "Update failed when requesting update from Github.");
            }
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

            // Load in the definitions file.
            GameDefinitions = new Definitions(GameProcess);

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

                logger.Debug("Assembly to inject:\n{0}", asm);

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
                    GameMemory.MapID = GameProcess.ReadUInt32(GameDefinitions.MapID);

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
        private void WriteWaymark(Waymark waymark)
        {
            // Ensure the waymark isn't null.
            if (waymark == null)
                return;

            // Write the X, Y and Z coordinates.
            GameProcess.Write(_newmem, waymark.X);
            GameProcess.Write(_newmem + 0x4, waymark.Y);
            GameProcess.Write(_newmem + 0x8, waymark.Z);

            // Write the waymark ID.
            GameProcess.Write(_newmem + 0xC, (byte)waymark.ID);

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
                    "It appears you might not be loaded into a zone yet. Placing Waymarks in this state will crash the game. Are you sure you want to do this?",
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
                    WriteWaymark(CurrentPreset.A);
                    WriteWaymark(CurrentPreset.B);
                    WriteWaymark(CurrentPreset.C);
                    WriteWaymark(CurrentPreset.D);
                    WriteWaymark(CurrentPreset.One);
                    WriteWaymark(CurrentPreset.Two);
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

            NLog.LogManager.Shutdown();
        }
    }
}
