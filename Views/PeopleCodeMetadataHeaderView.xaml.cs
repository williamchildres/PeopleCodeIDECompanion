using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeMetadataHeaderView : UserControl
{
    private readonly Brush? _secondaryBrush;
    private readonly Brush? _primaryBrush;

    public PeopleCodeMetadataHeaderView()
    {
        InitializeComponent();
        _secondaryBrush = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush;
        _primaryBrush = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
        SetTitle(string.Empty);
        SetTypeText(string.Empty);
        SetUpdatedText(string.Empty);
        SetKeysText(string.Empty);
    }

    public Button OpenButton => OpenButtonElement;

    public Button CompareButton => CompareButtonElement;

    public string TitleText => TitleTextBlock.Text;

    public string TypeValueText { get; private set; } = string.Empty;

    public string UpdatedValueText { get; private set; } = string.Empty;

    public string KeysValueText { get; private set; } = string.Empty;

    public void SetTitle(string value)
    {
        string title = value ?? string.Empty;
        TitleTextBlock.Text = title;
        ToolTipService.SetToolTip(TitleTextBlock, string.IsNullOrWhiteSpace(title) ? null : title);
    }

    public void SetTypeText(string value)
    {
        TypeValueText = value ?? string.Empty;
        SetLabeledText(TypeTextBlock, "Type", TypeValueText, _primaryBrush);
        TypeTextBlock.Visibility = string.IsNullOrWhiteSpace(TypeValueText) ? Visibility.Collapsed : Visibility.Visible;
        UpdateSecondaryRowVisibility();
    }

    public void SetUpdatedText(string value)
    {
        UpdatedValueText = value ?? string.Empty;
        SetLabeledText(UpdatedTextBlock, "Updated by", UpdatedValueText, _secondaryBrush);
        UpdatedTextBlock.Visibility = string.IsNullOrWhiteSpace(UpdatedValueText) ? Visibility.Collapsed : Visibility.Visible;
        UpdateSecondaryRowVisibility();
    }

    public void SetKeysText(string value, string label = "Keys")
    {
        KeysValueText = value ?? string.Empty;
        SetLabeledText(KeysTextBlock, label, KeysValueText, _primaryBrush);
        KeysTextBlock.Visibility = string.IsNullOrWhiteSpace(KeysValueText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateSecondaryRowVisibility()
    {
        TypeTextBlock.Visibility = string.IsNullOrWhiteSpace(TypeValueText) ? Visibility.Collapsed : Visibility.Visible;

        DetailsRowGrid.Visibility =
            KeysTextBlock.Visibility == Visibility.Visible || UpdatedTextBlock.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void SetLabeledText(TextBlock target, string label, string value, Brush? valueBrush)
    {
        target.Inlines.Clear();
        ToolTipService.SetToolTip(target, string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}");

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        target.Inlines.Add(new Run
        {
            Text = $"{label}: ",
            Foreground = _secondaryBrush
        });

        target.Inlines.Add(new Run
        {
            Text = value,
            Foreground = valueBrush
        });
    }
}
