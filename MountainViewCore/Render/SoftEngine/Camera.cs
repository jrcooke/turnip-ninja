﻿// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Render;

namespace SoftEngine
{
    public struct Camera
    {
        public Vector3f Position;
        public double MaxAngleRad;
        public double MinAngleRad;
        private double heightOffset;
        public double HeightOffset
        {
            get
            {
                return heightOffset;
            }
            set
            {
                heightOffset = value;
                Position = new Vector3f(0, 0, (float)heightOffset);
            }
        }

        public double FovRad
        {
            get
            {
                return MaxAngleRad - MinAngleRad;
            }
        }

        public double AvgAngleRad
        {
            get
            {
                return (MaxAngleRad + MinAngleRad) * 0.5;
            }
        }
    }
}