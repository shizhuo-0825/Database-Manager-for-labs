using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DXApplication2.Common.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<DataFolder> DataFolders { get; set; }
        public DbSet<DataGroup> DataGroups { get; set; }
        public DbSet<DataRecord> DataRecords { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<CollectionMember> CollectionMembers { get; set; }
        public DbSet<GroupExperimentType> GroupExperimentTypes { get; set; }
        public DbSet<AnalysisParams> AnalysisParamss { get; set; }
        public DbSet<ExperimentType> ExperimentTypes { get; set; } = null!;
        public DbSet<ExperimentParams> ExperimentParamss { get; set; } = null!;

        /// <summary>启用 WAL 模式(允许并发读写,减少 database locked)。程序启动时调用一次即可。</summary>
        public static async Task EnableWalModeAsync()
        {
            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync();
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            await cmd.ExecuteNonQueryAsync();
            await db.Database.CloseConnectionAsync();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SHGManager");
            Directory.CreateDirectory(dbDir);
            var dbPath = Path.Combine(dbDir, "shg_data.db");

            // Cache=Shared + WAL 模式:允许并发读写,减少 database locked
            var connectionString = $"Data Source={dbPath};Cache=Shared";
            options.UseSqlite(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DataGroup>().HasKey(x => x.Id);
            try
            {
                base.OnModelCreating(modelBuilder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }

            modelBuilder.Entity<ExperimentType>()
                .HasIndex(e => e.Name)
                .IsUnique();
            modelBuilder.Entity<ExperimentParams>()
                .HasIndex(e => e.FieldName)
                .IsUnique();
            modelBuilder.Entity<ExperimentParams>()
                .HasOne(p => p.ExperimentType)
                .WithMany()
                .HasForeignKey(p => p.ExperimentTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DataFolder>()
                .HasIndex(f => f.FolderPath)
                .IsUnique();
            foreach (var e in modelBuilder.Model.GetEntityTypes())
            {
                Debug.WriteLine($"{e.ClrType.FullName}");
            }
        }

    }
}