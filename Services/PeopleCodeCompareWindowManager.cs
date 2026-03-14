using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Views;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleCodeCompareWindowManager
{
    private readonly OracleSessionManager _sessionManager;
    private readonly PeopleCodeCompareService _compareService = new();
    private readonly List<Window> _openWindows = [];

    public PeopleCodeCompareWindowManager(OracleSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public IReadOnlyList<OracleConnectionSession> GetAvailableComparisonProfiles(OracleConnectionSession? currentSession)
    {
        if (currentSession is null)
        {
            return [];
        }

        return _sessionManager.Sessions
            .Where(session => !session.ProfileId.Equals(currentSession.ProfileId, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(session => session.DisplayName, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool CanCompare(OracleConnectionSession? currentSession, bool hasLoadedSource)
    {
        return hasLoadedSource && GetAvailableComparisonProfiles(currentSession).Count > 0;
    }

    public async Task OpenAsync(PeopleCodeCompareRequest request)
    {
        PeopleCodeCompareWindowViewModel viewModel = await _compareService.BuildViewModelAsync(request);
        PeopleCodeCompareWindow window = new(viewModel);
        window.Closed += Window_Closed;
        _openWindows.Add(window);
        window.Activate();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (sender is Window window)
        {
            window.Closed -= Window_Closed;
            _openWindows.Remove(window);
        }
    }
}
