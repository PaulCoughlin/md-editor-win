using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MdEditor;

/// <summary>
/// Preferences dialog. Each control change applies to the live editor (via
/// <see cref="_onChanged"/>) and persists immediately — there is no OK/Cancel.
/// </summary>
public partial class PreferencesWindow : Window
{
    private readonly Settings _settings;
    private readonly Action _onChanged;
    private bool _loading;

    private static readonly double[] CommonSizes = { 10, 11, 12, 13, 14, 15, 16, 18, 20, 24 };

    public PreferencesWindow(Settings settings, Action onChanged)
    {
        InitializeComponent();
        _settings = settings;
        _onChanged = onChanged;
        Populate();
    }

    private void Populate()
    {
        _loading = true;

        foreach (var family in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            FontCombo.Items.Add(family.Source);
        FontCombo.SelectedItem = _settings.FontFamily;

        foreach (var size in CommonSizes)
            SizeCombo.Items.Add(size.ToString(CultureInfo.InvariantCulture));
        SizeCombo.Text = _settings.FontSize.ToString(CultureInfo.InvariantCulture);

        SpellCombo.Items.Add("English (UK)");
        SpellCombo.Items.Add("English (US)");
        SpellCombo.Items.Add("Off");
        SpellCombo.SelectedIndex = _settings.SpellcheckLanguage switch
        {
            "en-US" => 1,
            "off" => 2,
            _ => 0,
        };

        _loading = false;
    }

    private void ApplyAndSave()
    {
        if (_loading) return;
        _onChanged();
        SettingsStore.Save(_settings);
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontCombo.SelectedItem is not string family) return;
        _settings.FontFamily = family;
        ApplyAndSave();
    }

    private void SizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => CommitSize();

    private void SizeCombo_LostFocus(object sender, RoutedEventArgs e) => CommitSize();

    private void CommitSize()
    {
        if (_loading) return;
        if (double.TryParse(SizeCombo.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double size)
            && size is >= 6 and <= 96)
        {
            _settings.FontSize = size;
            ApplyAndSave();
        }
    }

    private void SpellCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.SpellcheckLanguage = SpellCombo.SelectedIndex switch
        {
            1 => "en-US",
            2 => "off",
            _ => "en-GB",
        };
        ApplyAndSave();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
