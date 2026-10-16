using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Orchid.Ui.Layouts;

// Window-level keys. Accelerators fire wherever the focus is in the window; a key is marked handled only when it did something.
public partial class AdaptiveHost
{
    private bool _keysAttached;

    partial void AttachPlatformKeys()
    {
        if (_keysAttached || Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window window || window.Content is not UIElement root) return;
        _keysAttached = true;
        Add(root, VirtualKey.Space, VirtualKeyModifiers.None, () =>
        {
            if (!_state.IsLive) return false;
            _state.StopCommand.Execute(null);
            return true;
        });
        foreach (var key in new[] { VirtualKey.Number0, VirtualKey.NumberPad0 })
            Add(root, key, VirtualKeyModifiers.Control, () =>
            {
                if (!_state.IsReview) return false;
                _state.Review.Fit();
                return true;
            });
    }

    private static void Add(UIElement root, VirtualKey key, VirtualKeyModifiers modifiers, Func<bool> act)
    {
        var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) => args.Handled = act();
        root.KeyboardAccelerators.Add(accelerator);
    }
}
