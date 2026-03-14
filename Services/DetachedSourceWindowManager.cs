using System.Collections.Generic;
using Microsoft.UI.Xaml;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Views;

namespace PeopleCodeIDECompanion.Services;

public sealed class DetachedSourceWindowManager
{
    private readonly List<Window> _openWindows = [];

    public void Open(DetachedPeopleCodeSourceContext context)
    {
        DetachedSourceWindow window = new(context);
        window.Closed += DetachedWindow_Closed;
        _openWindows.Add(window);
        window.Activate();
    }

    private void DetachedWindow_Closed(object sender, WindowEventArgs args)
    {
        if (sender is Window window)
        {
            window.Closed -= DetachedWindow_Closed;
            _openWindows.Remove(window);
        }
    }
}
