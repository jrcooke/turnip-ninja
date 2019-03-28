using MountainView.Base;
using MountainView.Mesh;
using System;
using System.Diagnostics;

namespace MountainView.SkyColor
{
    public class Nishita
    {
        private readonly double thetaSun;
        private readonly double phiSun;
        private readonly double sinThetaSun;
        private readonly double cosThetaSun;
        private readonly double sinPhiSun;
        private readonly double cosPhiSun;
        private readonly double mieScale;

        public const double H_atmosphere = 60000;

        public enum Channel
        {
            R = 0, // lambda = 680nm
            G = 1, // lambda = 550nm
            B = 2, // lambda = 440nm
        }

        // Units are meters
        public const double H_R = 8000.0;

        // Units are 1/meters
        private static readonly double[] BetaR0 = new double[]
        {
             05.8e-6, // R
             13.5e-6, // G
             33.1e-6, // B
        };

        // meters
        public const double H_M = 1200.0;

        // Units are 1/meters
        private const double BetaM0 = 210e-5;

        /// <param name="turbidity">2.2 is clear sky</param>
        public Nishita(GeoPolar2d sunPos, double turbidity = 2.2)
        {
            //thetaSun = sunPos.Lon.Radians;
            //phiSun = sunPos.Lat.Radians;
            thetaSun = sunPos.Lat.Radians;
            phiSun = sunPos.Lon.Radians;
            this.mieScale = turbidity / 220;

            sinThetaSun = Math.Sin(thetaSun);
            sinPhiSun = Math.Sin(phiSun);
            cosThetaSun = Math.Cos(thetaSun);
            cosPhiSun = Math.Cos(phiSun);
        }

        public void RenderToJpeg(string filename, double h0)
        {
            int width = 1000;
            int height = 250;
            MyColor[][] image = new MyColor[width][];
            for (int thetaI = 0; thetaI < width; thetaI++)
            {
                double thetaDeg = thetaI * 360.0 / width;
                image[thetaI] = new MyColor[height];
                for (int phiI = 0; phiI < height; phiI++)
                {
                    double phiDeg = phiI * 90.0 / height;
                    var p = new GeoPolar2d(thetaDeg, phiDeg);
                    image[thetaI][phiI] = SkyColorAtPoint(h0, p);
                }
            }

            Utils.WriteImageFile(image, filename, (a) => a, OutputType.JPEG);
        }

        public MyColor SkyColorAtPointDist(double h0, GeoPolar2d p, double dist, MyColor ground, double nDotL, double ambiantLight)
        {
            double thetaPixel = p.Lat.Radians;
            double phiPixel = p.Lon.Radians;
            double theta = Utils.AngleBetween(thetaPixel, phiPixel, thetaSun, phiSun);

            return NewMethod(h0, dist, ground, nDotL, ambiantLight, theta);
        }

        private MyColor NewMethod(double h0, double dist, MyColor ground, double nDotL, double ambiantLight, double theta)
        {
            var rayR = new MyDColor()
            {
                R = BetaR0[(int)Channel.R] * P_R(theta),
                G = BetaR0[(int)Channel.G] * P_R(theta),
                B = BetaR0[(int)Channel.B] * P_R(theta),
            };
            var rayM = mieScale * BetaM0 * P_M(theta);

            var dground = InverseScaleColor(ground);

            // And the attenuation part, which is present even if no direct sunlight
            var attenuation = ElementLightAP(h0, dist, 0);
            var ambient = dground.Mult(ambiantLight).Mult(attenuation);

            MyDColor airColor = new MyDColor();
            MyDColor direct = new MyDColor();
            var hitEarth = Intersect(h0, sinPhiSun, 0);
            if (!hitEarth.HasValue || hitEarth.Value < 0)
            {
                // Air has no color if there is no sunlight
                double? lmaxP = Intersect(h0, sinPhiSun, H_atmosphere);

                MyDColor sunAttenuationR = new MyDColor()
                {
                    R = Math.Exp(-TR(h0, sinPhiSun, lmaxP.Value, Channel.R)),
                    G = Math.Exp(-TR(h0, sinPhiSun, lmaxP.Value, Channel.G)),
                    B = Math.Exp(-TR(h0, sinPhiSun, lmaxP.Value, Channel.B)),
                };
                double sunAttenuationM = Math.Exp(-TM(h0, sinPhiSun, lmaxP.Value));

                double densityPartOfScatteringR = Math.Exp(-h0 / H_R);
                double densityPartOfScatteringM = Math.Exp(-h0 / H_M);

                airColor = Integrate(0, dist, l => ElementLightAP(h0, l, 0));
                airColor = sunAttenuationR
                    .Mult(sunAttenuationM)
                    .Mult(rayR.Mult(densityPartOfScatteringR).Add(rayM * densityPartOfScatteringM))
                    .Mult(airColor);

                var sunLight = sunAttenuationR.Mult(sunAttenuationM);

                direct = dground.Mult(nDotL).Mult(sunLight).Mult(attenuation);
            }

            var total = airColor.Add(ambient).Add(direct);
            MyColor color = ScaleColor(total);

            return color;
        }

