using AutoUpdaterDotNET;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class MainWindowViewModel : BindableBase
	{
		private readonly IEventAggregator _ea;
		private NhaamaProcess GameProcess { get; set; }
		private Definitions GameDefinitions { get; set; }
		private readonly BackgroundWorker Worker = new BackgroundWorker();

		public Memory GameMemory { get; set; } = new Memory();
		public Settings UserSettings { get; set; }
		//public ARealmReversed Realm { get; set; }
		public Preset CurrentPreset { get; set; }

#pragma warning disable IDE1006 // Naming Styles

		// Memory addresses for our injection.
		public ulong _newmem { get; private set; }
		public ulong _inject { get; private set; }

#pragma warning restore IDE1006 // Naming Styles

		public ICommand LoadPresetCommand { get; private set; }
		public ICommand ClosingCommand { get; private set; }
		public ICommand ManagePresetsCommand { get; private set; }

		private const int WaymarkAddr = 0x1AE5960;

		private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		public MainWindowViewModel(IEventAggregator ea)
		{
			// Store reference to the event aggregator.
			_ea = ea;

			logger.Info("--- PAISLEY PARK START ---");
			logger.Info("Fetching update.");

			// Fetching the update.
			AutoUpdater.RunUpdateAsAdmin = true;
			AutoUpdater.DownloadPath = Environment.CurrentDirectory;
			AutoUpdater.Start("https://raw.githubusercontent.com/LeonBlade/PaisleyPark/master/Update.xml");

			// Load the settings file.
			UserSettings = Settings.Load();

			// Initialize Nhaama.
			InitializeNhaama();

			// Initialize SaintCoinach.
			//InitializeSaintCoinach();

			// TEMP

			//GameProcess.Dealloc(0x7FF7997A0000);
			//GameProcess.Dealloc(0x7FF7997B0000);

			// TEMP

			// Inject our code.
			InjectCode();

			// Create the commands.
			LoadPresetCommand = new DelegateCommand(LoadPreset);
			ClosingCommand = new DelegateCommand(OnClose);
			ManagePresetsCommand = new DelegateCommand(OnManagePresets);
		}

		/// <summary>
		/// Initialize Nhaama for use in the application.
		/// </summary>
		private void InitializeNhaama()
		{
			// Get the processes of XIV.
			var procs = Process.GetProcessesByName("ffxiv_dx11");

			// If there wasn't any process found, can't continue.
			if (procs.Length == 0)
			{
				MessageBox.Show(
					"You're not running FFXIV! Cannot start until you open the game.",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Exclamation
				);
				logger.Error("FFXIV is not running!");
				NLog.LogManager.Shutdown();
				Environment.Exit(-1);
			}

			// Get the Nhaama process from the first process that matches for XIV.
			GameProcess = procs[0].GetNhaamaProcess();

			// Enable raising events.
			GameProcess.BaseProcess.EnableRaisingEvents = true;

			// Listen to some stuff.
			GameProcess.BaseProcess.Exited += (_, e) =>
			{
				MessageBox.Show(
					"Looks like FINAL FANTASY XIV is no more! Please restart Paisley Park after you start it up again.",
					"Paisley Park",
					MessageBoxButton.OK,
					MessageBoxImage.Exclamation
				);
				logger.Info("FFXIV Shutdown or Crashed!");
			};

			// Load in the definitions file.
			GameDefinitions = new Definitions(GameProcess);

			// Set worker loop.
			Worker.DoWork += OnWork;
			// Support cancellation.
			Worker.WorkerSupportsCancellation = true;
			// Begin the loop.
			Worker.RunWorkerAsync();
		}

		/// <summary>
		/// Initialize Saint Coiniach for use in the application.
		/// </summary>
		/*private void InitializeSaintCoinach()
		{
			// Initialize the Realm based on our game path settings.
			Realm = new ARealmReversed(UserSettings.GamePath, SaintCoinach.Ex.Language.English);

			// Check if the game is updated, if not then update the game.
			if (!Realm.IsCurrentVersion)
			{
				try
				{
					// Create new window for the Progress Update.
					var win = new UpdateProgress();
					// Pull out the ViewModel from the DataContext.
					var vm = win.DataContext as UpdateProgressViewModel;

					// Call the Realm's update function.
					vm.UpdateRealm(Realm);

					// Show the new window.
					win.ShowDialog();
				}
				catch (Exception)
				{
					MessageBox.Show("Could not update SaintCoinach!  Map information may not be up to date.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}*/

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
				Environment.Exit(-1);
			}

			try
			{
				// Get xiv's base address.
				var ffxiv_dx11 = GameProcess.BaseProcess.MainModule.BaseAddress;
				// Get the sleep module base address.
				var sleep = Kernel32.GetProcAddress(Kernel32.GetModuleHandle("KERNEL32.DLL"), "Sleep").ToUInt64();
				// Waymark function address.
				// TODO: AoB!
				// 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 8B EA 49 8B F0 48 8B F9 83 FA 06
				var waymarkFunc = (ffxiv_dx11 + 0x752720).ToUint64();
				// Waymark class instance. (?)
				var waymarkClassPointer = (ffxiv_dx11 + 0x1AE57C0).ToUint64();

				logger.Debug("FFXIV Base Address: {0}", ffxiv_dx11.ToUint64().AsHex());
				logger.Debug("Sleep: {0}", sleep.AsHex());
				logger.Debug("Waymark Function: {0}", waymarkFunc.AsHex());
				logger.Debug("Waymark Pointer: {0}", waymarkClassPointer.AsHex());

				// Allocate new memory for our function's data.
				_newmem = GameProcess.Alloc(14, ffxiv_dx11.ToUint64());

				logger.Info("_newmem: {0}", _newmem.AsHex());

				// Assembly instructions.
				string asm = string.Format(string.Join("\n", new string[]
				{
					"xor rdx, rdx",			// zero out rdx and r8
					"xor r8, r8",
					"mov rax, {0}",			// memory allocated
					"mov rbx, [rax+0xD]",	// active state
					"mov dl, [rax+0xC]",	// waypoint ID
					"test rbx, rbx",
					"jz skip",
					"lea r8, [rax]",		// waypoint x,y,z coordinates
				"skip:",
					"mov rax, {1}",			// waymark class pointer (ffxiv_dx11.exe+1AE57C0)
					"lea rcx, [rax]",
					"mov rax, {2}",			// waymark function (ffxiv_dx11.exe+752360)
					"call rax",
					"push 10",				// 10 ms
					"mov rax, {3}",			// sleep function
					"call rax",
					"ret"
				}), _newmem.AsHex(), waymarkClassPointer.AsHex(), waymarkFunc.AsHex(), sleep.AsHex());

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
				Environment.Exit(-1);
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

			// pointers for waymark positions
			var wayA = (ffxiv + WaymarkAddr + 0x00).ToUint64();
			var wayB = (ffxiv + WaymarkAddr + 0x20).ToUint64();
			var wayC = (ffxiv + WaymarkAddr + 0x40).ToUint64();
			var wayD = (ffxiv + WaymarkAddr + 0x60).ToUint64();
			var wayOne = (ffxiv + WaymarkAddr + 0x80).ToUint64();
			var wayTwo = (ffxiv + WaymarkAddr + 0xA0).ToUint64();

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
					_ea.GetEvent<GameMemoryUpdateEvent>().Publish(GameMemory);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Exception while reading game memory. Waymark Address: {0}", WaymarkAddr.ToString("X4"));
				}

				// Sleep for 100ms before next loop.
				Thread.Sleep(10);
			}
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
				Environment.Exit(-1);
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

			// Create local function to write to the newmem address.
			void WriteWaymark(Waymark waymark)
			{
				// Write the X, Y and Z coordinates.
				GameProcess.Write(_newmem, waymark.X);
				GameProcess.Write(_newmem + 0x4, waymark.Y);
				GameProcess.Write(_newmem + 0x8, waymark.Z);

				// Write the waymark ID.
				GameProcess.Write(_newmem + 0xC, (byte)waymark.ID);

				// Write the enable state
				GameProcess.Write(_newmem + 0xD, (byte)(waymark.Active ? 1 : 0));

				// Create a thread to call our injected function.
				GameProcess.CreateRemoteThread(new IntPtr((long)_inject));

				// Wait 10 ms
				Task.Delay(10).Wait();
			}

			// Calls the waymark function for all our waymarks.
			try
			{
				WriteWaymark(CurrentPreset.A);
				WriteWaymark(CurrentPreset.B);
				WriteWaymark(CurrentPreset.C);
				WriteWaymark(CurrentPreset.D);
				WriteWaymark(CurrentPreset.One);
				WriteWaymark(CurrentPreset.Two);
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
				Environment.Exit(-1);
			}
		}

		/// <summary>
		/// Click to manage the presets.
		/// </summary>
		private void OnManagePresets()
		{
			// Create new preset manager window.
			var win = new PresetManager();

			// Pull view model from window.
			var vm = win.DataContext as PresetManagerViewModel;

			// Populate the presets with our current presets.
			vm.Presets = UserSettings.Presets;

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
