using Nhaama.FFXIV;
using Nhaama.Memory;
using PaisleyPark.Common;
using PaisleyPark.Models;
using PaisleyPark.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using SaintCoinach;
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
		public ARealmReversed Realm { get; set; }
		public Preset CurrentPreset { get; set; }

		// Memory addresses for our injection.
		public ulong _newmem { get; private set; }
		public ulong _inject { get; private set; }

		public ICommand LoadPresetCommand { get; private set; }
		public ICommand ClosingCommand { get; private set; }
		public ICommand ManagePresetsCommand { get; private set; }

		public MainWindowViewModel(IEventAggregator ea)
		{
			// Store reference to the event aggregator.
			_ea = ea;

			// Load the settings file.
			UserSettings = Settings.Load();

			// Initialize Nhaama.
			InitializeNhaama();

			// Initialize SaintCoinach.
			InitializeSaintCoinach();

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
				MessageBox.Show("You're not running FFXIV!  Cannot start until you open the game.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				Environment.Exit(-1);
			}

			// Get the Nhaama process from the first process that matches for XIV.
			GameProcess = procs[0].GetNhaamaProcess();

			// Enable raising events.
			GameProcess.BaseProcess.EnableRaisingEvents = true;
			// Listen to some stuff.
			GameProcess.BaseProcess.Exited += (_, e) => MessageBox.Show("Looks like FINAL FANTASY XIV is no more!  Please restart Paisley Park after you start it up again.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			GameProcess.BaseProcess.OutputDataReceived += (_, e) => Console.WriteLine(e.Data);

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
		private void InitializeSaintCoinach()
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
		}

		/// <summary>
		/// Injects code into the game.
		/// </summary>
		private void InjectCode()
		{
			// Ensure process is valid.
			if (GameProcess == null)
			{
				MessageBox.Show("FINAL FANTASY XIV is not running or something bad happened!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(-1);
			}

			try
			{
				// Get xiv's base address.
				var ffxiv_dx11 = GameProcess.BaseProcess.MainModule.BaseAddress;
				// Get the sleep module base address.
				var sleep = GameProcess.GetModuleBasedOffset("KERNEL32.DLL", 0x14A30);
				// Waymark function address.
				// 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 8B EA 49 8B F0 48 8B F9 83 FA 06
				var waymarkFunc = (ffxiv_dx11 + 0x7525D0).ToUint64();
				// Waymark class instance. (?)
				var waymarkClassPointer = (ffxiv_dx11 + 0x1AE57C0).ToUint64();

				// Allocate new memory for our function's data.
				_newmem = GameProcess.Alloc(14, ffxiv_dx11.ToUint64());

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
					"push 10",
					"mov rax, {3}",
					"call rax",
					"ret"
				}), _newmem.AsHex(), waymarkClassPointer.AsHex(), waymarkFunc.AsHex(), sleep.AsHex());

				// Get bytes from AsmjitCSharp.
				var bytes = AsmjitCSharp.Assemble(asm);

				// Allocate bytes for our code injection near waymark function.
				_inject = GameProcess.Alloc((uint)bytes.LongLength, waymarkFunc);

				// Write the injections to log in case of failure.
				Console.WriteLine("{0} {1}", _inject.AsHex(), _newmem.AsHex());

				// Write our injection bytes into the process.
				GameProcess.WriteBytes(_inject, bytes);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Something happened while injecting into FINAL FANTASY XIV!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Console.WriteLine(ex.Message);

				// Try to deallocate just in case before quitting.
				GameProcess.Dealloc(_inject);
				GameProcess.Dealloc(_newmem);

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
			var wayA = (ffxiv + 0x1AE5960 + 0x00).ToUint64();
			var wayB = (ffxiv + 0x1AE5960 + 0x20).ToUint64();
			var wayC = (ffxiv + 0x1AE5960 + 0x40).ToUint64();
			var wayD = (ffxiv + 0x1AE5960 + 0x60).ToUint64();
			var wayOne = (ffxiv + 0x1AE5960 + 0x80).ToUint64();
			var wayTwo = (ffxiv + 0x1AE5960 + 0xA0).ToUint64();

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
					Console.WriteLine(ex.Message);
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
				MessageBox.Show("Code is not injected for placing waymarks!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(-1);
			}

			// Ensure we have a preset selected.
			if (CurrentPreset == null)
				return;

			// Check if user coordinates are all 0.  This likely means that we're still loading into the zone.
			if (GameMemory.PlayerX == 0 && GameMemory.PlayerY == 0 && GameMemory.PlayerZ == 0)
			{
				// Ask the user if they want to still place based on the XYZ being all 0.
				var result = MessageBox.Show("It appears you haven't loaded into the zone yet.  Placing Waymarks in this state will crash the game.  Are you sure you want to do this?", "Paisley Park", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
			WriteWaymark(CurrentPreset.A);
			WriteWaymark(CurrentPreset.B);
			WriteWaymark(CurrentPreset.C);
			WriteWaymark(CurrentPreset.D);
			WriteWaymark(CurrentPreset.One);
			WriteWaymark(CurrentPreset.Two);
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
		}
	}
}
