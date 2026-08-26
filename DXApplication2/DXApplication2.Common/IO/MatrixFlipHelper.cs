namespace DXApplication2.Common.IO
{
    public static class MatrixFlipHelper
    {
        /// <summary>返回 Y 翻转后的新矩阵(row 0 变成 row N-1)</summary>
        public static double[,] FlipY(double[,] input)
        {
            int rows = input.GetLength(0);
            int cols = input.GetLength(1);
            var result = new double[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i, j] = input[rows - 1 - i, j];
            return result;
        }

        /// <summary>把翻转后坐标系里的 (row_start, row_end) 转回原图坐标系</summary>
        public static (int rowStart, int rowEnd) UnflipRowRange(int flippedRowStart, int flippedRowEnd, int totalRows)
        {
            // 翻转后 row 0 = 原图 row (totalRows-1)
            // flippedRowStart 对应原图 row (totalRows - 1 - flippedRowStart)
            // 翻转后 [rs, re) 区间 → 原图 [totalRows - re, totalRows - rs)
            int origRowStart = totalRows - flippedRowEnd;
            int origRowEnd = totalRows - flippedRowStart;
            return (origRowStart, origRowEnd);
        }
    }
}