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
            this.lonRadMin = lonRadMin < 0.0 ? 0.0 : lonRadMin;
            this.lonRadMax = lonRadMax;
            this.numLon = numLon;

            inters = new Lazy<TwoDInterpolator[]>(() => GetInters());
            aerialPers = new Lazy<AerialPers>(() => GetAerialPers());
        }

        private AerialPers GetAerialPers()
        {
            int numDists = 100;
            double[] dists = new double[numDists];
            for (int x = 0; x < numDists; x++)
            {
                dists[x] = maxDist * x / (numDists + -1);
            }

            double[][] valuesAR = new double[Utils.DColorToDoubleArray.Length][];
            double[][] valuesAM = new double[Utils.DColorToDoubleArray.Length][];
            double[][] valuesAT = new double[Utils.DColorToDoubleArray.Length][];
            double[][] valuesDP = new double[Utils.DColorToDoubleArray.Length][];
            for (int k = 0; k < valuesAR.Length; k++)
            {
                valuesAR[k] = new double[numDists];
                valuesAM[k] = new double[numDists];
                valuesAT[k] = new double[numDists];
                valuesDP[k] = new double[numDists];
            }

            for (int x = 0; x < dists.Length; x++)
            {
                skyColor.SkyColorAtPointComputer(
                    h0, dists[x],
                    out MyDColor attenuation,
                    out MyDColor airColorR,
                    out MyDColor airColorM,
                    out MyDColor directPart);

                for (int k = 0; k < valuesAR.Length; k++)
                {
                    valuesAR[k][x] = Utils.DColorToDoubleArray[k](airColorR);
                    valuesAM[k][x] = Utils.DColorToDoubleArray[k](airColorM);
                    valuesAT[k][x] = Utils.DColorToDoubleArray[k](attenuation);
                    valuesDP[k][x] = Utils.DColorToDoubleArray[k](directPart);
                }
            }

            AerialPers aerialPers = new AerialPers
            {
                intersAirColorR = new OneDInterpolator[valuesAR.Length],
                intersAirColorM = new OneDInterpolator[valuesAM.Length],
                intersAttenuation = new OneDInterpolator[valuesAT.Length],
                intersDirectPart = new OneDInterpolator[valuesDP.Length],
            };

            for (int k = 0; k < aerialPers.intersAirColorR.Length; k++)
            {
                aerialPers.intersAirColorR[k] = new OneDInterpolator(dists, valuesAR[k], InterpolatonType.Cubic);
                aerialPers.intersAirColorM[k] = new OneDInterpolator(dists, valuesAM[k], InterpolatonType.Cubic);
                aerialPers.intersAttenuation[k] = new OneDInterpolator(dists, valuesAT[k], InterpolatonType.Cubic);
                aerialPers.intersDirectPart[k] = new OneDInterpolator(dists, valuesDP[k], InterpolatonType.Cubic);
            }

            return aerialPers;
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
                inters.Value[k].TryGetValue(p.Lat.Radians, p.Lon.Radians, out double o);
                values[k] = o;
            }

            return Utils.ColorFromDoubleArray(values);
        }

        public MyColor SkyColorAtPointDist(GeoPolar2d p, double dist, MyColor ground, double nDotL)
        {
            double theta = skyColor.GetTheta(p);
            MyDColor attenuation = aerialPers.Value.GetAttenuation(dist);
            MyDColor airColorR = aerialPers.Value.GetAirColorR(dist);
            MyDColor airColorM = aerialPers.Value.GetAirColorM(dist);
            MyDColor directPart = aerialPers.Value.GetDirectPart(dist);

            MyColor color = Nishita.CombineForAerialPrespective(ground, theta, nDotL, ambientLight, attenuation, airColorR, airColorM, directPart);
            return color;
        }

        private class AerialPers
        {
            public OneDInterpolator[] intersAttenuation;
            public OneDInterpolator[] intersAirColorR;
            public OneDInterpolator[] intersAirColorM;
            public OneDInterpolator[] intersDirectPart;

            public MyDColor GetAttenuation(double dist)
            {
                return DoDLookup(intersAttenuation, dist);
            }
            public MyDColor GetAirColorR(double dist)
            {
                return DoDLookup(intersAirColorR, dist);
            }
            public MyDColor GetAirColorM(double dist)
            {
                return DoDLookup(intersAirColorM, dist);
            }
            public MyDColor GetDirectPart(double dist)
            {
                return DoDLookup(intersDirectPart, dist);
            }

            private static MyDColor DoDLookup(OneDInterpolator[] inters, double x)
            {
                double[] values = new double[3];
                for (int k = 0; k < 3; k++)
                {
                    inters[k].TryGetValue(x, out double o);
                    values[k] = o;
                }

                return new MyDColor()
                {
                    R = values[0],
                    G = values[1],
                    B = values[2],
                };
            }
        }
    }
}
