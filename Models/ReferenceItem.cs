namespace PeopleCodeIDECompanion.Models;

public sealed class ReferenceItem
{
    public static ReferenceItem Empty { get; } = new()
    {
        Name = string.Empty,
        Category = string.Empty,
        Signature = string.Empty,
        Imports = string.Empty,
        Notes = string.Empty
    };

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string Imports { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
