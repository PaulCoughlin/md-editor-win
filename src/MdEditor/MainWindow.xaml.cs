using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using MdEditor.Markdown;
using Microsoft.Win32;

namespace MdEditor;

/// <summary>
/// The app shell: menu, toolbar, and the RichTextBox editing surface, plus file I/O,
/// dirty-state tracking, and the formatting commands. The two markdown translations
/// live in <see cref="MarkdownToFlowDocument"/> (open) and
/// <see cref="FlowDocumentToMarkdown"/> (save).
/// </summary>
public partial class MainWindow : Window
{
    private string? _currentPath;
    private bool _isDirty;
    private bool _suppressDirty;
    private readonly Settings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load();

        // Start with a genuinely clean blank document. Routing through LoadDocument
        // (under the suppress-dirty guard) prevents the initial assignment from
        // marking the untouched document dirty — otherwise Open/close would wrongly
        // prompt "Save changes?" on a blank file.
        LoadDocument(new FlowDocument(new Paragraph()));
        MarkClean();

        ApplySettingsToEditor();
        RestoreWindowBounds();

        // A persistent (initially empty) context menu, repopulated on each open.
        // Assigning it here rather than per-open avoids a timing race where the
        // freshly-assigned menu is not shown on the click that created it.
        Editor.ContextMenu = new ContextMenu();
    }

    // ---- settings ----

    /// <summary>Applies the current settings' font, size, and spellcheck to the editor.</summary>
    private void ApplySettingsToEditor()
    {
        Editor.FontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily);
        Editor.FontSize = _settings.FontSize;

        if (_settings.SpellcheckLanguage == "off")
        {
            Editor.SpellCheck.IsEnabled = false;
        }
        else
        {
            Editor.SpellCheck.IsEnabled = true;
            // The spellcheck dictionary is chosen from the element's Language.
            Editor.Language = XmlLanguage.GetLanguage(_settings.SpellcheckLanguage);
        }
    }

    private void RestoreWindowBounds()
    {
        if (_settings.WindowWidth is not double w || _settings.WindowHeight is not double h)
            return;

        // Only restore a saved position if it lands on a visible screen.
        if (_settings.WindowLeft is double left && _settings.WindowTop is double top
            && left + w > SystemParameters.VirtualScreenLeft
            && top + h > SystemParameters.VirtualScreenTop
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        Width = w;
        Height = h;
    }

    private void SaveWindowBounds()
    {
        // Persist the normal (non-maximised) bounds so restore is sensible.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        SettingsStore.Save(_settings);
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PreferencesWindow(_settings, ApplySettingsToEditor) { Owner = this };
        dialog.ShowDialog();
    }

    // ---- dirty state ----

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDirty) return;
        _isDirty = true;
        UpdateTitle();
    }

    private void MarkClean()
    {
        _isDirty = false;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = _currentPath is null ? "Untitled" : Path.GetFileName(_currentPath);
        Title = (_isDirty ? "* " : "") + name + " — Markdown Editor";
        StatusText.Text = _currentPath ?? "Unsaved document";
    }

    /// <summary>Returns true if it is safe to discard the current document.</summary>
    private bool ConfirmDiscard()
    {
        if (!_isDirty) return true;
        var result = MessageBox.Show(
            "Save changes to the current document?",
            "Markdown Editor",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
        }
        else
        {
            SaveWindowBounds();
        }
        base.OnClosing(e);
    }

    // ---- file commands ----

    private void New_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        LoadDocument(new FlowDocument(new Paragraph()));
        _currentPath = null;
        MarkClean();
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            string markdown = File.ReadAllText(dialog.FileName);
            LoadDocument(MarkdownToFlowDocument.Convert(markdown));
            _currentPath = dialog.FileName;
            MarkClean();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open file:\n" + ex.Message, "Markdown Editor",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => Save();

    private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    /// <summary>Saves to the current path, prompting for one if none is set.</summary>
    private bool Save()
    {
        if (_currentPath is null) return SaveAs();
        return WriteToDisk(_currentPath);
    }

    private bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md",
            FileName = _currentPath is null ? "Untitled.md" : Path.GetFileName(_currentPath),
        };
        if (dialog.ShowDialog(this) != true) return false;
        if (WriteToDisk(dialog.FileName))
        {
            _currentPath = dialog.FileName;
            UpdateTitle();
            return true;
        }
        return false;
    }

    private bool WriteToDisk(string path)
    {
        try
        {
            string markdown = FlowDocumentToMarkdown.Serialize(Editor.Document);
            File.WriteAllText(path, markdown);
            MarkClean();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save file:\n" + ex.Message, "Markdown Editor",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void LoadDocument(FlowDocument doc)
    {
        _suppressDirty = true;
        Editor.Document = doc;
        _suppressDirty = false;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ---- keyboard shortcuts ----

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        int? level = e.Key switch
        {
            Key.D0 or Key.NumPad0 => 0,
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            Key.D6 or Key.NumPad6 => 6,
            _ => null,
        };
        if (level is int l)
        {
            SetBlockHeading(l);
            e.Handled = true;
        }
    }

    // ---- formatting commands ----

    /// <summary>Sets the block containing the caret to a heading level (1..6) or body (0).</summary>
    private void SetBlockHeading(int level)
    {
        if (Editor.CaretPosition.Paragraph is not Paragraph p) return;
        if (level == 0)
        {
            p.Tag = null;
            p.FontSize = Editor.FontSize;
            p.FontWeight = FontWeights.Normal;
        }
        else
        {
            double[] sizes = { 28, 24, 20, 17, 15, 14 };
            p.Tag = "h" + level;
            p.FontSize = sizes[level - 1];
            p.FontWeight = FontWeights.Bold;
        }
        _isDirty = true;
        UpdateTitle();
    }

    private void H1_Click(object sender, RoutedEventArgs e) => SetBlockHeading(1);
    private void H2_Click(object sender, RoutedEventArgs e) => SetBlockHeading(2);
    private void H3_Click(object sender, RoutedEventArgs e) => SetBlockHeading(3);
    private void Body_Click(object sender, RoutedEventArgs e) => SetBlockHeading(0);

    private void InlineCode_Click(object sender, RoutedEventArgs e)
    {
        var selection = Editor.Selection;
        if (selection.IsEmpty) return;
        selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"));
        _isDirty = true;
        UpdateTitle();
    }

    private void BulletList_Click(object sender, RoutedEventArgs e) => ToggleList(TextMarkerStyle.Disc);
    private void NumberList_Click(object sender, RoutedEventArgs e) => ToggleList(TextMarkerStyle.Decimal);

    private void ToggleList(TextMarkerStyle style)
    {
        // EditingCommands handles the list creation; then set the marker style.
        EditingCommands.ToggleBullets.Execute(null, Editor);
        if (Editor.CaretPosition.Paragraph?.Parent is ListItem { Parent: List list })
            list.MarkerStyle = style;
        _isDirty = true;
        UpdateTitle();
    }

    private void Quote_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.CaretPosition.Paragraph is not Paragraph p) return;
        // Wrap the current paragraph's block in a quote Section.
        var section = new Section { Tag = "quote" };
        var block = p as Block;
        var parent = block.SiblingBlocks;
        if (parent is null) return;
        parent.InsertAfter(block, section);
        parent.Remove(block);
        section.Blocks.Add(block);
        _isDirty = true;
        UpdateTitle();
    }

    private void Rule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new Paragraph { Tag = "hr" };
        rule.BorderBrush = System.Windows.Media.Brushes.LightGray;
        rule.BorderThickness = new Thickness(0, 1, 0, 0);
        rule.Margin = new Thickness(0, 8, 0, 8);

        if (Editor.CaretPosition.Paragraph is Paragraph p && p.SiblingBlocks is not null)
            p.SiblingBlocks.InsertAfter(p, rule);
        else
            Editor.Document.Blocks.Add(rule);

        Editor.Document.Blocks.InsertAfter(rule, new Paragraph());
        _isDirty = true;
        UpdateTitle();
    }

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        var selection = Editor.Selection;
        if (selection.IsEmpty)
        {
            MessageBox.Show(this, "Select the text to turn into a link first.", "Markdown Editor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string text = selection.Text;
        var link = new Hyperlink(selection.Start, selection.End)
        {
            NavigateUri = new Uri("https://example.com"),
        };
        _isDirty = true;
        UpdateTitle();
    }

    // ---- table commands ----

    private void InsertTable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InsertTableWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var table = TableOperations.Build(dialog.Rows, dialog.Columns);

        // Insert after the caret's block, then leave an empty paragraph after the
        // table so the caret has somewhere to go below it.
        if (Editor.CaretPosition.Paragraph is Paragraph p && p.SiblingBlocks is not null)
            p.SiblingBlocks.InsertAfter(p, table);
        else
            Editor.Document.Blocks.Add(table);
        Editor.Document.Blocks.InsertAfter(table, new Paragraph());

        _isDirty = true;
        UpdateTitle();
    }

    /// <summary>Finds the table cell/row/column the caret sits in, or null if not in a table.</summary>
    private (Table table, int row, int col)? CaretTableLocation()
    {
        DependencyObject? node = Editor.CaretPosition.Paragraph;
        while (node is not null and not TableCell)
            node = node is FrameworkContentElement fce ? fce.Parent : null;

        if (node is not TableCell cell) return null;
        if (cell.Parent is not TableRow row) return null;
        if (row.Parent is not TableRowGroup group) return null;
        if (group.Parent is not Table table) return null;

        int rowIndex = group.Rows.IndexOf(row);
        int colIndex = row.Cells.IndexOf(cell);
        return (table, rowIndex, colIndex);
    }

    /// <summary>
    /// A right-click does not move the caret, so before the menu opens we move the
    /// caret to the click point. That makes the spelling-suggestion and table
    /// lookups (both keyed off the caret) reflect what the user actually clicked on.
    /// </summary>
    private void Editor_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor), snapToText: true);
        if (pos is not null)
            Editor.CaretPosition = pos;
    }

    /// <summary>
    /// Builds the editor's right-click menu on demand so it can combine WPF's
    /// spelling suggestions and clipboard commands with the table operations. A
    /// static ContextMenu in XAML would suppress the built-in spellcheck menu, so
    /// the menu is constructed fresh each time here. The editor's ContextMenu is
    /// assigned once in the constructor; here we only repopulate its items.
    /// </summary>
    private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = Editor.ContextMenu!;
        menu.Items.Clear();

        AddSpellingSuggestions(menu);
        AddClipboardCommands(menu);
        AddTableCommands(menu);
    }

    private void AddSpellingSuggestions(ContextMenu menu)
    {
        SpellingError? error = Editor.GetSpellingError(Editor.CaretPosition);
        if (error is null) return;

        bool any = false;
        foreach (string suggestion in error.Suggestions)
        {
            any = true;
            var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.Bold };
            item.Click += (_, _) => error.Correct(suggestion);
            menu.Items.Add(item);
        }
        if (!any)
            menu.Items.Add(new MenuItem { Header = "(No spelling suggestions)", IsEnabled = false });

        var ignore = new MenuItem { Header = "Ignore All" };
        ignore.Click += (_, _) => error.IgnoreAll();
        menu.Items.Add(ignore);
        menu.Items.Add(new Separator());
    }

    private void AddClipboardCommands(ContextMenu menu)
    {
        menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = Editor });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = Editor });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = Editor });
    }

    private void AddTableCommands(ContextMenu menu)
    {
        if (CaretTableLocation() is null) return;

        menu.Items.Add(new Separator());
        var table = new MenuItem { Header = "Table" };

        void Add(string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            table.Items.Add(item);
        }

        Add("Insert Row Above", InsertRowAbove_Click);
        Add("Insert Row Below", InsertRowBelow_Click);
        Add("Delete Row", DeleteRow_Click);
        table.Items.Add(new Separator());
        Add("Insert Column Left", InsertColumnLeft_Click);
        Add("Insert Column Right", InsertColumnRight_Click);
        Add("Delete Column", DeleteColumn_Click);
        table.Items.Add(new Separator());
        Add("Toggle Header Row", ToggleHeader_Click);

        menu.Items.Add(table);
    }

    private void WithTable(Action<Table, int, int> op)
    {
        if (CaretTableLocation() is not (Table t, int r, int c)) return;
        op(t, r, c);
        _isDirty = true;
        UpdateTitle();
    }

    private void InsertRowAbove_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) => TableOperations.InsertRow(t, r));

    private void InsertRowBelow_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) => TableOperations.InsertRow(t, r + 1));

    private void DeleteRow_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) =>
        {
            if (!TableOperations.DeleteRow(t, r))
                MessageBox.Show(this, "A table must keep at least one row.", "Markdown Editor",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        });

    private void InsertColumnLeft_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) => TableOperations.InsertColumn(t, c));

    private void InsertColumnRight_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) => TableOperations.InsertColumn(t, c + 1));

    private void DeleteColumn_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) =>
        {
            if (!TableOperations.DeleteColumn(t, c))
                MessageBox.Show(this, "A table must keep at least one column.", "Markdown Editor",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        });

    private void ToggleHeader_Click(object sender, RoutedEventArgs e) =>
        WithTable((t, r, c) => TableOperations.ToggleHeader(t));
}
