using DevExpress.Mvvm;
using DevExpress.Mvvm.ModuleInjection;
using DevExpress.Mvvm.UI;
using DXApplication2.Common;
using DXApplication2.Common.Data;
using DXApplication2.Main.Properties;
using DXApplication2.Main.ViewModels;
using DXApplication2.Main.Views;
using DXApplication2.Modules.ViewModels;
using DXApplication2.Modules.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppModules = DXApplication2.Common.Modules;

namespace DXApplication2.Main
{
    public class Bootstrapper
    {
        public static Bootstrapper Default { get; protected set; }
        public static async Task RunAsync()
        {
            Default = new Bootstrapper();
            await Default.RunCoreAsync();
        }
        protected Bootstrapper() { }

        const string StateVersion = "1.0";
        private static async Task EnsureSchemaAsync(AppDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            // 1) 建表:把整个 CREATE 脚本按语句拆开逐条执行,已存在的表报错也没关系
            var script = db.Database.GenerateCreateScript();
            foreach (var stmt in SplitStatements(script))
            {
                if (string.IsNullOrWhiteSpace(stmt)) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = stmt;
                try { await cmd.ExecuteNonQueryAsync(); }
                catch (Microsoft.Data.Sqlite.SqliteException) { /* already exists 之类,忽略 */ }
            }

            // 2) 补列:对每张表比对期望列 vs 实际列,缺的 ALTER ADD
            var relationalModel = db.Model.GetRelationalModel();
            foreach (var table in relationalModel.Tables)
            {
                using var cols = conn.CreateCommand();
                cols.CommandText = $"PRAGMA table_info(\"{table.Name}\");";
                var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cols.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingCols.Add(reader.GetString(1));
                }
                if (existingCols.Count == 0) continue;

                foreach (var col in table.Columns)
                {
                    if (existingCols.Contains(col.Name)) continue;
                    var nullable = col.IsNullable ? "" : " NOT NULL DEFAULT " + DefaultLiteral(col);
                    using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE \"{table.Name}\" ADD COLUMN \"{col.Name}\" {col.StoreType}{nullable};";
                    try { await alter.ExecuteNonQueryAsync(); }
                    catch (Microsoft.Data.Sqlite.SqliteException) { }
                }
            }
        }

        private static IEnumerable<string> SplitStatements(string script)
        {
            var sb = new StringBuilder();
            foreach (var line in script.Split('\n'))
            {
                sb.AppendLine(line);
                if (line.TrimEnd().EndsWith(";"))
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static string DefaultLiteral(IColumn col)
        {
            // 简单版:按类型给个安全默认值
            var t = col.StoreType.ToUpperInvariant();
            if (t.Contains("INT")) return "0";
            if (t.Contains("REAL") || t.Contains("FLOAT") || t.Contains("DOUBLE")) return "0";
            if (t.Contains("BLOB")) return "x''";
            return "''"; // TEXT 兜底
        }
        public virtual async Task RunCoreAsync()
        {
            using var db = new AppDbContext();
            await EnsureSchemaAsync(db);
            System.Diagnostics.Debug.WriteLine(db.Database.GetConnectionString());
            ConfigureTypeLocators();
            RegisterModules();

            if (!RestoreState())
                InjectModules();

            ConfigureNavigation();
            ShowMainWindow();
        }

        protected IModuleManager Manager { get { return ModuleManager.DefaultManager; } }
        protected virtual void ConfigureTypeLocators()
        {
            var mainAssembly = typeof(MainViewModel).Assembly;
            var modulesAssembly = typeof(GroupModuleViewModel).Assembly;
            var assemblies = new[] { mainAssembly, modulesAssembly };
            ViewModelLocator.Default = new ViewModelLocator(assemblies);
            ViewLocator.Default = new ViewLocator(assemblies);
        }
        protected virtual void RegisterModules()
        {
            Manager.GetRegion(Regions.Documents).VisualSerializationMode = VisualSerializationMode.PerKey;
            Manager.Register(Regions.MainWindow, new Module(AppModules.Main, MainViewModel.Create, typeof(MainView)));
            Manager.Register(Regions.Navigation, new Module(AppModules.Group, () => new NavigationItem("Group")));
            Manager.Register(Regions.Navigation, new Module(AppModules.Collection, () => new NavigationItem("Collection")));
            Manager.Register(Regions.Navigation, new Module(AppModules.Records, () => new NavigationItem("Records")));
            Manager.Register(Regions.Documents, new Module(AppModules.PlotModule,() => new PlotModuleViewModel(), typeof(PlotModuleView)));
            Manager.Register(Regions.Documents, new Module(AppModules.PreviewModule,() => new PreviewModuleViewModel(), typeof(PreviewModuleView)));
            Manager.Register(Regions.Documents, new Module(AppModules.Group, () => new GroupModuleViewModel(), typeof(GroupModuleView)));
            Manager.Register(Regions.Documents, new Module(AppModules.Collection, () => new CollectionModuleViewModel(), typeof(CollectionModuleView)));
            Manager.Register(Regions.Documents, new Module(AppModules.Records, () => new RecordsModuleViewModel(), typeof(RecordsModuleView)));
            Manager.Register(Regions.Documents, new Module(AppModules.BackgroundModule, () => new BackgroundModuleViewModel(), typeof(BackgroundModuleView)));
        }
        protected virtual bool RestoreState()
        {
#if !DEBUG
            return false;
            //return Manager.Restore(Settings.Default.LogicalState, Settings.Default.VisualState);
#else
            return false;
#endif
        }
        protected virtual void InjectModules()
        {
            Manager.Inject(Regions.MainWindow, AppModules.Main);
            Manager.Inject(Regions.Navigation, AppModules.Group);
            Manager.Inject(Regions.Navigation, AppModules.Collection);
            //Manager.Inject(Regions.Navigation, AppModules.Records);
        }
        protected virtual void ConfigureNavigation()
        {
            Manager.GetEvents(Regions.Navigation).Navigation += OnNavigation;
            Manager.GetEvents(Regions.Documents).Navigation += OnDocumentsNavigation;
        }
        protected virtual void ShowMainWindow()
        {
            System.Windows.Application.Current.MainWindow = new MainWindow();
            System.Windows.Application.Current.MainWindow.Show();
            System.Windows.Application.Current.MainWindow.Closing += OnClosing;
        }
        void OnNavigation(object sender, NavigationEventArgs e)
        {
            if (e.NewViewModelKey == null) return;
            Manager.InjectOrNavigate(Regions.Documents, e.NewViewModelKey);
        }
        void OnDocumentsNavigation(object sender, NavigationEventArgs e)
        {
            Manager.Navigate(Regions.Navigation, e.NewViewModelKey);
        }
        void OnClosing(object sender, CancelEventArgs e)
        {
            string logicalState;
            string visualState;
            Manager.Save(out logicalState, out visualState);
            Settings.Default.StateVersion = StateVersion;
            Settings.Default.LogicalState = logicalState;
            Settings.Default.VisualState = visualState;
            Settings.Default.Save();
        }
    }
}