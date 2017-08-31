// Based now only on Numerical Recipies code
// http://www.it.uom.gr/teaching/linearalgebra/NumericalRecipiesInC/c3-6.pdf

using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.Base
{
    public class TwoDInterpolatorLinear
    {
        private readonly double[] xs;
        private readonly double[] ys;
        private readonly SimpleInterpolator[] values;
        private Dictionary<double, SimpleInterpolator> cachedValues = new Dictionary<double, SimpleInterpolator>();

        public TwoDInterpolatorLinear(double[] xs, double[] ys, double[][] values)
        {
            this.xs = xs;
            this.ys = ys;
            this.values = new SimpleInterpolator[xs.Length];
            for (int j = 0; j < xs.Length; j++)
            {
                this.values[j] = new SimpleInterpolator(ys, values[j]);
            }
        }

        public bool TryGetValue(double x, double y, out double z)
        {
            if (!cachedValues.TryGetValue(y, out SimpleInterpolator inter))
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

                inter = new SimpleInterpolator(xs, interpValues);
                cachedValues.Add(y, inter);
            }

            return inter.TryGetValue(x, out z);
        }

        //internal TwoDInterpolator GetInterpolatorForLine(double x1, double y1, double x2, double y2)
        //{
        //    int i1 = NRCubicSplineInterpolator.LookupIndex(xs, x1);
        //    int i2 = NRCubicSplineInterpolator.LookupIndex(xs, x2);
        //    int j1 = NRCubicSplineInterpolator.LookupIndex(ys, y1);
        //    int j2 = NRCubicSplineInterpolator.LookupIndex(ys, y2);

        //    // Start with the simplest thing that works
        //    return this;

        //    //// The best interpolator will be from points with the most hits.
        //    //int deltaI = Math.Abs(i1 - i2);
        //    //int deltaJ = Math.Abs(j1 - j2);
        //    //if (deltaI < deltaJ)
        //    //{

        //    //}
        //    //else
        //    //{

        //    //}

        //    //throw new NotImplementedException();
        //}
    }


    public class SimpleInterpolator
    {
        private int n;
        private double[] xa;
        private double[] ya;

        public SimpleInterpolator(double[] x, double[] y)
        {
            if (x[0] < x[x.Length - 1])
            {
                xa = x.ToArray();
                ya = y.ToArray();
            }
            else
            {
                xa = x.Reverse().ToArray();
                ya = y.Reverse().ToArray();
            }

            n = xa.Length;
        }

        public bool TryGetValue(double x, out double y)
        {
            if (GetKLoHi(x, out int klo, out int khi))
            {
                y = GetValue(x, klo, khi);
                return true;
            }

            y = 0;
            return false;
        }

        public bool GetKLoHi(double x, out int klo, out int khi)
        {
            if (x < xa[0] || x > xa[n - 1])
            {
                klo = 0;
                khi = 0;
                return false;
            }

            klo = 0;
            khi = n - 1;
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

            return true;
        }

        public double GetValue(double x, int klo, int khi)
        {
            // klo and khi now bracket the input value of x.
            var h = xa[khi] - xa[klo];
            var a = (xa[khi] - x) / h;
            var b = (x - xa[klo]) / h;
            return a * ya[klo] + b * ya[khi];
        }

        public static void Test()
        {
            int n = 10;
            double y0 = 5.0;

            double[] xs = new double[n];
            double[] ys = new double[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = i;
                ys[i] = y0;
            }

            var t1 = new SimpleInterpolator(xs, ys);
            bool success = t1.TryGetValue(n / 2.0 + 0.5, out double y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 1.0e-10) throw new InvalidOperationException();

            success = t1.TryGetValue(0.0, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 1.0e-10) throw new InvalidOperationException();

            success = t1.TryGetValue((n - 1) * 1.0, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 1.0e-10) throw new InvalidOperationException();

            success = t1.TryGetValue(-0.01, out y);
            if (success) throw new InvalidOperationException();
            if (Math.Abs(y) > 1.0e-10) throw new InvalidOperationException();

            success = t1.TryGetValue((n - 1) * 1.0 + 0.01, out y);
            if (success) throw new InvalidOperationException();
            if (Math.Abs(y) > 1.0e-10) throw new InvalidOperationException();

            Func<double, double> cubic = (x_) => (x_ - 2) * (x_ + 3) * (x_ - 1);

            for (int i = 0; i < n; i++)
            {
                xs[i] = i;
                ys[i] = cubic(i);
            }

            t1 = new SimpleInterpolator(xs, ys);

            double x = n / 2.0 + 0.5;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) >5 ) throw new InvalidOperationException();

            x = 0.05;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 0.5) throw new InvalidOperationException();

            x = (n - 1) * 1.0 - 0.5;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 7) throw new InvalidOperationException();

            x = -0.01;
            success = t1.TryGetValue(x, out y);
            if (success) throw new InvalidOperationException();
            if (Math.Abs(y) > 1.0e-10) throw new InvalidOperationException();

            success = t1.TryGetValue((n - 1) * 1.0 + 0.01, out y);
            if (success) throw new InvalidOperationException();
            if (Math.Abs(y) > 1.0e-10) throw new InvalidOperationException();
        }
    }
}