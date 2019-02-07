using MountainView.Mesh;
using MountainView.Render;

namespace MountainView.Base
{
    public class Config
    {
        public double HeightOffset { get; set; } = 0.001f;
        public double R { get; set; } = 100000;
        public int Width { get; set; }
        public int Height { get; set; }
        public Angle Lat { get; set; }
        public Angle Lon { get; set; }

        /// <summary>
        /// 0 is facing north. 90 degrees is facing east.
        /// </summary>
        public double MinAngleDec { get; set; }

        /// <summary>
        /// 0 is facing north. 90 degrees is facing east.
        /// </summary>
        public double MaxAngleDec { get; set; }

        public int MaxZoom { get; set; } = 6;
        public int MinZoom { get; set; } = 3;
        public bool UseHaze { get; set; } = true;
        public Vector3f Light { get; set; }
        public float DirectLight { get; set; }
        public float AmbientLight { get; set; }
        public GeoPolar2d HomePoint { get { return new GeoPolar2d(Lat, Lon); } }

        public static Config Home()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.6867797),
                Lon = Angle.FromDecimalDegrees(-122.2907541),
                MinAngleDec = 85.0,
                MaxAngleDec = 95.0,
            };
        }
    }
}
