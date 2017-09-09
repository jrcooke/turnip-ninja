using MountainView.Base;

namespace MountainView.ChunkManagement
{
    public class ChunkMetadata
    {
        public int LatSteps { get; private set; }
        public int LonSteps { get; private set; }
        public Angle LatLo { get; private set; }
        public Angle LonLo { get; private set; }
        public Angle LatHi { get; private set; }
        public Angle LonHi { get; private set; }
        public Angle LonDelta { get; private set; }
        public Angle LatDelta { get; private set; }
        public Angle PixelSizeLat { get; private set; }
        public Angle PixelSizeLon { get; private set; }

        public ChunkMetadata(int latSteps, int lonSteps, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi)
        {
            this.LatSteps = latSteps;
            this.LonSteps = lonSteps;
            this.LatLo = latLo;
            this.LonLo = lonLo;
            this.LatHi = latHi;
            this.LonHi = lonHi;
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.PixelSizeLat = Angle.Divide(LatDelta, LatSteps - 1);
            this.PixelSizeLon = Angle.Divide(LonDelta, LonSteps - 1);
        }

        protected Angle GetLat(int i)
        {
            return Angle.Add(LatLo, Angle.Divide(Angle.Multiply(LatDelta, i), LatSteps));
        }

        protected Angle GetLon(int j)
        {
            return Angle.Add(LonLo, Angle.Divide(Angle.Multiply(LonDelta, LonSteps - 1 - j), LonSteps));
        }

        protected int GetLatIndex(Angle lat)
        {
            var curLatDelta = Angle.Subtract(lat, LatLo);
            return Angle.Divide(curLatDelta, PixelSizeLat);
        }

        protected int GetLonIndex(Angle lon)
        {
            var curLonDelta = Angle.Subtract(lon, LonLo);
            return LonSteps - 1 - Angle.Divide(curLonDelta, PixelSizeLon);
        }

        public bool Disjoint(ChunkMetadata that)
        {
            return
                (this.LatLo.DecimalDegree > that.LatHi.DecimalDegree) ||
                (this.LatHi.DecimalDegree < that.LatLo.DecimalDegree) ||
                (this.LonLo.DecimalDegree > that.LonHi.DecimalDegree) ||
                (this.LonHi.DecimalDegree < that.LonLo.DecimalDegree);
        }

        public override string ToString()
        {
            return this.LatLo.ToLatString() + "," + this.LonLo.ToLonString() + " to " + this.LatHi.ToLatString() + "," + this.LonHi.ToLonString();
        }
    }
}
