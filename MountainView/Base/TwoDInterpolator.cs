// Based on code from Alex Musayev: https://gist.github.com/dreikanter/3526685
// And from http://www.it.uom.gr/teaching/linearalgebra/NumericalRecipiesInC/c3-6.pdf

using System;
using System.Collections.Generic;

namespace MountainView.Base
{
    public class TwoDInterpolator
    {
        private readonly double[] xs;
        private readonly SplineInterpolator[] values;
        private Dictionary<double, SplineInterpolator> cachedValues = new Dictionary<double, SplineInterpolator>();

        public TwoDInterpolator(double[] xs, double[] ys, double[][] values)
        {
            this.xs = xs;
            this.values = new SplineInterpolator[xs.Length];
            for (int j = 0; j < xs.Length; j++)
            {
                this.values[j] = new SplineInterpolator(ys, values[j]);
            }
        }

        public double GetValue(double x, double y)
        {
            SplineInterpolator inter = null;
            if (!cachedValues.TryGetValue(y, out inter))
            {
                double[] interpValues = new double[xs.Length];
                for (int j = 0; j < xs.Length; j++)
                {
                    interpValues[j] = this.values[j].GetValue(y);
                }

                inter = new SplineInterpolator(xs, interpValues);
                cachedValues.Add(y, inter);
            }

            return inter.GetValue(x);
        }

        /// <summary>
        /// Spline interpolation class.
        /// </summary>
        private class SplineInterpolator
        {
            private readonly double[] xs;
            private readonly double[] values;
            private readonly double[] h;
            private readonly double[] a;

            /// <param name="nodes">Collection of known points for further interpolation.
            /// Should contain at least two items.</param>
            public SplineInterpolator(double[] xs, double[] values)
            {
                var n = xs.Length;
                if (n < 3)
                {
                    throw new ArgumentException("At least three point required for interpolation.");
                }

                this.xs = xs;
                this.values = values;
                a = new double[n];
                h = new double[n];

                for (int i = 1; i < n; i++)
                {
                    h[i] = xs[i] - xs[i - 1];
                }

                var sub = new double[n - 1];
                var diag = new double[n - 1];
                var sup = new double[n - 1];

                for (int i = 1; i <= n - 2; i++)
                {
                    diag[i] = (h[i + 0] + h[i + 1]) / 3;
                    sup[i] = h[i + 1] / 6;
                    sub[i] = h[i + 0] / 6;
                    a[i] =
                        (values[i + 1] - values[i + 0]) / h[i + 1] -
                        (values[i + 0] - values[i - 1]) / h[i + 0];
                }

                SolveTridiag(sub, diag, sup, a, n - 2);
            }

            /// <summary>
            /// Gets interpolated value for specified argument.
            /// </summary>
            /// <param name="x">Argument value for interpolation. Must be within
            /// the interval bounded by lowest and highest <see cref="xs"/> values.</param>
            public double GetValue(double x)
            {
                int gap = 0;
                var previous = double.MinValue;

                // At the end of this iteration, "gap" will contain the index of the interval
                // between two known values, which contains the unknown z, and "previous" will
                // contain the biggest z value among the known samples, left of the unknown z
                for (int i = 0; i < xs.Length; i++)
                {
                    if (xs[i] < x && xs[i] > previous)
                    {
                        previous = xs[i];
                        gap = i + 1;
                    }
                }

                var x1 = x - previous;
                var x2 = h[gap] - x1;

                return (
                        (-a[gap - 1] / 6 * (x2 + h[gap]) * x1 + values[gap - 1]) * x2 +
                        (-a[gap - 0] / 6 * (x1 + h[gap]) * x2 + values[gap - 0]) * x1
                    ) / h[gap];
            }

            /// <summary>
            /// Solve linear system with tridiagonal n*n matrix "a"
            /// using Gaussian elimination without pivoting.
            /// </summary>
            private static void SolveTridiag(double[] sub, double[] diag, double[] sup, double[] b, int n)
            {
                for (int i = 2; i <= n; i++)
                {
                    sub[i] = sub[i] / diag[i - 1];
                    diag[i] = diag[i] - sub[i] * sup[i - 1];
                    b[i] = b[i] - sub[i] * b[i - 1];
                }

                b[n] = b[n] / diag[n];
                for (int i = n - 1; i >= 1; i--)
                {
                    b[i] = (b[i] - sup[i] * b[i + 1]) / diag[i];
                }
            }
        }
    }
}