using Microsoft.UI.Xaml.Data;

namespace GTasks.UI.Converters;

public class DateToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTimeOffset dto)
        {
            var today = DateTimeOffset.Now.Date;
            var date = dto.Date;

            if (date == today)
                return "Today";
            if (date == today.AddDays(1))
                return "Tomorrow";
            if (date == today.AddDays(-1))
                return "Yesterday";
            if (date < today)
                return $"Overdue ({date:MMM d})";
            if (date <= today.AddDays(7))
                return date.ToString("ddd");

            return date.ToString("MMM d");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
