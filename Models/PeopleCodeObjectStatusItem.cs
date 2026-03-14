using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeObjectStatusItem : INotifyPropertyChanged
{
    private string _statusText = "Not loaded";
    private DateTimeOffset? _lastLoadedAt;
    private bool _hasSession;
    private bool _isLoading;

    public PeopleCodeObjectStatusItem(string objectTypeName)
    {
        ObjectTypeName = objectTypeName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ObjectTypeName { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public DateTimeOffset? LastLoadedAt
    {
        get => _lastLoadedAt;
        private set
        {
            if (SetProperty(ref _lastLoadedAt, value))
            {
                OnPropertyChanged(nameof(LastLoadedText));
            }
        }
    }

    public string LastLoadedText => LastLoadedAt?.ToLocalTime().ToString("g") ?? "Never";

    public string LastLoadedDisplayText => $"Last loaded {LastLoadedText}";

    public bool CanRefresh => _hasSession && !_isLoading;

    internal void Reset()
    {
        _hasSession = false;
        _isLoading = false;
        StatusText = "Not loaded";
        LastLoadedAt = null;
        OnPropertyChanged(nameof(CanRefresh));
    }

    internal void SetSessionAvailable(bool hasSession)
    {
        if (_hasSession == hasSession)
        {
            return;
        }

        _hasSession = hasSession;
        OnPropertyChanged(nameof(CanRefresh));
    }

    internal void MarkLoading()
    {
        _isLoading = true;
        StatusText = "Loading...";
        OnPropertyChanged(nameof(CanRefresh));
    }

    internal void MarkLoaded(DateTimeOffset loadedAt)
    {
        _isLoading = false;
        StatusText = "Loaded";
        LastLoadedAt = loadedAt;
        OnPropertyChanged(nameof(CanRefresh));
    }

    internal void MarkError()
    {
        _isLoading = false;
        StatusText = "Error";
        OnPropertyChanged(nameof(CanRefresh));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
