using Microsoft.UI.Xaml;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed class PeopleCodeCompareWindow : Window
{
    public PeopleCodeCompareWindow(PeopleCodeCompareWindowViewModel viewModel)
    {
        Title = viewModel.WindowTitle;
        Content = new PeopleCodeCompareView(viewModel);
    }
}
