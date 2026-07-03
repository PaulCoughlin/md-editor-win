using System.Threading;
using System.Windows.Documents;
using MdEditor;
using MdEditor.Markdown;

namespace MdEditor.Tests;

/// <summary>
/// Table structure edits, verified through the serializer: build → edit → emit
/// markdown → assert. WPF document objects need an STA thread.
/// </summary>
public class TableOperationsTests
{
    private static void Sta(Action body)
    {
        Exception? captured = null;
        var t = new Thread(() => { try { body(); } catch (Exception ex) { captured = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (captured is not null) throw captured;
    }

    private static string Serialize(Table table)
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(table);
        return FlowDocumentToMarkdown.Serialize(doc).TrimEnd('\n');
    }

    private static int Lines(string md) => md.Split('\n').Length;

    [Fact]
    public void Build_2x2_Has_Header_Separator_And_One_Body_Row() => Sta(() =>
    {
        var md = Serialize(TableOperations.Build(2, 2));
        var lines = md.Split('\n');
        Assert.Equal(3, lines.Length);          // header, separator, 1 body row
        Assert.Equal("| --- | --- |", lines[1]);
    });

    [Fact]
    public void InsertRow_Adds_A_Body_Row() => Sta(() =>
    {
        var table = TableOperations.Build(2, 2);
        int before = Lines(Serialize(table));
        TableOperations.InsertRow(table, 2);   // append at end
        Assert.Equal(before + 1, Lines(Serialize(table)));
    });

    [Fact]
    public void InsertColumn_Widens_Every_Row() => Sta(() =>
    {
        var table = TableOperations.Build(2, 2);
        TableOperations.InsertColumn(table, 1);
        foreach (var line in Serialize(table).Split('\n'))
            Assert.Equal(3, line.Count(ch => ch == '|') - 1); // 3 cols => 4 pipes
    });

    [Fact]
    public void DeleteRow_Keeps_At_Least_One_Row() => Sta(() =>
    {
        var table = TableOperations.Build(1, 2);
        Assert.False(TableOperations.DeleteRow(table, 0)); // refuses last row
    });

    [Fact]
    public void DeleteColumn_Keeps_At_Least_One_Column() => Sta(() =>
    {
        var table = TableOperations.Build(2, 1);
        Assert.False(TableOperations.DeleteColumn(table, 0)); // refuses last column
    });

    [Fact]
    public void DeleteColumn_Removes_Cell_From_Every_Row() => Sta(() =>
    {
        var table = TableOperations.Build(3, 3);
        Assert.True(TableOperations.DeleteColumn(table, 0));
        foreach (var line in Serialize(table).Split('\n'))
            Assert.Equal(2, line.Count(ch => ch == '|') - 1); // 2 cols
    });

    [Fact]
    public void ToggleHeader_Off_Then_On_RoundTrips_Cell_Text() => Sta(() =>
    {
        var table = TableOperations.Build(2, 2);
        // Off then on must not corrupt structure; separator stays after row 0.
        TableOperations.ToggleHeader(table);
        TableOperations.ToggleHeader(table);
        var lines = Serialize(table).Split('\n');
        Assert.Equal("| --- | --- |", lines[1]);
    });
}
