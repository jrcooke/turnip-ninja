﻿// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Render;

namespace SoftEngine
{
    public struct Face
    {
        public int A;
        public int B;
        public int C;

        public Vector3f Normal;
    }
}