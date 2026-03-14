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

    public Visibility NoResultsVisibility => string.IsNullOrWhiteSpace(ErrorMessage) && FilteredReferences.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

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
        ApplyFilter(sender.Text);
    }

    private void ApplyFilter(string? searchText)
    {
        IEnumerable<ReferenceItem> matches = _allReferences;
        ReferenceItem previousSelection = SelectedReference;
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            matches = matches.Where(reference =>
                Matches(reference, normalizedSearchText));
        }

        FilteredReferences.Clear();

        foreach (ReferenceItem reference in matches)
        {
            FilteredReferences.Add(reference);
        }

        SelectedReference = FilteredReferences.Contains(previousSelection)
            ? previousSelection
            : FilteredReferences.FirstOrDefault() ?? ReferenceItem.Empty;

        OnPropertyChanged(nameof(NoResultsVisibility));
    }

    private static bool Matches(ReferenceItem reference, string searchText)
    {
        return Contains(reference.Name, searchText) ||
               Contains(reference.Category, searchText) ||
               Contains(reference.Signature, searchText) ||
               Contains(reference.Imports, searchText) ||
               Contains(reference.Notes, searchText);
    }

    private static bool Contains(string? value, string searchText)
    {
        return !string.IsNullOrEmpty(value) &&
               value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
