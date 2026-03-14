using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class OracleSessionManager
{
    public OracleSessionManager()
    {
        Sessions.CollectionChanged += Sessions_CollectionChanged;
    }

    public event EventHandler? SessionsChanged;

    public event EventHandler<OracleConnectionSession?>? SelectedSessionChanged;

    public ObservableCollection<OracleConnectionSession> Sessions { get; } = [];

    public OracleConnectionSession? SelectedSession { get; private set; }

    public void AddOrUpdate(OracleConnectionSession session, bool selectSession = true)
    {
        int existingIndex = Sessions
            .Select((existingSession, index) => new { existingSession, index })
            .Where(item => item.existingSession.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();

        if (existingIndex >= 0)
        {
            Sessions[existingIndex] = session;
        }
        else
        {
            Sessions.Add(session);
        }

        if (selectSession || SelectedSession is null)
        {
            SelectSession(session.ProfileId);
        }
    }

    public void SelectSession(string profileId)
    {
        OracleConnectionSession? nextSession = Sessions.FirstOrDefault(session =>
            session.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));

        if (ReferenceEquals(SelectedSession, nextSession))
        {
            return;
        }

        SelectedSession = nextSession;
        SelectedSessionChanged?.Invoke(this, SelectedSession);
    }

    public OracleConnectionSession? PromoteSession(string existingSessionProfileId, SavedOracleConnectionProfile profile)
    {
        OracleConnectionSession? existingSession = Sessions.FirstOrDefault(session =>
            session.ProfileId.Equals(existingSessionProfileId, StringComparison.OrdinalIgnoreCase));

        if (existingSession is null)
        {
            return null;
        }

        OracleConnectionSession updatedSession = new()
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            CredentialTargetId = profile.CredentialTargetId,
            Options = existingSession.Options
        };

        int existingIndex = Sessions.IndexOf(existingSession);
        Sessions[existingIndex] = updatedSession;

        if (SelectedSession is not null &&
            SelectedSession.ProfileId.Equals(existingSession.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            SelectedSession = updatedSession;
            SelectedSessionChanged?.Invoke(this, SelectedSession);
        }

        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return updatedSession;
    }

    public bool RemoveSession(string profileId)
    {
        OracleConnectionSession? existingSession = Sessions.FirstOrDefault(session =>
            session.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));

        if (existingSession is null)
        {
            return false;
        }

        Sessions.Remove(existingSession);
        return true;
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedSession is not null &&
            Sessions.All(session => !session.ProfileId.Equals(SelectedSession.ProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedSession = Sessions.FirstOrDefault();
            SelectedSessionChanged?.Invoke(this, SelectedSession);
        }
        else if (SelectedSession is null && Sessions.Count > 0)
        {
            SelectedSession = Sessions[0];
            SelectedSessionChanged?.Invoke(this, SelectedSession);
        }

        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
