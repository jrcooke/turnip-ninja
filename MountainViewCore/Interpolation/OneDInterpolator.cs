// Based now only on Numerical Recipes code
// http://www.it.uom.gr/teaching/linearalgebra/NumericalRecipiesInC/c3-6.pdf

using System;
using System.Linq;

namespace MountainViewDesktop.Interpolation
{
    public class OneDInterpolator
    {
        InterpolatonType type;
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
        public OneDInterpolator(double[] x, double[] y, InterpolatonType type)
        {
            this.type = type;
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

            switch (type)
            {
                case InterpolatonType.Linear:
                    // Nothing more to do
                    break;
                case InterpolatonType.Cubic:
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

                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        /// <summary>
        /// Given the arrays xa[1..n] and ya[1..n], which tabulate a function(with the xai’s in order),
        /// and given the array y2a[1..n], which is the output from spline above, and given a value of
        /// x, this routine returns a cubic-spline interpolated value y.
        /// </summary>
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

            switch (type)
            {
                case InterpolatonType.Linear:
                    return a * ya[klo] + b * ya[khi];
                case InterpolatonType.Cubic:
                    // Cubic spline polynomial is now evaluated.
                    return a * ya[klo] + b * ya[khi] + ((a * a * a - a) * y2a[klo] + (b * b * b - b) * y2a[khi]) * (h * h) / 6;
                default: throw new InvalidOperationException();
            }
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

            var t1 = new OneDInterpolator(xs, ys, InterpolatonType.Cubic);
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

            t1 = new OneDInterpolator(xs, ys, InterpolatonType.Cubic);

            double x = n / 2.0 + 0.5;
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
