using DevExpress.Xpf.Core;
using DXApplication2.Main.ViewModels;

namespace DXApplication2.Main.Views
{
    public partial class ExperimentTypesWindow : ThemedWindow
    {
        public ExperimentTypesWindow()
        {
            InitializeComponent();
            DataContext = new ExperimentTypesViewModel();
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}