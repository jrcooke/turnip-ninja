// Based on Numerical Recipes code
// http://www.it.uom.gr/teaching/linearalgebra/NumericalRecipiesInC/c3-6.pdf

using System.Collections.Generic;
using System.Linq;

namespace MountainViewDesktop.Interpolation
{
    public class TwoDInterpolator
    {
        private readonly InterpolatonType type;
        private readonly double[] xs;
        private readonly bool xsReversed;
        private readonly double[] ys;
        private readonly bool ysReversed;
        private readonly double[][] rawValues;
        private readonly OneDInterpolator[] values;
        private Dictionary<double, OneDInterpolator> cachedValues = new Dictionary<double, OneDInterpolator>();

        public TwoDInterpolator(double[] xs, double[] ys, double[][] values, InterpolatonType type)
        {
            this.type = type;
            this.xs = xs;
            this.ys = ys;
            if (type == InterpolatonType.Nearest)
            {
                this.rawValues = values;
                if (xs[0] > xs[1])
                {
                    xsReversed = true;
                    this.xs = xs.Reverse().ToArray();
                }

                if (ys[0] > ys[1])
                {
                    ysReversed = true;
                    this.ys = ys.Reverse().ToArray();
                }
            }
            else
            {
                this.values = new OneDInterpolator[xs.Length];
                for (int j = 0; j < xs.Length; j++)
                {
                    this.values[j] = new OneDInterpolator(ys, values[j], type);
                }
            }
        }

        public bool TryGetValue(double x, double y, out double z)
        {
            if (type == InterpolatonType.Nearest)
            {
                if (GetNearestIndex(xs, x, xsReversed, out int i) && GetNearestIndex(ys, y, ysReversed, out int j))
                {
                    z = rawValues[i][j];
                    return true;
                }

                z = 0;
                return false;
            }
            else
            {
                if (!cachedValues.TryGetValue(y, out OneDInterpolator inter))
                {
                    if (!this.values[0].GetKLoHi(y, out int klo, out int khi))
                    {
                        z = 0;
                        return false;
                    }

                    double[] interpValues = new double[xs.Length];
                    for (int j = 0; j < xs.Length; j++)
                    {
                        interpValues[j] = this.values[j].GetValue(y, klo, khi);
                    }

                    inter = new OneDInterpolator(xs, interpValues, type);
                    cachedValues.Add(y, inter);
                }

                return inter.TryGetValue(x, out z);
            }
        }

        public static bool GetNearestIndex(double[] xa, double x, bool reversed, out int i)
        {
            int n = xa.Length;
            if (x < xa[0] || x > xa[n - 1])
            {
                i = 0;
                return false;
            }

            int klo = 0;
            int khi = n - 1;
            while (khi - klo > 1)
            {
                int k = (khi + klo) / 2;
                if (xa[k] > x)
                {
                    khi = k;
                }
                else
                {
                    klo = k;
                }
            }

            i = ((x - xa[klo]) < (xa[khi] - x)) ? klo : khi;
            if (reversed)
            {
                i = n - 1 - i;
            }

            return true;
        }
    }
}
