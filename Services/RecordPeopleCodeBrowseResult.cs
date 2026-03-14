using System.Collections.Generic;
using System;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class RecordPeopleCodeBrowseResult
{
    public IReadOnlyList<RecordPeopleCodeItem> Items { get; init; } = [];

    public TimeSpan LoadDuration { get; init; }

    public string WarningMessage { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;
}
