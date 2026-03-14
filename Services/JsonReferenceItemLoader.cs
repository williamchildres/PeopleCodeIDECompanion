using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class JsonReferenceItemLoader : IReferenceItemLoader
{
    private const string DefaultErrorMessage = "Reference data could not be loaded.";
    private const string ReferenceItemsFileName = "ReferenceItems.json";

    public ReferenceItemLoadResult LoadReferenceItems()
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, ReferenceItemsFileName);

        try
        {
            string json = File.ReadAllText(filePath);
            List<ReferenceItem>? references = JsonSerializer.Deserialize<List<ReferenceItem>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return ReferenceItemLoadResult.Success(references ?? []);
        }
        catch (Exception)
        {
            return ReferenceItemLoadResult.Failure(DefaultErrorMessage);
        }
    }
}
