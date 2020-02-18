using System.ComponentModel;

namespace PaisleyPark.Models
{
	/// <summary>
	/// Waymark Model prepresents the data behind a Waymark.
	/// </summary>
	public class Waymark : INotifyPropertyChanged
	{
		/// <summary>
		/// X Coordinate of Waymark.
		/// </summary>
		public float X { get; set; }

		/// <summary>
		/// Y Coordinate of Waymark.
		/// </summary>
		public float Y { get; set; }

		/// <summary>
		/// Z Coordinate of Waymark.
		/// </summary>
		public float Z { get; set; }

		/// <summary>
		/// ID of Waymark.
		/// </summary>
		public WaymarkID ID { get; set; }

		/// <summary>
		/// Active state of the Waymark.
		/// </summary>
		public bool Active { get; set; }

		public override bool Equals(object obj)
		{
			if (obj.GetType() != typeof(Waymark))
				return base.Equals(obj);
			var w = obj as Waymark;
			return w.X == X && w.Y == Y && w.Z == Z && w.Active == Active && w.ID == ID;
		}

		public override int GetHashCode() => X.GetHashCode() & Y.GetHashCode() & Z.GetHashCode() & ID.GetHashCode() & Active.GetHashCode();

		/// <summary>
		/// PropertyChanged event handler for this model.
		/// </summary>
#pragma warning disable 67
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
	}

	/// <summary>
	/// Waymark ID is the byte value of the waymark ID in memory.
	/// </summary>
	public enum WaymarkID : byte { A = 0, B, C, D, One, Two, Three, Four }
}
