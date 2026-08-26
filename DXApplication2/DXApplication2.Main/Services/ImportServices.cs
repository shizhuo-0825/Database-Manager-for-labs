using Microsoft.EntityFrameworkCore;
using System.Linq;
using DXApplication2.Common.Data;
using System.Threading.Tasks;
using System;
using static DevExpress.Utils.Svg.CommonSvgImages;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using DXApplication2.Common.Models;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Diagnostics;
using System.Globalization;

namespace DXApplication2.Main.Services
{
    public class ImportService
    {


        public async Task FullRebuildImportAsync()
        {
            using var db = new AppDbContext();

            var folderPath = await db.DataFolders
               .OrderByDescending(f => f.Id)
               .Select(f => f.FolderPath)
               .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(folderPath))
                return;

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            try
            {
                // 找当前Folder下面所有Group
                var groups = await db.DataGroups
                    .Where(g => g.CsvPath.StartsWith(folderPath))
                    .ToListAsync();
                

                if (groups.Count > 0)
                {
                    var groupIds = groups
                        .Select(g => g.Id)
                        .ToList();
                    var records = await db.DataRecords
                           .Where(x => groupIds.Contains(x.DataGroupId))
                           .ToListAsync();

                    var recordIds = records
                        .Select(r => r.Id)
                        .ToList();


                    // 删除 CollectionMember
                    var members = await db.CollectionMembers
                        .Where(x => groupIds.Contains(x.DataGroupId))
                        .ToListAsync();

                    db.CollectionMembers.RemoveRange(members);



                    // 删除 ExperimentType关联
                    var types = await db.GroupExperimentTypes
                        .Where(x => groupIds.Contains(x.DataGroupId))
                        .ToListAsync();

                    db.GroupExperimentTypes.RemoveRange(types);



                    // 删除 Analysis
                   

                    var analyses = await db.AnalysisParamss
                    .Where(a => recordIds.Contains(a.DataRecordId))
                    .ToListAsync();

                    db.AnalysisParamss.RemoveRange(analyses);
                    db.DataRecords.RemoveRange(records);
                    db.DataGroups.RemoveRange(groups);


                    await db.SaveChangesAsync();
                }


                await transaction.CommitAsync();


                // 清理完成后重新导入
                await IncrementalImportAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task DeleteAllDataAsync()
        {
            using var db = new AppDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                // 逆向依赖顺序删除

                // 1. CollectionMembers(关系表,引用 Collection 和 DataGroup)
                var members = await db.CollectionMembers.ToListAsync();
                db.CollectionMembers.RemoveRange(members);

                // 2. Collections
                var collections = await db.Collections.ToListAsync();
                db.Collections.RemoveRange(collections);

                // 3. GroupExperimentTypes(关系表,引用 DataGroup 和 ExperimentType)
                var groupTypes = await db.GroupExperimentTypes.ToListAsync();
                db.GroupExperimentTypes.RemoveRange(groupTypes);

                // 4. AnalysisParamss(引用 DataRecord)
                var analyses = await db.AnalysisParamss.ToListAsync();
                db.AnalysisParamss.RemoveRange(analyses);

                // 5. DataRecords(引用 DataGroup)
                var records = await db.DataRecords.ToListAsync();
                db.DataRecords.RemoveRange(records);

                // 6. DataGroups
                var groups = await db.DataGroups.ToListAsync();
                db.DataGroups.RemoveRange(groups);

                // 7. DataFolders(不被别的表引用)
                var folders = await db.DataFolders.ToListAsync();
                db.DataFolders.RemoveRange(folders);

                // 注意:不删 ExperimentTypes 和 ExperimentParams(用户配置)

                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private async Task<List<string>> FindDatabaseCsvFilesAsync(string folderPath)
        {
            return await Task.Run(() =>
            {
                return Directory
                    .EnumerateFiles(folderPath, "database.csv", SearchOption.AllDirectories)
                    .ToList();
            });
        }
        private async Task<string> CalculateCsvHashAsync(string csvPath)
        {
            await using var stream = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            using var sha256 = SHA256.Create();

            var hashBytes = await sha256.ComputeHashAsync(stream);

            return Convert.ToHexString(hashBytes);
        }



        private async Task ImportCsvDataAsync(AppDbContext db, DataGroup group, string csvPath)
        {
            using var reader = new StreamReader(csvPath);

            var csvDirectory = Path.GetDirectoryName(csvPath)!;

            var ascFiles = Directory
                .EnumerateFiles(csvDirectory, "*.asc", SearchOption.TopDirectoryOnly)
                .ToList();

            // 读取第一行
            var headerLine = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(headerLine))
                return;

            var headers = headerLine
                .Split(',')
                .Select(x => x.Trim())
                .ToArray();

            var existing = await db.ExperimentParamss
                .Select(x => x.FieldName)
                .ToListAsync();

            var timestampIndex = Array.FindIndex(
                headers,
                h => h.Equals("Timestamp", StringComparison.OrdinalIgnoreCase));

            if (timestampIndex < 0)
                return;

            foreach (var field in headers)
            {
                if (existing.Contains(field, StringComparer.OrdinalIgnoreCase))
                    continue;

                db.ExperimentParamss.Add(new ExperimentParams
                {
                    FieldName = field,
                    IsDeprecated = false,
                    FirstSeenAt = DateTime.Now
                });
            }

            // 1. 先把所有数据行读到内存
            var allLines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                allLines.Add(line);
            }

            // 2. 计算补 0 宽度(至少 3 位,让小数据集也好看)
            int totalRows = allLines.Count;
            int width = Math.Max(3, totalRows.ToString().Length);

            // 3. 遍历生成 records
            for (int rowIndex = 0; rowIndex < allLines.Count; rowIndex++)
            {
                var line = allLines[rowIndex];
                var values = line.Split(',');

                var timestamp = values[timestampIndex];

                var parameters = new Dictionary<string, string>();

                for (int i = 0; i < headers.Length; i++)
                {
                    if (i == timestampIndex) continue;
                    if (i < values.Length)
                        parameters[headers[i]] = values[i];
                }

                string imagePath = string.Empty;

                for (int i = 0; i < ascFiles.Count; i++)
                {
                    if (Path.GetFileNameWithoutExtension(ascFiles[i])
                        .Contains(timestamp, StringComparison.OrdinalIgnoreCase))
                    {
                        imagePath = ascFiles[i];
                        ascFiles.RemoveAt(i);
                        break;
                    }
                }

                var record = new DataRecord
                {
                    DataGroup = group,
                    RecordName = timestamp,
                    RowIndex = rowIndex.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0'),
                    ImageFilePath = imagePath,
                    ExptParams = JsonSerializer.Serialize(parameters)
                };

                db.DataRecords.Add(record);
            }
        }

