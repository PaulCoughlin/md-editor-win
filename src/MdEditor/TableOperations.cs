using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MdEditor;

/// <summary>
/// Pure structural edits on a WPF <see cref="Table"/>: build an empty table, insert
/// or delete rows and columns, toggle the header row. Kept separate from the window
/// so the logic is unit-testable (build → edit → serialize → assert markdown).
///
/// The header row is marked with <c>TableRow.Tag = "header"</c>; its cells are bold.
/// Markdown allows exactly one header, and it must be the first row, so the header is
/// always row 0.
/// </summary>
public static class TableOperations
{
    private const string HeaderTag = "header";

    private static readonly Brush Border = Brushes.LightGray;

    public static Table Build(int rows, int columns)
    {
        rows = Math.Clamp(rows, 1, 100);
        columns = Math.Clamp(columns, 1, 100);

        var table = new Table { CellSpacing = 0 };
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        for (int r = 0; r < rows; r++)
        {
            var row = new TableRow();
            bool header = r == 0;
            if (header) row.Tag = HeaderTag;
            for (int c = 0; c < columns; c++)
                row.Cells.Add(NewCell(header));
            group.Rows.Add(row);
        }
        return table;
    }

    public static TableCell NewCell(bool header)
    {
        var para = new Paragraph();
        if (header) para.FontWeight = FontWeights.Bold;
        return new TableCell(para)
        {
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3, 6, 3),
        };
    }

    private static TableRowGroup Group(Table table) => table.RowGroups[0];

    private static int ColumnCount(Table table)
    {
        var rows = Group(table).Rows;
        return rows.Count == 0 ? 0 : rows[0].Cells.Count;
    }

    /// <summary>Inserts a new (non-header) row at <paramref name="index"/>.</summary>
    public static void InsertRow(Table table, int index)
    {
        var rows = Group(table).Rows;
        index = Math.Clamp(index, 0, rows.Count);
        int cols = ColumnCount(table);

        var row = new TableRow();
        for (int c = 0; c < cols; c++)
            row.Cells.Add(NewCell(header: false));

        if (index >= rows.Count) rows.Add(row);
        else rows.Insert(index, row);

        // Row 0 must stay the header; if a body row was inserted at 0, re-mark row 0.
        NormalizeHeader(table);
    }

    /// <summary>Deletes the row at <paramref name="index"/>, keeping at least one row.</summary>
    public static bool DeleteRow(Table table, int index)
    {
        var rows = Group(table).Rows;
        if (rows.Count <= 1 || index < 0 || index >= rows.Count) return false;
        rows.RemoveAt(index);
        NormalizeHeader(table);
        return true;
    }

    /// <summary>Inserts an empty column at <paramref name="index"/> across every row.</summary>
    public static void InsertColumn(Table table, int index)
    {
        int cols = ColumnCount(table);
        index = Math.Clamp(index, 0, cols);
        foreach (TableRow row in Group(table).Rows)
        {
            bool header = ReferenceEquals(row, HeaderRow(table));
            var cell = NewCell(header);
            if (index >= row.Cells.Count) row.Cells.Add(cell);
            else row.Cells.Insert(index, cell);
        }
    }

    /// <summary>Deletes the column at <paramref name="index"/>, keeping at least one column.</summary>
    public static bool DeleteColumn(Table table, int index)
    {
        int cols = ColumnCount(table);
        if (cols <= 1 || index < 0 || index >= cols) return false;
        foreach (TableRow row in Group(table).Rows)
            if (index < row.Cells.Count)
                row.Cells.RemoveAt(index);
        return true;
    }

    /// <summary>Turns the header row on (bold row 0) or off (plain row 0).</summary>
    public static void ToggleHeader(Table table)
    {
        var rows = Group(table).Rows;
        if (rows.Count == 0) return;
        var first = rows[0];
        bool isHeader = (first.Tag as string) == HeaderTag;
        SetRowHeader(first, !isHeader);
    }

    private static TableRow? HeaderRow(Table table)
    {
        foreach (TableRow row in Group(table).Rows)
            if ((row.Tag as string) == HeaderTag) return row;
        return null;
    }

    /// <summary>Ensures exactly row 0 carries the header tag when a header exists.</summary>
    private static void NormalizeHeader(Table table)
    {
        var rows = Group(table).Rows;
        if (rows.Count == 0) return;
        bool anyHeader = false;
        foreach (TableRow row in rows)
            if ((row.Tag as string) == HeaderTag) { anyHeader = true; break; }
        if (!anyHeader) return; // headerless table stays headerless

        for (int i = 0; i < rows.Count; i++)
            SetRowHeader(rows[i], i == 0);
    }

    private static void SetRowHeader(TableRow row, bool header)
    {
        row.Tag = header ? HeaderTag : null;
        foreach (TableCell cell in row.Cells)
            foreach (Block b in cell.Blocks)
                if (b is Paragraph p)
                    p.FontWeight = header ? FontWeights.Bold : FontWeights.Normal;
    }
}
