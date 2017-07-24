using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Interpolation
{
    class Program
    {
        static void Mainx(string[] args)
        {
            var r = 1000;
            var known = new Dictionary<double, double>
                {
                    { 0.0, 0.0 },
                    { 100.0, 0.50 * r },
                    { 300.0, 0.75 * r },
                    { 500.0, 1.00 * r },
                };

            foreach (var pair in known)
            {
                Debug.WriteLine(String.Format("{0:0.000}\t{1:0.000}", pair.Key, pair.Value));
            }

            var scaler = new SplineInterpolator(known);
            var start = known.First().Key;
            var end = known.Last().Key;
            var step = (end - start) / 50;

            for (var x = start; x <= end; x += step)
            {
                var y = scaler.GetValue(x);
                Debug.WriteLine(String.Format("\t\t{0:0.000}\t{1:0.000}", x, y));
            }
        }
    }
}
