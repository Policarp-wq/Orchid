namespace Orchid.UiPreview;

internal static class ThemeResources
{
    // Looks up a resource from Theme.xaml; ResourceDictionary's indexer skips merged dictionaries, TryGetValue does not.
    public static T Get<T>(string key) =>
        Microsoft.Maui.Controls.Application.Current!.Resources.TryGetValue(key, out var value) && value is T typed
            ? typed
            : throw new InvalidOperationException($"'{key}' is not defined in Theme.xaml.");
}