        public MyColor SkyColorAtPoint(double h0, GeoPolar2d p)
        {
            double thetaPixel = p.Lat.Radians;
            double phiPixel = p.Lon.Radians;

            if (Math.Abs(thetaPixel - thetaSun) < Math.PI / 180 && Math.Abs(phiPixel - phiSun) < Math.PI / 180)
            {
                return MyColor.White;
            }

            double theta = Utils.AngleBetween(thetaPixel, phiPixel, thetaSun, phiSun);

            double sinPhi = Math.Sin(phiPixel);
            if (sinPhi < 0.0) { return new MyColor(); }

            double cosPhi = Math.Cos(phiPixel);
            double sinTheta = Math.Sin(thetaPixel);
            double cosTheta = Math.Cos(thetaPixel);
            var intersect = Intersect(h0, sinPhi, H_atmosphere);
            if (!intersect.HasValue) return new MyColor();

            double l_max = intersect.Value;
            var rayRR = BetaR0[(int)Channel.R] * P_R(theta);
            var rayRG = BetaR0[(int)Channel.G] * P_R(theta);
            var rayRB = BetaR0[(int)Channel.B] * P_R(theta);
            var rayM = mieScale * BetaM0 * P_M(theta);
            var tot =
                Integrate(0, l_max - 10, l => ElementLight(rayRR, rayRG, rayRB, rayM, h0, l, sinPhi, cosPhi, sinTheta, cosTheta));
            MyColor color = ScaleColor(tot);
            return color;
        }

        // The core ofthe element light
        // P is the coords of the pixel we are looking at
        // l is the distance we've traveled
        // h0 is the height at the starting point
        private MyDColor ElementLightAP(double h0, double l, double sinPhiP)
        {
            double scatteredAttenuationRR = Math.Exp(-TR(h0, sinPhiP, l, Channel.R));
            double scatteredAttenuationRG = Math.Exp(-TR(h0, sinPhiP, l, Channel.G));
            double scatteredAttenuationRB = Math.Exp(-TR(h0, sinPhiP, l, Channel.B));
            double scatteredAttenuationM = Math.Exp(-TM(h0, sinPhiP, l));
            return new MyDColor()
            {
                R = scatteredAttenuationRR * scatteredAttenuationM,
                G = scatteredAttenuationRG * scatteredAttenuationM,
                B = scatteredAttenuationRB * scatteredAttenuationM,
            };
        }

