using MountainView.Base;
using MountainView.Mesh;
using System;

namespace MountainViewCore.SkyColor
{
    // https://www.cs.utah.edu/~shirley/papers/sunsky/sunsky.pdf
    // Preetham, A. J. et al. "A Practical Analytic Model for Daylight" SIGGRAPH (1999).
    public class Preetham
    {
        private readonly double thetaSun;
        private readonly double phiSun;
        private AllSkylightDistCoef skyParams;

        /// <param name="turbidity">2.2 is clear sky</param>
        public Preetham(GeoPolar2d sunPos, double turbidity = 2.2)
        {
            // Theta is angle from zenith down to sun.
            // phi is angle around up-axis from south.
            thetaSun = Math.PI / 2 - sunPos.Lon.Radians;
            phiSun = Math.PI - sunPos.Lat.Radians;
            while (phiSun > +Math.PI) phiSun -= 2 * Math.PI;
            while (phiSun < -Math.PI) phiSun += 2 * Math.PI;

            skyParams = new AllSkylightDistCoef(turbidity, thetaSun);
        }

        public void RenderToJpeg(string filename)
        {
            int width = 1000;
            int height = 250;
            MyColor[][] image = new MyColor[width][];
            for (int phiI = 0; phiI < width; phiI++)
            {
                double phiDeg = phiI * 360.0 / width;
                image[phiI] = new MyColor[height];
                for (int thetaI = 0; thetaI < height; thetaI++)
                {
                    double thetaDeg = thetaI * 90.0 / height;
                    var p = new GeoPolar2d(phiDeg, thetaDeg);
                    image[phiI][thetaI] = SkyColorAtPoint(p);
                }
            }

            Utils.WriteImageFile(image, filename, (a) => a, OutputType.JPEG);
        }

        public MyColor SkyColorAtPoint(GeoPolar2d p)
        {
            if (p.Lon.Radians < 0.0) return new MyColor();
            // Theta is angle from zenith down to sun.
            // phi is angle around up-axis from south.
            var theta = Math.PI / 2 - p.Lon.Radians;
            var phi = Math.PI - p.Lat.Radians;
            while (phi > +Math.PI) phi -= 2 * Math.PI;
            while (phi < -Math.PI) phi += 2 * Math.PI;

            var Y = SkyChanelAtPoint(skyParams.Y, theta, phi);
            var x = SkyChanelAtPoint(skyParams._x, theta, phi);
            var y = SkyChanelAtPoint(skyParams._y, theta, phi);

            // Convert from luminance and chromaticity to RGB.
            var X = Y / y * x;
            var Z = Y / y * (1 - x - y);

            var r = +3.241030 * X - 1.537410 * Y - 0.498620 * Z;
            var g = -0.969242 * X + 1.875960 * Y + 0.041555 * Z;
            var b = +0.055632 * X - 0.203979 * Y + 1.056980 * Z;

            double scale = -3.0;
            r = 1 - Math.Exp(scale * r);
            g = 1 - Math.Exp(scale * g);
            b = 1 - Math.Exp(scale * b);

            var color = new MyColor(
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, r))),
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, g))),
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, b))));
            return color;
        }

        private double SkyChanelAtPoint(SkylightDistCoef skyCoef, double theta, double phi)
        {
            var gamma = Math.Acos(
                Math.Cos(thetaSun) * Math.Cos(theta) +
                Math.Sin(thetaSun) * Math.Sin(theta) * Math.Cos(phiSun - phi));
            return skyCoef.Zenith * CurlyF(skyCoef, theta, gamma) / CurlyF(skyCoef, 0, thetaSun);
        }

        private static double CurlyF(SkylightDistCoef skyCoef, double theta, double gamma)
        {
            return
                (1 + skyCoef.A * Math.Exp(skyCoef.B / Math.Cos(theta))) *
                (1 + skyCoef.C * Math.Exp(skyCoef.D * gamma) + skyCoef.E * Math.Cos(gamma) * Math.Cos(gamma));
        }

        private class AllSkylightDistCoef
        {
            public SkylightDistCoef _x { get; private set; }
            public SkylightDistCoef _y { get; private set; }
            public SkylightDistCoef Y { get; private set; }

            public AllSkylightDistCoef(double turbidity, double thetaSun)
            {
                var t1 = thetaSun;
                var t2 = t1 * t1;
                var t3 = t1 * t2;
                double Yz = (4.0453 * turbidity - 4.9710) * Math.Tan((4.0 / 9 - turbidity / 120) * (Math.PI - 2 * thetaSun)) - 0.2155 * turbidity + 2.4192;
                double Y0 = (4.0453 * turbidity - 4.9710) * Math.Tan((4.0 / 9 - turbidity / 120) * (Math.PI)) - 0.2155 * turbidity + 2.4192;
                double YZen = Yz / Y0;
                double xZen =
                    (+0.00166 * t3 - 0.00375 * t2 + 0.00209 * t1 + 0.00000) * turbidity * turbidity +
                    (-0.02903 * t3 + 0.06377 * t2 - 0.03202 * t1 + 0.00394) * turbidity +
                    (+0.11693 * t3 - 0.21196 * t2 + 0.06052 * t1 + 0.25886);
                double yZen =
                    (+0.00275 * t3 - 0.00610 * t2 + 0.00317 * t1 + 0.00000) * turbidity * turbidity +
                    (-0.04214 * t3 + 0.08970 * t2 - 0.04153 * t1 + 0.00516) * turbidity +
                    (+0.15346 * t3 - 0.26756 * t2 + 0.06670 * t1 + 0.26688);

                Y = new SkylightDistCoef()
                {
                    A = +0.1787 * turbidity - 1.4630,
                    B = -0.3554 * turbidity + 0.4275,
                    C = -0.0227 * turbidity + 5.3251,
                    D = +0.1206 * turbidity - 2.5771,
                    E = -0.0670 * turbidity + 0.3703,
                    Zenith = YZen,
                };

                _x = new SkylightDistCoef()
                {
                    A = -0.0193 * turbidity - 0.2592,
                    B = -0.0665 * turbidity + 0.0008,
                    C = -0.0004 * turbidity + 0.2125,
                    D = -0.0641 * turbidity - 0.8989,
                    E = -0.0033 * turbidity + 0.0452,
                    Zenith = xZen,
                };

                _y = new SkylightDistCoef()
                {
                    A = -0.0167 * turbidity - 0.2608,
                    B = -0.0950 * turbidity + 0.0092,
                    C = -0.0079 * turbidity + 0.2102,
                    D = -0.0441 * turbidity - 1.6537,
                    E = -0.0109 * turbidity + 0.0529,
                    Zenith = yZen,
                };
            }
        }

        private class SkylightDistCoef
        {
            public double A { get; set; }
            public double B { get; set; }
            public double C { get; set; }
            public double D { get; set; }
            public double E { get; set; }
            public double Zenith { get; set; }
        }
    }
}
