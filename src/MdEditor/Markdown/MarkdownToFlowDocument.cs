using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace MdEditor.Markdown;

/// <summary>
/// Open path: markdown text -> WPF FlowDocument (the rendered view).
/// Markdig parses to an AST; we map each supported node to a FlowDocument element.
/// Only the fixed vocabulary from the spec is handled.
/// </summary>
public static class MarkdownToFlowDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .Build();

    // Named heading font sizes (H1..H6). Body text is the RichTextBox default.
    private static readonly double[] HeadingSizes = { 28, 24, 20, 17, 15, 14 };

    public static FlowDocument Convert(string markdown)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0) };
        MarkdownDocument ast = Markdig.Markdown.Parse(markdown ?? string.Empty, Pipeline);

        foreach (MdBlock block in ast)
            AddBlock(doc.Blocks, block);

        // A FlowDocument must never be empty or the caret has nowhere to go.
        if (doc.Blocks.Count == 0)
            doc.Blocks.Add(new Paragraph());

        return doc;
    }

    private static void AddBlock(BlockCollection target, MdBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(BuildHeading(heading));
                break;

            case ParagraphBlock paragraph:
                target.Add(BuildParagraph(paragraph.Inline));
                break;

            case Markdig.Syntax.ListBlock list:
                target.Add(BuildList(list));
                break;

            case QuoteBlock quote:
                target.Add(BuildQuote(quote));
                break;

            case Markdig.Syntax.FencedCodeBlock fenced:
                target.Add(BuildCodeBlock(fenced));
                break;

            case CodeBlock code:
                target.Add(BuildCodeBlock(code));
                break;

            case ThematicBreakBlock:
                target.Add(BuildRule());
                break;

            case Markdig.Extensions.Tables.Table table:
                target.Add(BuildTable(table));
                break;
        }
    }

    private static Paragraph BuildHeading(HeadingBlock heading)
    {
        int level = System.Math.Clamp(heading.Level, 1, 6);
        var p = new Paragraph
        {
            FontSize = HeadingSizes[level - 1],
            FontWeight = FontWeights.Bold,
        };
        p.Tag = "h" + level; // remembered by the serializer on save
        AddInlines(p.Inlines, heading.Inline);
        return p;
    }

    private static Paragraph BuildParagraph(ContainerInline? inline)
    {
        var p = new Paragraph();
        AddInlines(p.Inlines, inline);
        return p;
    }

    private static List BuildList(Markdig.Syntax.ListBlock list)
    {
        var wpfList = new List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        };

        foreach (MdBlock item in list)
        {
            if (item is Markdig.Syntax.ListItemBlock listItem)
            {
                var wpfItem = new ListItem();
                foreach (MdBlock child in listItem)
                    AddBlock(wpfItem.Blocks, child);
                if (wpfItem.Blocks.Count == 0)
                    wpfItem.Blocks.Add(new Paragraph());
                wpfList.ListItems.Add(wpfItem);
            }
        }

        return wpfList;
    }

    private static Section BuildQuote(QuoteBlock quote)
    {
        var section = new Section
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 0, 0, 0),
            Foreground = Brushes.DimGray,
        };
        section.Tag = "quote";
        foreach (MdBlock child in quote)
            AddBlock(section.Blocks, child);
        if (section.Blocks.Count == 0)
            section.Blocks.Add(new Paragraph());
        return section;
    }

    private static Paragraph BuildCodeBlock(CodeBlock code)
    {
        string text = ExtractCode(code);
        var p = new Paragraph(new Run(text))
        {
            FontFamily = new FontFamily("Consolas"),
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)),
            Padding = new Thickness(10),
        };
        p.Tag = "codeblock";
        return p;
    }

    private static Table BuildTable(Markdig.Extensions.Tables.Table table)
    {
        var wpfTable = new Table { CellSpacing = 0 };
        var rowGroup = new TableRowGroup();
        wpfTable.RowGroups.Add(rowGroup);

        foreach (var rowObj in table)
        {
            if (rowObj is not Markdig.Extensions.Tables.TableRow row) continue;
            var wpfRow = new TableRow();
            foreach (var cellObj in row)
            {
                if (cellObj is not Markdig.Extensions.Tables.TableCell cell) continue;
                var para = new Paragraph();
                if (cell.Count > 0 && cell[0] is ParagraphBlock pb)
                    AddInlines(para.Inlines, pb.Inline);
                var wpfCell = new TableCell(para)
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 3, 6, 3),
                };
                if (row.IsHeader) para.FontWeight = FontWeights.Bold;
                wpfRow.Cells.Add(wpfCell);
            }
            rowGroup.Rows.Add(wpfRow);
        }

        return wpfTable;
    }

    private static System.Windows.Documents.Block BuildRule()
    {
        // A thin bordered paragraph reads as a horizontal rule.
        var p = new Paragraph
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 8, 0, 8),
        };
        p.Tag = "hr";
        return p;
    }

    private static void AddInlines(InlineCollection target, ContainerInline? container)
    {
        if (container == null) return;
        foreach (MdInline inline in container)
            AddInline(target, inline);
    }

    private static void AddInline(InlineCollection target, MdInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                var span = BuildEmphasis(emphasis);
                target.Add(span);
                break;

            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)),
                });
                break;

            case LinkInline link when link.IsImage:
                // Inline image: render as a placeholder Run carrying the url/alt.
                target.Add(new Run($"[image: {link.Url}]") { Foreground = Brushes.SteelBlue, Tag = "img:" + link.Url });
                break;

            case LinkInline link:
                var hyperlink = new Hyperlink { NavigateUri = SafeUri(link.Url) };
                AddInlines(hyperlink.Inlines, link);
                if (hyperlink.Inlines.Count == 0)
                    hyperlink.Inlines.Add(new Run(link.Url ?? string.Empty));
                target.Add(hyperlink);
                break;

            case LineBreakInline lineBreak:
                target.Add(lineBreak.IsHard ? new LineBreak() : new Run(" "));
                break;

            case ContainerInline nested:
                AddInlines(target, nested);
                break;
        }
    }

    private static Span BuildEmphasis(EmphasisInline emphasis)
    {
        Span span = emphasis.DelimiterChar switch
        {
            '~' when emphasis.DelimiterCount == 1 => WithBaseline(BaselineAlignment.Subscript),
            '^' => WithBaseline(BaselineAlignment.Superscript),
            _ => emphasis.DelimiterCount >= 2 ? new Bold() : new Italic(),
        };
        AddInlines(span.Inlines, emphasis);
        return span;
    }

    private static Span WithBaseline(BaselineAlignment alignment)
    {
        var span = new Span { BaselineAlignment = alignment, FontSize = 11 };
        span.Tag = alignment == BaselineAlignment.Subscript ? "sub" : "sup";
        return span;
    }

    private static string ExtractCode(CodeBlock code)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in code.Lines.Lines)
        {
            if (line.Slice.Text == null) continue;
            sb.AppendLine(line.Slice.ToString());
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static System.Uri? SafeUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return System.Uri.TryCreate(url, System.UriKind.RelativeOrAbsolute, out var uri) ? uri : null;
    }
}
