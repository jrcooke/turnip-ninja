using MountainView.Base;
using MountainView.Mesh;
using MountainViewDesktop.Interpolation;
using System;
using System.Diagnostics;

namespace MountainView.SkyColor
{
    public class NishitaInterp
    {
        private readonly Nishita skyColor;
        private readonly double h0;
        private readonly double directLight;
        private readonly double ambientLight;
        private readonly double maxDist;
        private readonly double latRadMin;
        private readonly double latRadMax;
        private readonly int numLat;
        private readonly double lonRadMin;
        private readonly double lonRadMax;
        private readonly int numLon;
        private readonly InterpolatonType intType;

        private Lazy<TwoDInterpolator[]> inters;
        private Lazy<AerialPers> aerialPers;

        public NishitaInterp(Nishita skyColor, double h0,
            double directLight, double ambiantLight,
            double maxDist,
            double latRadMin, double latRadMax, int numLat,
            double lonRadMin, double lonRadMax, int numLon)
        {
            this.skyColor = skyColor;
            this.h0 = h0;
            this.directLight = directLight;
            this.ambientLight = ambiantLight;
            this.maxDist = maxDist;
            this.latRadMin = latRadMin;
            this.latRadMax = latRadMax;
            this.numLat = numLat;
            this.lonRadMin = lonRadMin < lonRadMax ? lonRadMin : lonRadMax;
            this.lonRadMax = lonRadMax > lonRadMin ? lonRadMax : lonRadMin;
            this.numLon = numLon;
            this.intType = InterpolatonType.Linear;

            // Fixup
            if (this.lonRadMin < 0.0) this.lonRadMin = 0.0;

            inters = new Lazy<TwoDInterpolator[]>(() => GetInters());
            aerialPers = new Lazy<AerialPers>(() => GetAerialPers());
        }

        private AerialPers GetAerialPers()
        {
            int numDists = 10;
            return new AerialPers(numDists, maxDist, h0, skyColor, intType);
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
                    var skyPt = new GeoPolar2d(Angle.FromRadians(lats[x]), Angle.FromRadians(lons[y]));
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
                inters[k] = new TwoDInterpolator(lats, lons, values[k], intType);
            }

            return inters;
        }

        public MyColor SkyColorAtPoint(GeoPolar2d p)
        {
            double[] values = new double[3];
            for (int k = 0; k < 3; k++)
            {
                inters.Value[k].TryGetValue(p.Lat.Radians, p.Lon.Radians, out double o);
                values[k] = o;
            }

            return Utils.ColorFromDoubleArray(values);
        }

        public MyColor SkyColorAtPointDist(GeoPolar2d p, double dist, MyColor ground, double nDotL)
        {
            double theta = skyColor.GetTheta(p);
            MyDColor attenuation = new MyDColor();
            MyDColor airColorR = new MyDColor();
            MyDColor airColorM = new MyDColor();
            MyDColor directPart = new MyDColor();

            aerialPers.Value.TryGetValues(dist, ref attenuation, ref airColorR, ref airColorM, ref directPart);

            MyColor color = Nishita.CombineForAerialPrespective(ground, theta, nDotL, ambientLight, attenuation, airColorR, airColorM, directPart);
            return color;
        }

        private class AerialPers
        {
            private readonly OneDVectorInterpolator inters;

            public AerialPers(int numDists, double maxDist, double h0, Nishita skyColor, InterpolatonType intType)
            {
                double[] dists = new double[numDists];
                for (int x = 0; x < numDists; x++)
                {
                    dists[x] = maxDist * x / (numDists + -1);
                }

                double[][] values = new double[numDists][];
                for (int k = 0; k < values.Length; k++)
                {
                    values[k] = new double[12];
                }

                for (int x = 0; x < dists.Length; x++)
                {
                    skyColor.SkyColorAtPointComputer(
                        h0, dists[x],
                        out MyDColor attenuation,
                        out MyDColor airColorR,
                        out MyDColor airColorM,
                        out MyDColor directPart);

                    for (int k = 0; k < 3; k++)
                    {
                        values[x][0] = attenuation.R;
                        values[x][1] = attenuation.G;
                        values[x][2] = attenuation.B;
                        values[x][3] = airColorR.R;
                        values[x][4] = airColorR.G;
                        values[x][5] = airColorR.B;
                        values[x][6] = airColorM.R;
                        values[x][7] = airColorM.G;
                        values[x][8] = airColorM.B;
                        values[x][9] = directPart.R;
                        values[x][10] = directPart.G;
                        values[x][11] = directPart.B;
                    }
                }

                inters = new OneDVectorInterpolator(dists, values, intType);
            }

            public bool TryGetValues(double dist,
                ref MyDColor attenuation,
                ref MyDColor airColorR,
                ref MyDColor airColorM,
                ref MyDColor directPart)
            {
                double[] values = new double[12];
                if (!inters.TryGetValue(dist, values))
                {
                    return false;
                }

                attenuation.R = values[0];
                attenuation.G = values[1];
                attenuation.B = values[2];
                airColorR.R = values[3];
                airColorR.G = values[4];
                airColorR.B = values[5];
                airColorM.R = values[6];
                airColorM.G = values[7];
                airColorM.B = values[8];
                directPart.R = values[9];
                directPart.G = values[10];
                directPart.B = values[11];

                return true;
            }
        }
    }
}
