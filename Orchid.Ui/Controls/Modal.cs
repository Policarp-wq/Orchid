namespace Orchid.Ui.Controls;

/// <summary>Shows one modal at a time over the window. The host implements it; controls and views find it by walking up their parents.</summary>
public interface IModalPresenter
{
    /// <summary>Opens the modal. A modal already open is replaced and comes back when this one closes.</summary>
    void Present(string title, View body, Action? closed = null);

    void Dismiss();
}

public static class Modal
{
    public static IModalPresenter? Of(Element element)
    {
        for (var node = element; node is not null; node = node.Parent)
            if (node is IModalPresenter presenter) return presenter;
        return null;
    }
}
