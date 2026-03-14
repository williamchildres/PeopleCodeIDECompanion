using System;
using System.Collections.ObjectModel;
using System.Linq;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleCodeObjectStatusStore
{
    public PeopleCodeObjectStatusStore()
    {
        Items =
        [
            new PeopleCodeObjectStatusItem(AllObjectsPeopleCodeBrowserService.AppPackageMode),
            new PeopleCodeObjectStatusItem(AllObjectsPeopleCodeBrowserService.AppEngineMode),
            new PeopleCodeObjectStatusItem(AllObjectsPeopleCodeBrowserService.RecordMode),
            new PeopleCodeObjectStatusItem(AllObjectsPeopleCodeBrowserService.PageMode),
            new PeopleCodeObjectStatusItem(AllObjectsPeopleCodeBrowserService.ComponentMode)
        ];
    }

    public ObservableCollection<PeopleCodeObjectStatusItem> Items { get; }

    public void ResetAll()
    {
        foreach (PeopleCodeObjectStatusItem item in Items)
        {
            item.Reset();
        }
    }

    public void SetSessionAvailable(string objectTypeName, bool hasSession)
    {
        GetItem(objectTypeName).SetSessionAvailable(hasSession);
    }

    public void MarkLoading(string objectTypeName)
    {
        GetItem(objectTypeName).MarkLoading();
    }

    public void MarkLoaded(string objectTypeName)
    {
        GetItem(objectTypeName).MarkLoaded(DateTimeOffset.Now);
    }

    public void MarkError(string objectTypeName)
    {
        GetItem(objectTypeName).MarkError();
    }

    private PeopleCodeObjectStatusItem GetItem(string objectTypeName)
    {
        return Items.First(item => item.ObjectTypeName.Equals(objectTypeName, StringComparison.Ordinal));
    }
}
