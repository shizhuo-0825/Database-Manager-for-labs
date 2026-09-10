using System;

namespace DXApplication2.Common.Plotting
{
    public static class ContourQuantizer
    {
        /// <summary>把矩阵 Z 值量化到 N 档等间隔中点。Smooth 渲染下呈现 Origin 风格色带。</summary>
        public static double[,] QuantizeMatrix(double[,] m, double zMin, double zMax, int levels)
        {
            int ny = m.GetLength(0), nx = m.GetLength(1);
            var q = new double[ny, nx];
            double range = zMax - zMin;
            if (range < 1e-15 || levels < 2) { Array.Copy(m, q, m.Length); return q; }
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                {
                    var v = m[i, j];
                    if (double.IsNaN(v)) { q[i, j] = double.NaN; continue; }
                    double t = (v - zMin) / range;
                    int bin = (int)Math.Floor(t * levels);
                    bin = Math.Clamp(bin, 0, levels - 1);
                    q[i, j] = zMin + (bin + 0.5) / levels * range;
                }
            return q;
        }
    }
}