using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeCompareView : UserControl
{
    public PeopleCodeCompareView(PeopleCodeCompareWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public PeopleCodeCompareWindowViewModel ViewModel { get; }
}
