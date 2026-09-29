using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TaskManager.Models;

namespace TaskManager_WinUI.Converters;

public sealed class BoolVisibilityConverter:IValueConverter
{public object Convert(object value,Type targetType,object parameter,string language)=>value is true?Visibility.Visible:Visibility.Collapsed;public object ConvertBack(object value,Type targetType,object parameter,string language)=>value is Visibility.Visible;}
public sealed class InverseBoolVisibilityConverter:IValueConverter
{public object Convert(object value,Type targetType,object parameter,string language)=>value is true?Visibility.Collapsed:Visibility.Visible;public object ConvertBack(object value,Type targetType,object parameter,string language)=>value is not Visibility.Visible;}
public sealed class BoolOpacityConverter:IValueConverter
{public object Convert(object value,Type targetType,object parameter,string language)=>value is true?0.58d:1d;public object ConvertBack(object value,Type targetType,object parameter,string language)=>throw new NotSupportedException();}
public sealed class PriorityBrushConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,string language)=>new SolidColorBrush(value switch{TaskPriority.High=>ColorHelper.FromArgb(255,190,64,64),TaskPriority.Medium=>ColorHelper.FromArgb(255,173,112,22),_=>ColorHelper.FromArgb(255,35,137,104)});
    public object ConvertBack(object value,Type targetType,object parameter,string language)=>throw new NotSupportedException();
}