        // The core ofthe element light
        // P is the coords of the pixel we are looking at
        // l is the distance we've traveled
        // h0 is the height at the starting point
        private MyDColor ElementLight(
            double rayRR, double rayRG, double rayRB, double rayM,
            double h0, double l,
            double sinPhiP, double cosPhiP,
            double sinThetaP, double cosThetaP)
        {
            // The height of the current element
            double h = H(h0, sinPhiP, l);

            // The psi is the angular distance across earth to current point
            double sinPsi = l / (h + h0 + Utils.AlphaMeters) * cosPhiP;
            double cosPsi = Math.Sqrt(1 - sinPsi * sinPsi);

            // Cosine of angle to sun from up at current element,
            // which is sin of angle of sun from horizon
            double sinPhiSunPrime =
                    sinPsi * sinThetaP * cosPhiSun * sinThetaSun +
                    sinPsi * cosThetaP * cosPhiSun * cosThetaSun +
                    cosPsi * sinPhiSun;
            var hitEarth = Intersect(h, sinPhiSunPrime, 0);
            if (hitEarth.HasValue && hitEarth.Value > 0)
            {
                return new MyDColor();
            }

            double sunAttenuationRR = 1.0;
            double sunAttenuationRG = 1.0;
            double sunAttenuationRB = 1.0;
            double sunAttenuationM = 1.0;
            double? lmaxP = Intersect(h, sinPhiSunPrime, H_atmosphere);
            if (lmaxP.HasValue)
            {
                if (lmaxP.Value < 0)
                {
                    throw new InvalidOperationException();
                }

                sunAttenuationRR = Math.Exp(-TR(h, sinPhiSunPrime, lmaxP.Value, Channel.R));
                sunAttenuationRG = Math.Exp(-TR(h, sinPhiSunPrime, lmaxP.Value, Channel.G));
                sunAttenuationRB = Math.Exp(-TR(h, sinPhiSunPrime, lmaxP.Value, Channel.B));
                sunAttenuationM = Math.Exp(-TM(h, sinPhiSunPrime, lmaxP.Value));
            }

            double densityPartOfScatteringR = Math.Exp(-h / H_R);
            double densityPartOfScatteringM = Math.Exp(-h / H_M);

            double scatteredAttenuationRR = Math.Exp(-TR(h0, sinPhiP, l, Channel.R));
            double scatteredAttenuationRG = Math.Exp(-TR(h0, sinPhiP, l, Channel.G));
            double scatteredAttenuationRB = Math.Exp(-TR(h0, sinPhiP, l, Channel.B));
            double scatteredAttenuationM = Math.Exp(-TM(h0, sinPhiP, l));

            double totalR = sunAttenuationRR * sunAttenuationM * (rayRR * densityPartOfScatteringR + rayM * densityPartOfScatteringM) * scatteredAttenuationRR * scatteredAttenuationM;
            double totalG = sunAttenuationRG * sunAttenuationM * (rayRG * densityPartOfScatteringR + rayM * densityPartOfScatteringM) * scatteredAttenuationRG * scatteredAttenuationM;
            double totalB = sunAttenuationRB * sunAttenuationM * (rayRB * densityPartOfScatteringR + rayM * densityPartOfScatteringM) * scatteredAttenuationRB * scatteredAttenuationM;

            return new MyDColor()
            {
                R = totalR,
                G = totalG,
                B = totalB,
            };
        }

        public static double H(double h0, double sinPhi, double l)
        {
            var rprime = Utils.AlphaMeters + h0;
            // var ln = l / rprime;
            // return -Utils.AlphaMeters + rprime * Math.Sqrt(1 + 2 * ln * sinPhi + ln * ln);
            return h0 + l * sinPhi + l * l / (2 * rprime);
        }

        public static double? Intersect(double h0, double sinPhi, double intersectH)
        {
            var rprime = Utils.AlphaMeters + h0;
            double discriminant = sinPhi * sinPhi + 2 * (intersectH - h0) / rprime;
            if (discriminant < 0.0) return null;

            double sqrt = Math.Sqrt(discriminant);
            return rprime * (sqrt - sinPhi);
        }

        private static double P_R(double theta)
        {
            double cosTheta = Math.Cos(theta);
            return 3.0 / (16.0 * Math.PI) * (1 + cosTheta * cosTheta);
        }

        private static double P_M(double theta)
        {
            double cosTheta = Math.Cos(theta);
            double g = 0.76;
            double g2 = g * g;
            return 3.0 * (1 - g2) * (1 + cosTheta * cosTheta) / (8 * Math.PI * (2 + g2) * Math.Pow(1 + g2 - 2 * g * cosTheta, 1.5));
        }

        public static double TR(double h0, double sinPhi, double lmax, Channel lambda)
        {
            return BetaR0[(int)lambda] * TOverBeta(h0, sinPhi, lmax, H_R);
        }

        public double TM(double h0, double sinPhi, double lmax)
        {
            return mieScale * BetaM0 * TOverBeta(h0, sinPhi, lmax, H_M);
        }

        public static double TOverBeta(double h0, double sinPhi, double lmax, double H)
        {
            if (lmax == 0.0)
            {
                return 0;
            }

            var rprime = Utils.AlphaMeters + h0;
            double sqrtRPo2H = Math.Sqrt(rprime / (2 * H));

            double i = Math.Exp(-h0 / H) * sqrtRPo2H * H *
                ErfSPExpXSq(sinPhi * sqrtRPo2H, sinPhi * sqrtRPo2H + sqrtRPo2H * lmax / rprime);

            if (double.IsInfinity(i) || double.IsNaN(i) || double.IsNegativeInfinity(i) || double.IsPositiveInfinity(i))
            {
                throw new InvalidOperationException();
            }

            return i;
        }

