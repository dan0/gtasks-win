using Microsoft.UI.Xaml.Data;
using Windows.UI.Text;

namespace GTasks.UI.Converters;

public class BoolToStrikethroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isCompleted && isCompleted)
        {
            return TextDecorations.Strikethrough;
        }
        return TextDecorations.None;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
