using Microsoft.UI.Xaml;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed class DetachedSourceWindow : Window
{
    public DetachedSourceWindow(DetachedPeopleCodeSourceContext context)
    {
        Title = context.WindowTitle;
        Content = new DetachedSourceView(context);
    }
}
