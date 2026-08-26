using DevExpress.Xpf.Core;
using DXApplication2.Main.ViewModels;


namespace DXApplication2.Main.Views
{
    /// <summary>
    /// ExperimentParamsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ExperimentParamsWindow : ThemedWindow
    {
        public ExperimentParamsWindow()
        {
            InitializeComponent();
            DataContext = new ExperimentParamsViewModel();
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
