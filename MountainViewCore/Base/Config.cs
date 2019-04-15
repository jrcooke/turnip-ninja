using MountainView.Mesh;
using MountainView.Render;
using System;

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

        private DateTimeOffset localTime;
        public DateTimeOffset LocalTime
        {
            get { return localTime; }
            set
            {
                localTime = value;
                SunPos = Utils.GetSunPosition(Lat, Lon, localTime);
                if (SunPos.Lon.DecimalDegree > 0)
                {
                    Light = SunPos.GetUnitVector();
                    DirectLight = 3.5f;
                }
                else
                {
                    DirectLight = 0.0f;
                }

                // https://en.wikipedia.org/wiki/Sky_brightness#/media/File:Illuminated-arimass.png
                var amb = 0.35 + 0.35 / 6.0 * SunPos.Lon.DecimalDegree;

                AmbientLight = (float)(0.1 + 0.4 * (amb > 1 ? 1.0 : amb < 0 ? 0.0 : amb));
            }
        }

        /// <summary>
        /// Direction of where the sun shines from
        /// (+1, 0,0) is from East.
        /// (-1, 0,0) is from West.
        /// ( 0,+1,0) is from north.
        /// ( 0,-1,0) is from south
        /// ( 0, 0,1) is zenith.
        /// Set by LocalTime
        /// </summary>
        public Vector3f Light { get; private set; }

        public GeoPolar2d SunPos { get; private set; }

        /// <summary>
        /// Set by LocalTime
        /// </summary>
        public float DirectLight { get; private set; }

        /// <summary>
        /// Set by LocalTime
        /// </summary>
        public float AmbientLight { get; private set; }
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
