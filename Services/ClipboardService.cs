using Windows.ApplicationModel.DataTransfer;

namespace PeopleCodeIDECompanion.Services;

public static class ClipboardService
{
    public static void CopyText(string text)
    {
        DataPackage package = new();
        package.SetText(text ?? string.Empty);
        Clipboard.SetContent(package);
    }
}