        public async Task IncrementalImportAsync()
        {
            using var db = new AppDbContext();

            var folderPath = await db.DataFolders
               .OrderByDescending(f => f.Id)
               .Select(f => f.FolderPath)
               .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(folderPath))
                return;

            var experimentTypes = await db.ExperimentTypes
                            .AsNoTracking()
                            .ToListAsync();
            var csvFiles = await FindDatabaseCsvFilesAsync(folderPath);
            var recentGroups = await db.DataGroups
                .OrderByDescending(g => g.ExperimentDate)
                .Take(3)
                .ToListAsync();
            foreach (var csv in csvFiles)
            {
                var hash = await CalculateCsvHashAsync(csv);

                Console.WriteLine($"{csv}\n{hash}");// for test

                var matched = recentGroups.FirstOrDefault(g => g.CsvHash == hash);
                if (matched != null)
                {
                    if (matched.CsvPath != csv)
                    {
                        matched.CsvPath = csv;
                        await db.SaveChangesAsync();
                    }
                    continue;
                }

                var oldGroup = await db.DataGroups
                        .FirstOrDefaultAsync(x => x.CsvPath == csv);


                if (oldGroup != null &&
                   oldGroup.CsvHash == hash)
                {
                    continue;
                }

                var directory = Path.GetDirectoryName(csv);

                if (directory == null) break;


                var fields = directory
                    .Split(
                        Path.DirectorySeparatorChar,
                        StringSplitOptions.RemoveEmptyEntries)
                    .SelectMany(x =>
                        x.Split('_',
                            StringSplitOptions.RemoveEmptyEntries))
                    .ToList();


                // 找日期
                int dateIndex = -1;
                DateTime experimentDate = default;

                for (int i = fields.Count - 1; i >= 0; i--)
                {
                    if (Regex.IsMatch(fields[i], @"^\d{8}$") &&
                        DateTime.TryParseExact(
                            fields[i],
                            "yyyyMMdd",
                            null,
                            System.Globalization.DateTimeStyles.None,
                            out experimentDate))
                    {
                        dateIndex = i;
                        break;
                    }
                }


                // 没日期，不导入
                if (dateIndex < 0) continue;
                Debug.WriteLine("========== Fields ==========");

                foreach (var f in fields)
                {
                    Debug.WriteLine($"[{f}]");
                }

                Debug.WriteLine("========== Experiment Types ==========");

                foreach (var t in experimentTypes)
                {
                    Debug.WriteLine($"[{t.Name}]");
                }
                foreach (var t in experimentTypes)
                {
                    foreach (var f in fields)
                    {
                        var equal = string.Equals(
                            f,
                            t.Name,
                            StringComparison.OrdinalIgnoreCase);

                        Debug.WriteLine(
                            $"'{f}' == '{t.Name}' -> {equal}");
                    }
                }
                // 匹配 ExperimentType
                var matchedTypes = experimentTypes
                    .Where(t =>
                        fields.Any(f =>
                            string.Equals(
                                f,
                                t.Name,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                // 日期后面的作为 Material
                var material = string.Join(" ",
                        fields
                            .Skip(dateIndex + 1)
                            .Where(f => !matchedTypes.Any(t =>
                string.Equals(f, t.Name, StringComparison.OrdinalIgnoreCase))));

                var group = new DataGroup
                {
                    Material = material,
                    ExperimentDate = experimentDate,
                    CsvPath = csv,
                    CsvHash = hash
                };

                if (oldGroup != null)
                {
                    // CsvPath 已存在但 CsvHash 不一致：清理旧的 Records / Analyses / Types，
                    // 复用旧 Group 实体并更新其字段和链接
                    var oldRecords = await db.DataRecords
                        .Where(r => r.DataGroupId == oldGroup.Id)
                        .ToListAsync();

                    var oldRecordIds = oldRecords.Select(r => r.Id).ToList();

                    var oldAnalyses = await db.AnalysisParamss
                        .Where(a => oldRecordIds.Contains(a.DataRecordId))
                        .ToListAsync();
                    db.AnalysisParamss.RemoveRange(oldAnalyses);
                    db.DataRecords.RemoveRange(oldRecords);

                    var oldTypes = await db.GroupExperimentTypes
                        .Where(t => t.DataGroupId == oldGroup.Id)
                        .ToListAsync();
                    db.GroupExperimentTypes.RemoveRange(oldTypes);

                    oldGroup.Material = material;
                    oldGroup.ExperimentDate = experimentDate;
                    oldGroup.CsvHash = hash;
                    group = oldGroup;
                }
                else
                {
                    group = new DataGroup
                    {
                        Material = material,
                        ExperimentDate = experimentDate,
                        CsvPath = csv,
                        CsvHash = hash
                    };
                    db.DataGroups.Add(group);
                }
                foreach (var type in matchedTypes)
                {
                    group.GroupExperimentTypes.Add(
                        new GroupExperimentType
                        {
                            ExperimentTypeId = type.Id
                        });
                }
                if (!matchedTypes.Any())
                {
                    var noneType = experimentTypes
                        .First(t => t.Name == "None");

                    group.GroupExperimentTypes.Add(new GroupExperimentType
                    {
                        ExperimentTypeId = noneType.Id
                    });
                }

                await ImportCsvDataAsync(db, group,csv);

            }
            await db.SaveChangesAsync();
        }
    }
}