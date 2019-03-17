using System.Windows;

namespace PaisleyPark.Common
{
	public static class DialogCloser
	{
		public static readonly DependencyProperty DialogResultProperty = DependencyProperty.RegisterAttached(
			"DialogResult",
			typeof(bool?),
			typeof(DialogCloser),
			new PropertyMetadata(OnDialogResultChanged)
		);

		private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Window window && window != null && window.IsVisible)
				window.DialogResult = e.NewValue as bool?;
		}

		public static void SetDialogResult(Window target, bool? value) => target.SetValue(DialogResultProperty, value);
	}
}
