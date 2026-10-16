using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Orchid.Ui.Controls;

// Escape closes, Tab stays inside the card, focus moves to the close button on open and back to the opener on close.
public partial class ModalDialog
{
    private UIElement? _keyElement;
    private KeyEventHandler? _keyHandler;
    private Control? _opener;

    partial void OnOpenedPlatform()
    {
        if (Handler?.PlatformView is not UIElement root) return;
        if (!ReferenceEquals(_keyElement, root))
        {
            // handledEventsToo: the focused button marks Escape and Tab as handled before they reach the dialog.
            _keyHandler ??= new KeyEventHandler(OnKeyDown);
            _keyElement?.RemoveHandler(UIElement.KeyDownEvent, _keyHandler);
            root.AddHandler(UIElement.KeyDownEvent, _keyHandler, true);
            _keyElement = root;
        }
        if (_card.Handler?.PlatformView is UIElement card) card.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        _opener = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot) as Control;
        // A modal reopened inside the close of another one is not laid out yet on the first try.
        if (!_close.Focus()) Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () => _close.Focus());
    }

    partial void OnClosedPlatform()
    {
        _opener?.Focus(FocusState.Programmatic);
        _opener = null;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Escape || !IsOpen) return;
        args.Handled = true;
        Close();
    }
}
