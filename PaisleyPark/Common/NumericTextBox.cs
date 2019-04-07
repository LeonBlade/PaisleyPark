using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace PaisleyPark.Common
{
    public class NumericTextBox : TextBox
    {
        private readonly Regex NumericRegex = new Regex("[^0-9]+");

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            e.Handled = !IsNumeric(e.Text);
        }

        private bool IsNumeric(string text)
        {
            return !NumericRegex.IsMatch(text);
        }
    }
}
