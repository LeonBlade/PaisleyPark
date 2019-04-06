using System.Windows;

namespace PaisleyPark.Common
{
	public static class CloseProperty
    {
		public static DependencyProperty IsCloseProperty = DependencyProperty.RegisterAttached(
			"IsClose",
			typeof(bool),
			typeof(CloseProperty),
			new PropertyMetadata(false, new PropertyChangedCallback(OnNotifyPropertyChanged))
		);

		public static bool GetIsClose(UIElement win) => (bool)win.GetValue(IsCloseProperty);
		public static void SetIsClose(UIElement win, bool value) => win.SetValue(IsCloseProperty, value);

		private static void OnNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var win = d as Window;
			if ((bool)e.NewValue)
				win.Close();
		}
	}
}
