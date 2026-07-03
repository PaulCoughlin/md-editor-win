using System.Text;
using System.Windows.Documents;
using System.Windows;

namespace MdEditor.Markdown;

/// <summary>
/// Save path: walk a FlowDocument's block/inline tree and emit markdown text.
/// This serializer is the real substance of the app. It is bounded because only
/// the fixed vocabulary is allowed, but it is where the effort lives.
///
/// Block elements are separated by a blank line. Inline formatting maps to markdown
/// delimiters. Element Tags set by <see cref="MarkdownToFlowDocument"/> (h1..h6,
/// quote, codeblock, hr, sub, sup) are honoured on the round trip.
/// </summary>
public static class FlowDocumentToMarkdown
{
    public static string Serialize(FlowDocument doc)
    {
        var sb = new StringBuilder();
        WriteBlocks(sb, doc.Blocks, indent: 0);
        // Normalise trailing whitespace to a single newline.
        return sb.ToString().TrimEnd('\n', '\r') + "\n";
    }

    private static void WriteBlocks(StringBuilder sb, BlockCollection blocks, int indent)
    {
        foreach (Block block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    WriteParagraph(sb, paragraph, indent);
                    break;
                case List list:
                    WriteList(sb, list, indent);
                    break;
                case Section section:
                    WriteSection(sb, section, indent);
                    break;
                case Table table:
                    WriteTable(sb, table);
                    break;
            }
        }
    }

    private static void WriteParagraph(StringBuilder sb, Paragraph paragraph, int indent)
    {
        string tag = paragraph.Tag as string ?? "";
        string pad = new string(' ', indent);

        if (tag == "hr")
        {
            sb.Append(pad).Append("---\n\n");
            return;
        }

        if (tag == "codeblock")
        {
            string code = InlinesToText(paragraph.Inlines);
            sb.Append(pad).Append("```\n");
            foreach (var line in code.Replace("\r\n", "\n").Split('\n'))
                sb.Append(pad).Append(line).Append('\n');
            sb.Append(pad).Append("```\n\n");
            return;
        }

        string content = InlinesToMarkdown(paragraph.Inlines).Trim();

        if (tag.Length == 2 && tag[0] == 'h' && char.IsDigit(tag[1]))
        {
            int level = tag[1] - '0';
            sb.Append(pad).Append(new string('#', level)).Append(' ').Append(content).Append("\n\n");
            return;
        }

        // Blank paragraph -> skip (avoids stray blank lines multiplying on round trips).
        if (content.Length == 0)
            return;

        sb.Append(pad).Append(content).Append("\n\n");
    }

    private static void WriteList(StringBuilder sb, List list, int indent)
    {
        bool ordered = list.MarkerStyle == TextMarkerStyle.Decimal
                    || list.MarkerStyle == TextMarkerStyle.LowerLatin
                    || list.MarkerStyle == TextMarkerStyle.UpperRoman;
        int number = 1;
        string pad = new string(' ', indent);

        foreach (ListItem item in list.ListItems)
        {
            string marker = ordered ? $"{number}. " : "- ";
            // First block of the item goes on the marker line; nested blocks indent under it.
            bool first = true;
            foreach (Block child in item.Blocks)
            {
                if (child is Paragraph p)
                {
                    string text = InlinesToMarkdown(p.Inlines).Trim();
                    if (first)
                    {
                        sb.Append(pad).Append(marker).Append(text).Append('\n');
                        first = false;
                    }
                    else
                    {
                        sb.Append(pad).Append("  ").Append(text).Append('\n');
                    }
                }
                else if (child is List nested)
                {
                    WriteList(sb, nested, indent + 2);
                }
            }
            number++;
        }
        sb.Append('\n');
    }

    private static void WriteSection(StringBuilder sb, Section section, int indent)
    {
        if ((section.Tag as string) == "quote")
        {
            var inner = new StringBuilder();
            WriteBlocks(inner, section.Blocks, indent: 0);
            foreach (var line in inner.ToString().TrimEnd('\n').Split('\n'))
                sb.Append(line.Length == 0 ? ">" : "> " + line).Append('\n');
            sb.Append('\n');
        }
        else
        {
            WriteBlocks(sb, section.Blocks, indent);
        }
    }

    private static void WriteTable(StringBuilder sb, Table table)
    {
        var rows = new List<List<string>>();
        foreach (var group in table.RowGroups)
            foreach (TableRow row in group.Rows)
            {
                var cells = new List<string>();
                foreach (TableCell cell in row.Cells)
                {
                    var text = new StringBuilder();
                    foreach (Block b in cell.Blocks)
                        if (b is Paragraph p) text.Append(InlinesToMarkdown(p.Inlines).Trim());
                    cells.Add(text.ToString());
                }
                rows.Add(cells);
            }

        if (rows.Count == 0) return;
        int cols = rows[0].Count;

        void WriteRow(List<string> cells) =>
            sb.Append("| ").Append(string.Join(" | ", PadTo(cells, cols))).Append(" |\n");

        WriteRow(rows[0]);
        sb.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", cols))).Append(" |\n");
        for (int i = 1; i < rows.Count; i++)
            WriteRow(rows[i]);
        sb.Append('\n');
    }

    private static IEnumerable<string> PadTo(List<string> cells, int cols)
    {
        for (int i = 0; i < cols; i++)
            yield return i < cells.Count ? cells[i] : "";
    }

    // ---- inline serialization ----

    private static string InlinesToMarkdown(InlineCollection inlines)
    {
        var sb = new StringBuilder();
        foreach (Inline inline in inlines)
            WriteInline(sb, inline);
        return sb.ToString();
    }

    private static void WriteInline(StringBuilder sb, Inline inline)
    {
        switch (inline)
        {
            case Run run when (run.Tag as string)?.StartsWith("img:") == true:
                sb.Append("![](").Append(((string)run.Tag)["img:".Length..]).Append(')');
                break;

            case Run run:
                sb.Append(WrapRun(run));
                break;

            case Bold bold:
                sb.Append("**").Append(InlinesToMarkdown(bold.Inlines)).Append("**");
                break;

            case Italic italic:
                sb.Append('*').Append(InlinesToMarkdown(italic.Inlines)).Append('*');
                break;

            case Hyperlink link:
                string text = InlinesToMarkdown(link.Inlines);
                string url = link.NavigateUri?.ToString() ?? "";
                sb.Append('[').Append(text).Append("](").Append(url).Append(')');
                break;

            case Span span when (span.Tag as string) == "sub":
                sb.Append('~').Append(InlinesToMarkdown(span.Inlines)).Append('~');
                break;

            case Span span when (span.Tag as string) == "sup":
                sb.Append('^').Append(InlinesToMarkdown(span.Inlines)).Append('^');
                break;

            case Span span:
                sb.Append(WrapSpan(span));
                break;

            case LineBreak:
                sb.Append("  \n");
                break;
        }
    }

    /// <summary>
    /// A bare Run may still carry formatting (applied via the toolbar's ToggleBold/
    /// ToggleItalic, or inline code styling) without being wrapped in Bold/Italic
    /// elements. Detect those properties and emit the right delimiters.
    /// </summary>
    private static string WrapRun(Run run)
    {
        string text = run.Text;
        if (text.Length == 0) return text;

        // Only emit delimiters for formatting set LOCALLY on this Run. Formatting
        // inherited from a parent element (a Bold/Italic wrapper, a heading, a bold
        // table header) is emitted by that element's own case, not here — otherwise
        // it would be double-wrapped.
        bool code = IsLocallySet(run, TextElement.FontFamilyProperty)
                    && run.FontFamily?.Source == "Consolas";
        if (code) return "`" + text + "`";

        bool bold = IsLocallySet(run, TextElement.FontWeightProperty)
                    && run.FontWeight == FontWeights.Bold;
        bool italic = IsLocallySet(run, TextElement.FontStyleProperty)
                    && run.FontStyle == FontStyles.Italic;

        if (bold && italic) return "***" + text + "***";
        if (bold) return "**" + text + "**";
        if (italic) return "*" + text + "*";
        return text;
    }

    private static bool IsLocallySet(Run run, System.Windows.DependencyProperty property) =>
        run.ReadLocalValue(property) != System.Windows.DependencyProperty.UnsetValue;

    private static string WrapSpan(Span span)
    {
        string inner = InlinesToMarkdown(span.Inlines);
        if (inner.Length == 0) return inner;

        bool bold = span.FontWeight == FontWeights.Bold;
        bool italic = span.FontStyle == FontStyles.Italic;

        if (bold && italic) return "***" + inner + "***";
        if (bold) return "**" + inner + "**";
        if (italic) return "*" + inner + "*";
        return inner;
    }

    private static string InlinesToText(InlineCollection inlines)
    {
        var sb = new StringBuilder();
        foreach (Inline inline in inlines)
        {
            if (inline is Run run) sb.Append(run.Text);
            else if (inline is Span span) sb.Append(InlinesToText(span.Inlines));
            else if (inline is LineBreak) sb.Append('\n');
        }
        return sb.ToString();
    }
}
