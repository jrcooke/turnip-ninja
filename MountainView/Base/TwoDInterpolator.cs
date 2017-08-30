// Based on code from Alex Musayev: https://gist.github.com/dreikanter/3526685
// And from http://www.it.uom.gr/teaching/linearalgebra/NumericalRecipiesInC/c3-6.pdf

using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.Base
{
    public class TwoDInterpolator
    {
        private readonly double[] xs;
        private readonly double[] ys;
        private readonly NRCubicSplineInterpolator[] values;
        private Dictionary<double, NRCubicSplineInterpolator> cachedValues = new Dictionary<double, NRCubicSplineInterpolator>();

        public TwoDInterpolator(double[] xs, double[] ys, double[][] values)
        {
            this.xs = xs;
            this.ys = ys;
            this.values = new NRCubicSplineInterpolator[xs.Length];
            for (int j = 0; j < xs.Length; j++)
            {
                this.values[j] = new NRCubicSplineInterpolator(ys, values[j]);
            }
        }

        public bool TryGetValue(double x, double y, out double z)
        {
            NRCubicSplineInterpolator inter = null;
            if (!cachedValues.TryGetValue(y, out inter))
            {
                double[] interpValues = new double[xs.Length];
                for (int j = 0; j < xs.Length; j++)
                {
                    double tmp;
                    if (this.values[j].TryGetValue(y, out tmp))
                    {
                        interpValues[j] = tmp;
                    }
                    else
                    {
                        z = 0;
                        return false;
                    }
                }

                inter = new NRCubicSplineInterpolator(xs, interpValues);
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

        /// <summary>
        /// Spline interpolation class.
        /// </summary>
        //private class SplineInterpolator
        //{
        //    private readonly double[] xs;
        //    private readonly double[] values;
        //    private readonly double[] h;
        //    private readonly double[] a;

        //    /// <param name="nodes">Collection of known points for further interpolation.
        //    /// Should contain at least two items.</param>
        //    public SplineInterpolator(double[] xs, double[] values)
        //    {
        //        var n = xs.Length;
        //        if (n < 3)
        //        {
        //            throw new ArgumentException("At least three point required for interpolation.");
        //        }

        //        this.xs = xs;
        //        this.values = values;
        //        a = new double[n];
        //        h = new double[n];

        //        for (int i = 1; i < n; i++)
        //        {
        //            h[i] = xs[i] - xs[i - 1];
        //        }

        //        var sub = new double[n - 1];
        //        var diag = new double[n - 1];
        //        var sup = new double[n - 1];

        //        for (int i = 1; i <= n - 2; i++)
        //        {
        //            diag[i] = (h[i + 0] + h[i + 1]) / 3;
        //            sup[i] = h[i + 1] / 6;
        //            sub[i] = h[i + 0] / 6;
        //            a[i] =
        //                (values[i + 1] - values[i + 0]) / h[i + 1] -
        //                (values[i + 0] - values[i - 1]) / h[i + 0];
        //        }

        //        SolveTridiag(sub, diag, sup, a, n - 2);
        //    }

        //    /// <summary>
        //    /// Gets interpolated value for specified argument.
        //    /// </summary>
        //    /// <param name="x">Argument value for interpolation. Must be within
        //    /// the interval bounded by lowest and highest <see cref="xs"/> values.</param>
        //    public double GetValue(double x)
        //    {
        //        int gap = 0;
        //        var previous = double.MinValue;

        //        // At the end of this iteration, "gap" will contain the index of the interval
        //        // between two known values, which contains the unknown z, and "previous" will
        //        // contain the biggest z value among the known samples, left of the unknown z
        //        for (int i = 0; i < xs.Length; i++)
        //        {
        //            if (xs[i] < x && xs[i] > previous)
        //            {
        //                previous = xs[i];
        //                gap = i + 1;
        //            }
        //        }

        //        var x1 = x - previous;
        //        var x2 = h[gap] - x1;

        //        return (
        //                (-a[gap - 1] / 6 * (x2 + h[gap]) * x1 + values[gap - 1]) * x2 +
        //                (-a[gap - 0] / 6 * (x1 + h[gap]) * x2 + values[gap - 0]) * x1
        //            ) / h[gap];
        //    }

        //    public static int LookupIndex(double[] xs, double x)
        //    {
        //        int iLo = 0;
        //        int iHi = xs.Length - 1;
        //        bool isNormal = xs[iLo] < xs[iHi];
        //        while (iHi - iLo > 1)
        //        {
        //            int iMid = iLo + (iHi - iLo) / 2;
        //            if (x < xs[iMid] == isNormal)
        //            {
        //                iHi = iMid;
        //            }
        //            else
        //            {
        //                iLo = iMid;
        //            }
        //        }

        //        return iLo;
        //    }

        //    /// <summary>
        //    /// Solve linear system with tridiagonal n*n matrix "a"
        //    /// using Gaussian elimination without pivoting.
        //    /// </summary>
        //    private static void SolveTridiag(double[] sub, double[] diag, double[] sup, double[] b, int n)
        //    {
        //        for (int i = 2; i <= n; i++)
        //        {
        //            sub[i] = sub[i] / diag[i - 1];
        //            diag[i] = diag[i] - sub[i] * sup[i - 1];
        //            b[i] = b[i] - sub[i] * b[i - 1];
        //        }

        //        b[n] = b[n] / diag[n];
        //        for (int i = n - 1; i >= 1; i--)
        //        {
        //            b[i] = (b[i] - sup[i] * b[i + 1]) / diag[i];
        //        }
        //    }
        //}
    }


    public class NRCubicSplineInterpolator
    {
        private int n;
        private double[] xa;
        private double[] ya;
        private double[] y2a;

        /// <summary>
        /// Given arrays x[0..n-1] and y[0..n-1] containing a tabulated function, i.e., y[i] = f(x[i]), with
        /// x sorted, this routine returns an array y2[1..n] that contains
        /// the second derivatives of the interpolating function at the tabulated points xi.The
        /// routine is signaled to set the corresponding boundary
        /// condition for a natural spline, with zero second derivative on that boundary.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public NRCubicSplineInterpolator(double[] x, double[] y)
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
            y2a = new double[n];

            // The boundary conditions are set to be “natural”
            y2a[1] = 0.0;
            var u = new double[n];
            var qn = 0.0;
            var un = 0.0;
            for (int i = 1; i <= n - 2; i++)
            {
                // This is the decomposition loop of the tridiagonal algorithm.
                // y2 and u are used for temporary storage of the decomposed factors.
                var sig = (xa[i] - xa[i - 1]) / (xa[i + 1] - xa[i - 1]);
                var p = sig * y2a[i - 1] + 2;
                y2a[i] = (sig - 1) / p;
                u[i] =
                    (ya[i + 1] - ya[i + 0]) / (xa[i + 1] - xa[i + 0]) -
                    (ya[i + 0] - ya[i - 1]) / (xa[i + 0] - xa[i - 1]);
                u[i] = (6 * u[i] / (xa[i + 1] - xa[i - 1]) - sig * u[i - 1]) / p;
            }

            y2a[n - 1] = (un - qn * u[n - 2]) / (qn * y2a[n - 2] + 1);
            for (int k = n - 2; k >= 0; k--)
            {
                // This is the backsubstitution loop of the tridiagonal algorithm.
                y2a[k] = y2a[k] * y2a[k + 1] + u[k];
            }
        }

        /// <summary>
        /// Given the arrays xa[1..n] and ya[1..n], which tabulate a function(with the xai’s in order),
        /// and given the array y2a[1..n], which is the output from spline above, and given a value of
        /// x, this routine returns a cubic-spline interpolated value y.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public bool TryGetValue(double x, out double y)
        {
            if (x < xa[0] || x > xa[n - 1])
            {
                y = 0;
                return false;
            }

            y = GetValue(x);
            return true;
        }

        private double GetValue(double x)
        {
            // We will find the right place in the table by means of
            // bisection.This is optimal if sequential calls to this
            // routine are at random values of x.If sequential calls
            // are in order, and closely spaced, one would do better
            // to store previous values of klo and khi and test if
            // they remain appropriate on the next call.
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

            // klo and khi now bracket the input value of x.
            var h = xa[khi] - xa[klo];
            if (h == 0.0)
            {
                throw new InvalidOperationException("Bad xa input to routine splint"); // The xa’s must be distinct.
            }

            var a = (xa[khi] - x) / h;
            var b = (x - xa[klo]) / h; // Cubic spline polynomial is now evaluated.
            return a * ya[klo] + b * ya[khi] + ((a * a * a - a) * y2a[klo] + (b * b * b - b) * y2a[khi]) * (h * h) / 6;
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

            var t1 = new NRCubicSplineInterpolator(xs, ys);

            var y = t1.GetValue(n / 2.0);
            if (Math.Abs(y - y0) > 1.0e-10) throw new InvalidOperationException();

            bool success = t1.TryGetValue(n / 2.0 + 0.5, out y);
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

            t1 = new NRCubicSplineInterpolator(xs, ys);

            double x = n / 2.0;
            y = t1.GetValue(x);
            if (Math.Abs(y - cubic(x)) > 1.0e-10) throw new InvalidOperationException();

            x = n / 2.0 + 0.5;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 0.05) throw new InvalidOperationException();

            x = 0.05;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 1.0e-4) throw new InvalidOperationException();

            x = (n - 1) * 1.0 - 0.5;
            y0 = cubic(x);
            success = t1.TryGetValue(x, out y);
            if (!success) throw new InvalidOperationException();
            if (Math.Abs(y - y0) > 2.5) throw new InvalidOperationException();

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