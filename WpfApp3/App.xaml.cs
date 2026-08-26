using DevExpress.Xpf.Core;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace WpfApp3
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ApplicationThemeHelper.ApplicationThemeName = Theme.Office2019ColorfulName;
            base.OnStartup(e);
        }
    }
}