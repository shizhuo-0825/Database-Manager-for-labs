using DevExpress.Mvvm;
using DevExpress.Mvvm.ModuleInjection;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core;
using DXApplication2.Common;
using DXApplication2.Common.Data;
using DXApplication2.Main.Properties;
using DXApplication2.Main.ViewModels;
using DXApplication2.Main.Views;
using DXApplication2.Modules.ViewModels;
using DXApplication2.Modules.Views;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AppModules = DXApplication2.Common.Modules;

namespace DXApplication2.Main
{
    public partial class App : Application
    {
        public App()
        {
            CompatibilitySettings.UseLightweightThemes = true;
            ApplicationThemeHelper.UpdateApplicationThemeName();
            SplashScreenManager.CreateThemed().ShowOnStartup();

        }
        protected override void OnExit(ExitEventArgs e)
        {
            ApplicationThemeHelper.SaveApplicationThemeName();
            base.OnExit(e);
        }
        async void OnApplicationStartup(object sender, StartupEventArgs e)
        {
            await Bootstrapper.RunAsync();
        }
    }
}