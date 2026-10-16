namespace Orchid.Ui.Theme;

internal static class ThemeResources
{
    // ResourceDictionary's indexer skips merged dictionaries, TryGetValue does not.
    public static T Get<T>(string key) =>
        Microsoft.Maui.Controls.Application.Current is { } app && app.Resources.TryGetValue(key, out var value) && value is T typed
            ? typed
            : throw new InvalidOperationException($"'{key}' is not defined in the Orchid theme.");
}
