using System.Windows.Controls;
using Brushes = System.Windows.Media.Brushes;

namespace TaskAzure.Windows;

internal static class DataGridHelper
{
    public static void ApplyDefaultColumnStyle(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is DataGridTextColumn col)
        {
            col.Foreground = Brushes.LightCyan;
            col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        }
    }
}