        private static MyColor ScaleColor(MyDColor tot)
        {
            double scale = -45.0;
            var color = new MyColor(
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, 1 - Math.Exp(scale * tot.R)))),
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, 1 - Math.Exp(scale * tot.G)))),
                (byte)(255 * Math.Max(0.0, Math.Min(1.0, 1 - Math.Exp(scale * tot.B)))));
            return color;
        }

        private static MyDColor InverseScaleColor(MyColor color)
        {
            double scale = -45.0;
            MyDColor tot = new MyDColor()
            {
                R = Math.Log(1.0 - color.R / 255.0) / scale,
                G = Math.Log(1.0 - color.G / 255.0) / scale,
                B = Math.Log(1.0 - color.B / 255.0) / scale,
            };
            return tot;
        }

        public static void RunTests()
        {
            var integralOfPR = 2 * Math.PI * Integrate(0, Math.PI, theta => P_R(theta) * Math.Sin(theta), 100000);
            var integralOfPM = 2 * Math.PI * Integrate(0, Math.PI, theta => P_M(theta) * Math.Sin(theta), 100000);
            AssertEqualWithin(1.0, integralOfPR, 2.5e-6, "Integral of R phase function over solid angle");
            AssertEqualWithin(1.0, integralOfPM, 2.5e-6, "Integral of M phase function over solid angle");

            double lmax0 = Intersect(0, 0, H_atmosphere).Value;
            double lmax1 = Intersect(0, 1, H_atmosphere).Value;

            double naieve = TOverBetaSimple(0, 1, lmax1, H_R, 10000);
            double optimi = TOverBeta(0, 1, lmax1, H_R);
            AssertEqualWithin(naieve, optimi, 2.5e-6, "Comparing T_R/B_R to simple one, striaght up");

            naieve = TOverBetaSimple(0, 0, lmax0, H_R);
            optimi = TOverBeta(0, 0, lmax0, H_R);
            AssertEqualWithin(naieve, optimi, 1.5e-7, "Comparing T_R/B_R to simple one, toward horizon");

            naieve = TOverBetaSimple(0, 1, lmax1, H_M, 100000);
            optimi = TOverBeta(0, 1, lmax1, H_M);
            AssertEqualWithin(naieve, optimi, 1.5e-6, "Comparing T_M/B_M to simple one, striaght up");

            naieve = TOverBetaSimple(0, 0, lmax0, H_M);
            optimi = TOverBeta(0, 0, lmax0, H_M);
            AssertEqualWithin(naieve, optimi, 3.0e-14, "Comparing T_M/B_M to simple one, toward horizon");

            naieve = H_R;
            optimi = TOverBeta(0, 1, 100 * H_R, H_R);
            AssertEqualWithin(naieve, optimi, 0.07, "Comparing T_R/B_R should be approx H_R, striaght up");

            naieve = H_M;
            optimi = TOverBeta(0, 1, 100 * H_M, H_M);
            AssertEqualWithin(naieve, optimi, 0.01, "Comparing T_M/B_M should be approx H_M, striaght up");

        }

        private static double TOverBetaSimple(double h0, double sinPhi, double lmax, double charHeight, int num = 1000)
        {
            if (lmax < 0.0)
            {
                throw new InvalidOperationException();
            }

            var i = Integrate(0, lmax, l => Math.Exp(-H(h0, sinPhi, l) / charHeight), num);
            return i;
        }

        private static void AssertEqualWithin(double x0, double x1, double pctDiff, string message)
        {
            double diff = Math.Abs(x0 - x1) / (Math.Abs(x0 + x1) + 0.000001);
            bool isOk = (diff * 100 < pctDiff);
            Debug.WriteLine(message + ": " + x0 + ", " + x1 + ", pctDelta is " + diff * 100 + ": " + (isOk ? "OK" : "ERROR"));
            if (!isOk) throw new InvalidOperationException();
        }

        public static double Integrate(double x0, double x1, Func<double, double> f, int num = 15)
        {
            double dx = (x1 - x0) / (num - 1);

            double acc = (f(x0) + f(x1)) / 2;
            for (int i = 1; i < (num - 1); i++)
            {
                acc += f(x0 + i * dx);
            }

            return acc * dx;
        }

        public static MyDColor Integrate(double x0, double x1, Func<double, MyDColor> f, int num = 15)
        {
            double dx = (x1 - x0) / (num - 1);

            MyDColor acc = new MyDColor();
            MyDColor f0 = f(x0);
            MyDColor f1 = f(x1);
            acc.R = (f0.R + f1.R) / 2;
            acc.G = (f0.G + f1.G) / 2;
            acc.B = (f0.B + f1.B) / 2;

            for (int i = 1; i < (num - 1); i++)
            {
                var fi = f(x0 + i * dx);
                acc.R += fi.R;
                acc.G += fi.G;
                acc.B += fi.B;
            }

            acc.R *= dx;
            acc.G *= dx;
            acc.B *= dx;

            return acc;
        }

        /// <summary>
        /// Gives the error function
        ///     Erf(x) = 2/Sqrt(PI) * Integral(0, x, exp(-t^2) dt)
        /// </summary>
        /// <param name="x0">Lower bound on the ERF integral</param>
        public static double ErfSPExpXSq(double x)
        {
            //return Erf(0, x);
            var erf = ErfSPExpXSqWorker(x);
            return erf.Item1 + erf.Item2;
        }

        public static double Erf(double x0, double x1)
        {
            var ret = ErfSPExpXSq(x0, x1) * Math.Exp(-x0 * x0) / Math.Sqrt(Math.PI);
            return ret;
        }

        private static readonly double SqrtPi = Math.Sqrt(Math.PI);

        /// <summary>
        /// Gives the error function
        ///    Sqrt(PI) Exp(x0^2) Erf(x0,x1) = 2 * Exp(x0^2) * Integral(x0, x1, exp(-t^2) dt)
        /// </summary>
        /// <param name="x0">Lower bound on the ERF integral</param>
        /// <param name="x1">Upper bounc on the ERF integral</param>
        public static double ErfSPExpXSq(double x0, double x1)
        {
            var erf0 = ErfSPExpXSqWorker(x0);
            var erf1 = ErfSPExpXSqWorker(x1);
            var exp = Math.Exp(x0 * x0 - x1 * x1);

            var ret = exp * erf1.Item1 - erf0.Item1;

            if (erf0.Item2 != erf1.Item2)
            {
                ret += (erf1.Item2 - erf0.Item2) * SqrtPi * Math.Exp(x0 * x0);
            }

            return ret;
        }

        public static double Erf2(double x)
        {
            return Erf2(0, x);
        }

        /// <summary>
        /// Gives the error function
        ///     Erf(x0,x1) = 2/Sqrt(PI) * Integral(x0, x1, exp(-t^2) dt)
        /// </summary>
        /// <param name="x0">Lower bound on the ERF integral</param>
        /// <param name="x1">Upper bounc on the ERF integral</param>
        public static double Erf2(double x0, double x1)
        {
            return 2 / Math.Sqrt(Math.PI) * Integrate(x0, x1, t => Math.Exp(-t * t), 1000);
        }

        private static Tuple<double, int> ErfSPExpXSqWorker(double x)
        {
            if (x == 0.0) return new Tuple<double, int>(0.0, 0);

            if (x < 0)
            {
                var ret = ErfSPExpXSqWorker(-x);
                return new Tuple<double, int>(-ret.Item1, -ret.Item2);
            }

            // Using equations from http://www.mhtlab.uwaterloo.ca/courses/me755/web_chap2.pdf
            if (x <= 2.0)
            {
                return new Tuple<double, int>(ErfSPExpXSqWorkerSmallX(x), 0);
            }
            else
            {
                return new Tuple<double, int>(-ErfcSPWorkerLargeX(x), 1);
            }
        }

        public static double ErfSPExpXSqWorkerSmallX(double x)
        {
            // Using equations from http://www.mhtlab.uwaterloo.ca/courses/me755/web_chap2.pdf
            double a0 = 1.0;
            double sum = a0;
            double anm1 = a0;
            for (int n = 1; n < 16; n++)
            {
                double an = x * x * anm1 * 2 / (2 * n + 1.0);
                sum += an;
                anm1 = an;
            }
            return 2 * x * sum;
        }

        public static double ErfcSPWorkerLargeX(double x)
        {
            double contFracTerm = 0;
            for (int n = 16; n >= 1; n--)
            {
                contFracTerm = n / (2 * (x + contFracTerm));
            }
            return 1 / (x + contFracTerm);
        }
    }
}