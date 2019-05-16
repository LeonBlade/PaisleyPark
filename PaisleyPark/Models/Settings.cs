using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace PaisleyPark.Models
{
	/// <summary>
	/// Settings model used for saving the settings to a file.
	/// </summary>
    public class Settings : INotifyPropertyChanged
    {
		/// <summary>
		/// Folder path to where the settings are stored.
		/// </summary>
		private static readonly string SETTINGS_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaisleyPark");

		/// <summary>
		/// File name for the settings.
		/// </summary>
		private static readonly string SETTINGS_FILE = "settings.json";

		/// <summary>
		/// Path to the game to use for various functions.
		/// </summary>
		public string GamePath { get; set; }

        /// <summary>
        /// Port for HTTP server.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Autostarts the HTTP server on launch.
        /// </summary>
        public bool HTTPAutoStart { get; set; } = false;

        /// <summary>
        /// How long to wait between placing waymarks.
        /// </summary>
        public int PlacementDelay { get; set; } = 200;

		/// <summary>
		/// List of presets to be saved to the file.
		/// </summary>
		public ObservableCollection<Preset> Presets { get; set; } = new ObservableCollection<Preset>();

		/// <summary>
		/// Saves the settings to the file.
		/// </summary>
		/// <param name="settings">Settings to save to the user file.</param>
		public static void Save(Settings settings)
		{
			// Does the settings folder exist?  If not, create Settings folder.
			if (!Directory.Exists(SETTINGS_FOLDER))
				Directory.CreateDirectory(SETTINGS_FOLDER);

			// Get the full path to the settings file.
			var fullPath = Path.Combine(SETTINGS_FOLDER, SETTINGS_FILE);

			// Create StreamWriter instance to save file contents into full path.
			using (var text = File.CreateText(fullPath))
			{
				// Save the contents of the file.
				text.Write(JsonConvert.SerializeObject(settings));
			}
		}

		/// <summary>
		/// Load the settings from a file.
		/// </summary>
		/// <returns>Returns instance of <see cref="Settings"/> with contents of file if file was found, creates a new instance if not.</returns>
		public static Settings Load()
		{
			// Create the return value.
			Settings settings = new Settings();

			// Does the settings folder exist?  If not, create Settings folder.
			if (!Directory.Exists(SETTINGS_FOLDER))
				Directory.CreateDirectory(SETTINGS_FOLDER);

			// Get the full path to the settings file.
			var fullPath = Path.Combine(SETTINGS_FOLDER, SETTINGS_FILE);

			// Does the settings file exist?  If so, load the file into the settings object.
			if (File.Exists(fullPath))
				settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(fullPath));

			// Return our settings.
			return settings;
		}

		/// <summary>
		/// Add a new preset to the settings preset list.
		/// </summary>
		/// <param name="preset">Preset object to add.</param>
		/// <param name="save">If you want to save the file afterwards.</param>
		public void AddPreset(Preset preset, bool save = true)
		{
			// Add the preset to the list.
			Presets.Add(preset);

			// Save the file if save is true
			if (save)
				Save(this);
		}

		/// <summary>
		/// Property Changed event handler for this model.
		/// </summary>
#pragma warning disable 67
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
	}
}
