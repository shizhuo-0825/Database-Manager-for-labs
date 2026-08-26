using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace DXApplication2.Common.IO
{
    /// <summary>
    /// 读取 .asc 矩阵数据。
    /// 格式:
    /// - 第一列 = 序号(丢弃)
    /// - 第二列开始 = 数据
    /// - 行内 tab 分割
    /// - 行间 \n 分割
    /// </summary>
    public static class AscMatrixReader
    {
        /// <summary>
        /// 同步读取。返回 double[rows, cols],cols 不包含序号列。
        /// </summary>
        public static double[,] Read(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($".asc file not found: {filePath}");

            var lines = File.ReadAllLines(filePath);
            return ParseLines(lines);
        }

        /// <summary>异步读取</summary>
        public static async Task<double[,]> ReadAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($".asc file not found: {filePath}");

            var lines = await File.ReadAllLinesAsync(filePath);
            return ParseLines(lines);
        }

        private static double[,] ParseLines(string[] lines)
        {
            var validRows = new List<double[]>();
            int? expectedCols = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;  // 至少要有序号 + 1 个数据

                // 跳过第一列(序号),解析后面的
                var row = new double[parts.Length - 1];
                bool allParsed = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out row[i - 1]))
                    {
                        allParsed = false;
                        break;
                    }
                }
                if (!allParsed) continue;

                if (expectedCols == null) expectedCols = row.Length;
                else if (row.Length != expectedCols) continue;   // 忽略列数不一致的行

                validRows.Add(row);
            }

            if (validRows.Count == 0)
                throw new InvalidDataException("No valid rows found in .asc file.");

            int rows = validRows.Count;
            int cols = expectedCols!.Value;
            var matrix = new double[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    matrix[i, j] = validRows[i][j];

            return matrix;
        }

        /// <summary>
        /// 计算矩阵的 min / max(供 colorbar 范围用),忽略 NaN 和 Inf
        /// </summary>
        public static (double Min, double Max) GetRange(double[,] matrix)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            if (min == double.MaxValue) return (0, 1);
            return (min, max);
        }
    }
}