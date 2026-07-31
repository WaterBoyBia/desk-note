using System.Windows;
using AppThemeMode = DeskNote.App.Models.ThemeMode;

namespace DeskNote.App.Services;

public sealed class ThemeService
{
    public event Action<AppThemeMode>? Changed;

    public void Apply(AppThemeMode mode)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("/Resources/Themes/", StringComparison.Ordinal) == true);
        if (current is not null)
        {
            dictionaries.Remove(current);
        }

        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"/Resources/Themes/{mode}.xaml", UriKind.Relative)
        });

        Changed?.Invoke(mode);
    }
}
