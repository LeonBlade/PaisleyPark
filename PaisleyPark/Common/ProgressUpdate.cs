using SaintCoinach.Ex.Relational.Update;
using System;

namespace PaisleyPark.Common
{
	public class ProgressUpdate : IProgress<UpdateProgress>
	{
		public event EventHandler<UpdateProgress> UpdateEvent;

		public void Report(UpdateProgress value)
		{
			UpdateEvent.Invoke(this, value);
		}
	}
}
