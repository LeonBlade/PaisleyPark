using PaisleyPark.Common;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class UpdateProgressViewModel : BindableBase
    {
		// ProgressUpdater from SaintCoinach.
		//private readonly ProgressUpdate Update = new ProgressUpdate();
		/*
		// Bindings for the View.
		public string CurrentStep { get; set; }
		public string CurrentFile { get; set; }
		public ObservableCollection<string> Log { get; set; } = new ObservableCollection<string>();
		public double Percentage { get; set; }
		public bool IsClose { get; set; }
		
		// Commands.
		public ICommand ClosingCommand { get; private set; }

		public UpdateProgressViewModel()
		{
			// Set the event listener on our updater.
			Update.UpdateEvent += OnUpdate;

			// Create the closing command.
			ClosingCommand = new DelegateCommand<CancelEventArgs>(OnClosing);
		}

		/// <summary>
		/// Updates realm on its own thread and updates the UI.
		/// </summary>
		/// <param name="realm"></param>
		/public void UpdateRealm(SaintCoinach.ARealmReversed realm)
		{
			// Create a new thread to not block the UI thread.
			new Thread(new ParameterizedThreadStart(vm =>
			{
				// Call the update function on realm.
				realm.Update(false, (vm as UpdateProgressViewModel).Update);
			}))
			// Pass in this VM to access it's ProgressUpdate instance.
			.Start(this);
		}

		/// <summary>
		/// Update event fired from the ProgressUpdate class for SaintCoinach's update call.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnUpdate(object sender, SaintCoinach.Ex.Relational.Update.UpdateProgress e)
		{
			// Dispatch so we're on the UI thread.
			Application.Current.Dispatcher.Invoke(() =>
			{
				// Test console log.
				Console.WriteLine(e);
				// Set the CurrentStep value to the current/total.
				CurrentStep = e.CurrentStep + "/" + e.TotalSteps;
				// Set the progress bar percentage.
				Percentage = e.Percentage;
				// Add to the log the current operation.
				Log.Add(e.CurrentOperation + " > " + e.CurrentFile);
				// Set the current file.
				CurrentFile = e.CurrentFile;

				// Test if we are done and close the window.
				if (Percentage == 1)
					IsClose = true;
			});
		}

		/// <summary>
		/// When the view for this VM closes.
		/// </summary>
		/// <param name="e"></param>
		private void OnClosing(CancelEventArgs e)
		{
			// Prevents the window from being closed if we're not complete.
			if (Percentage != 1)
				e.Cancel = true;
		}
		*/
	}
}
