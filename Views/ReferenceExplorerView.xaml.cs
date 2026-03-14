using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class ReferenceExplorerView : UserControl, INotifyPropertyChanged
{
    private readonly List<ReferenceItem> _allReferences;
    private string _errorMessage = string.Empty;
    private ReferenceItem _selectedReference = ReferenceItem.Empty;

    public ReferenceExplorerView() : this(new JsonReferenceItemLoader())
    {
    }

    public ReferenceExplorerView(IReferenceItemLoader referenceItemLoader)
    {
        InitializeComponent();

        ReferenceItemLoadResult loadResult = referenceItemLoader.LoadReferenceItems();
        ErrorMessage = loadResult.ErrorMessage;
        _allReferences = loadResult.Items.ToList();
        FilteredReferences = new ObservableCollection<ReferenceItem>(_allReferences);

        if (FilteredReferences.Count > 0)
        {
            SelectedReference = FilteredReferences[0];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ReferenceItem> FilteredReferences { get; }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ErrorMessageVisibility));
        }
    }

    public Visibility ErrorMessageVisibility => string.IsNullOrWhiteSpace(ErrorMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public ReferenceItem SelectedReference
    {
        get => _selectedReference;
        set
        {
            if (_selectedReference == value)
            {
                return;
            }

            _selectedReference = value ?? ReferenceItem.Empty;
            OnPropertyChanged();
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        ApplyFilter(sender.Text);
    }

    private void ApplyFilter(string? searchText)
    {
        IEnumerable<ReferenceItem> matches = _allReferences;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            matches = matches.Where(reference =>
                reference.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                reference.Category.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                reference.Signature.Contains(searchText, System.StringComparison.OrdinalIgnoreCase));
        }

        FilteredReferences.Clear();

        foreach (ReferenceItem reference in matches)
        {
            FilteredReferences.Add(reference);
        }

        SelectedReference = FilteredReferences.FirstOrDefault() ?? ReferenceItem.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
