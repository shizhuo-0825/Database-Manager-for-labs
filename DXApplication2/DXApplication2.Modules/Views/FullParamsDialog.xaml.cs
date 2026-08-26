using DXApplication2.Common.Models;
using System.Text.Json;
using System.Windows;

namespace DXApplication2.Modules.Views
{
    public partial class FullParamsDialog : Window
    {
        public FullParamsDialog(DataRecord record)
        {
            InitializeComponent();
            HeaderText.Text = $"Record #{record.Id} — {record.RecordName}";

            // 尝试格式化 JSON(缩进易读)
            try
            {
                using var doc = JsonDocument.Parse(record.ExptParams ?? "{}");
                JsonBox.Text = JsonSerializer.Serialize(doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                JsonBox.Text = record.ExptParams ?? "(empty)";
            }
        }

        private void CopyClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(JsonBox.Text);
        }
    }
}