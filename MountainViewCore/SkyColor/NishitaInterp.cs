using MountainView.Base;
using MountainView.Mesh;
using MountainViewDesktop.Interpolation;
using System;

namespace MountainView.SkyColor
{
    public class NishitaInterp
    {
        private readonly Nishita skyColor;
        private readonly double h0;
        private readonly double latRadMin;
        private readonly double latRadMax;
        private readonly int numLat;
        private readonly double lonRadMin;
        private readonly double lonRadMax;
        private readonly int numLon;

        private Lazy<TwoDInterpolator[]> inters;

        public NishitaInterp(Nishita skyColor, double h0,
            double directLight, double ambiantLight,
            double latRadMin, double latRadMax, int numLat,
            double lonRadMin, double lonRadMax, int numLon)
        {
            this.skyColor = skyColor;
            this.h0 = h0;
            this.latRadMin = latRadMin;
            this.latRadMax = latRadMax;
            this.numLat = numLat;
            this.lonRadMin = lonRadMin < 0.0 ? 0.0 : lonRadMin;
            this.lonRadMax = lonRadMax;
            this.numLon = numLon;

            inters = new Lazy<TwoDInterpolator[]>(() => GetInters());
        }

        private TwoDInterpolator[] GetInters()
        {
            double[] lats = new double[numLat];
            for (int x = 0; x < numLat; x++)
            {
                lats[x] = latRadMin + (latRadMax - latRadMin) * x / (numLat + -1);
            }

            double[] lons = new double[numLon];
            for (int y = 0; y < numLon; y++)
            {
                lons[y] = lonRadMin + (lonRadMax - lonRadMin) * y / (numLon + -1);
            }

            double[][][] values = new double[Utils.ColorToDoubleArray.Length][][];
            for (int k = 0; k < values.Length; k++)
            {
                values[k] = new double[numLat][];
                for (int x = 0; x < numLat; x++)
                {
                    values[k][x] = new double[numLon];
                }
            }

            for (int x = 0; x < lats.Length; x++)
            {
                for (int y = 0; y < lons.Length; y++)
                {
                    var skyPt = new GeoPolar2d(lats[x], lons[y]);
                    var color2 = skyColor.SkyColorAtPoint(h0, skyPt);
                    for (int k = 0; k < values.Length; k++)
                    {
                        values[k][x][y] = Utils.ColorToDoubleArray[k](color2);
                    }
                }
            }

            var inters = new TwoDInterpolator[values.Length];
            for (int k = 0; k < inters.Length; k++)
            {
                inters[k] = new TwoDInterpolator(lats, lons, values[k], InterpolatonType.Cubic);
            }

            return inters;
        }

        public MyColor SkyColorAtPoint(GeoPolar2d p)
        {
            double[] values = new double[3];
            for (int k = 0; k < 3; k++)
            {
                double o;
                inters.Value[k].TryGetValue(p.Lat.Radians, p.Lon.Radians, out o);
                values[k] = o;
            }

            return Utils.ColorFromDoubleArray(values);
        }

        public MyColor SkyColorAtPointDist(GeoPolar2d p, double dist, MyColor ground)
        {
            throw new NotImplementedException();
        }
    }
}
