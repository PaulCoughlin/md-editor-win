using System.Windows;

namespace MdEditor;

/// <summary>Asks for a table size. On OK, <see cref="Rows"/>/<see cref="Columns"/> hold the choice.</summary>
public partial class InsertTableWindow : Window
{
    public int Rows { get; private set; }
    public int Columns { get; private set; }

    public InsertTableWindow()
    {
        InitializeComponent();
        RowsBox.Focus();
        RowsBox.SelectAll();
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RowsBox.Text, out int rows) || rows is < 1 or > 20)
        {
            MessageBox.Show(this, "Rows must be between 1 and 20.", "Insert Table",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!int.TryParse(ColsBox.Text, out int cols) || cols is < 1 or > 20)
        {
            MessageBox.Show(this, "Columns must be between 1 and 20.", "Insert Table",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Rows = rows;
        Columns = cols;
        DialogResult = true;
    }
}
