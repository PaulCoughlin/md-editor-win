using System.Threading;
using System.Windows.Documents;
using MdEditor.Markdown;

namespace MdEditor.Tests;

/// <summary>
/// The serializer is the substance of the app, so these tests exercise the full
/// round trip: markdown -> FlowDocument -> markdown. WPF document objects must be
/// created on an STA thread, so each assertion runs inside <see cref="Sta"/>.
/// </summary>
public class RoundTripTests
{
    private static string RoundTrip(string markdown)
    {
        FlowDocument doc = MarkdownToFlowDocument.Convert(markdown);
        return FlowDocumentToMarkdown.Serialize(doc).TrimEnd('\n');
    }

    /// <summary>Runs an assertion body on a dedicated STA thread and rethrows failures.</summary>
    private static void Sta(Action body)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) throw captured;
    }

    [Fact]
    public void Headings_RoundTrip() => Sta(() =>
    {
        Assert.Equal("# Title", RoundTrip("# Title"));
        Assert.Equal("### Sub", RoundTrip("### Sub"));
    });

    [Fact]
    public void Paragraph_RoundTrip() => Sta(() =>
        Assert.Equal("Just some text.", RoundTrip("Just some text.")));

    [Fact]
    public void Bold_And_Italic_RoundTrip() => Sta(() =>
    {
        Assert.Equal("**bold**", RoundTrip("**bold**"));
        Assert.Equal("*italic*", RoundTrip("*italic*"));
    });

    [Fact]
    public void InlineCode_RoundTrip() => Sta(() =>
        Assert.Equal("Use `code` here.", RoundTrip("Use `code` here.")));

    [Fact]
    public void BulletList_RoundTrip() => Sta(() =>
    {
        string result = RoundTrip("- one\n- two");
        Assert.Contains("- one", result);
        Assert.Contains("- two", result);
    });

    [Fact]
    public void OrderedList_RoundTrip() => Sta(() =>
    {
        string result = RoundTrip("1. first\n2. second");
        Assert.Contains("1. first", result);
        Assert.Contains("2. second", result);
    });

    [Fact]
    public void Blockquote_RoundTrip() => Sta(() =>
        Assert.Contains("> quoted", RoundTrip("> quoted")));

    [Fact]
    public void HorizontalRule_RoundTrip() => Sta(() =>
        Assert.Contains("---", RoundTrip("above\n\n---\n\nbelow")));

    [Fact]
    public void Link_RoundTrip() => Sta(() =>
        Assert.Equal("[text](https://example.com/)", RoundTrip("[text](https://example.com/)")));

    [Fact]
    public void CodeBlock_RoundTrip() => Sta(() =>
    {
        string result = RoundTrip("```\nline1\nline2\n```");
        Assert.Contains("```", result);
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
    });

    [Fact]
    public void Table_RoundTrip() => Sta(() =>
    {
        string result = RoundTrip("| A | B |\n| --- | --- |\n| 1 | 2 |");
        Assert.Contains("| A | B |", result);
        Assert.Contains("| 1 | 2 |", result);
    });

    [Fact]
    public void Empty_Document_Produces_Single_Newline() => Sta(() =>
        Assert.Equal("", RoundTrip("")));
}
