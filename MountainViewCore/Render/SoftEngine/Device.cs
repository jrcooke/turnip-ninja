﻿// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using MountainView.Render;
using MountainViewCore.Base;
using SharpDX;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SoftEngine
{
    public class Device
    {
        public Camera Camera { get; set; }
        public Vector3f Light { get; set; }
        public float DirectLight { get; set; }
        public float AmbientLight { get; set; }
        public Collection<Mesh> Meshes { get; set; } = new Collection<Mesh>();

        public Device()
        {
        }

        // Interpolating the value between 2 vertices
        // min is the starting point, max the ending point
        // and gradient the % between the 2 points
        private float Interpolate(float min, float max, float gradient)
        {
            return min + (max - min) * gradient;
        }

        // Project takes some 3D coordinates and transform them
        // in 2D coordinates using the transformation matrix
        // It also transform the same coordinates and the normal to the vertex
        // in the 3D world
        private VertexProj Project(RenderState state, ref Vertex vertex, float FovRad, double lookAngle)
        {
            // transforming the coordinates into 2D space
            Vector3f point2d = new Vector3f();
            //            Matrix.TransformCoordinate(ref vertex.Coordinates, ref transMat, ref point2d);

            //// The transformed coordinates will be based on coordinate system
            //// starting on the center of the screen. But drawing on screen normally starts
            //// from top left. We then need to transform them again to have x:0, y:0 on top left.
            //point2d.X = +point2d.X * state.Width + state.Width / 2.0f;
            //point2d.Y = -point2d.Y * state.Height + state.Height / 2.0f;

            // Figure out angle in x-y plane.
            var theta = Math.Atan2(vertex.Coordinates.X, vertex.Coordinates.Y) - lookAngle;
            // Then the "height" angle.
            var xz = Math.Sqrt(vertex.Coordinates.Y * vertex.Coordinates.Y + vertex.Coordinates.X * vertex.Coordinates.X);
            var phi = Math.Atan2(vertex.Coordinates.Z, xz);

            theta *= state.Width / Camera.FovRad;
            phi *= state.Width / Camera.FovRad;

            //  Debug.WriteLine("A: " + point2d.X + "\t " + point2d.Y);

            point2d.X = (float)(-theta + state.Width / 2.0f);
            point2d.Y = (float)(-phi + state.Height / 2.0f);
            point2d.Z = Vector3f.Dot(ref vertex.Coordinates, ref vertex.Coordinates);

            //   Debug.WriteLine("B: " + point2d.X + "\t " + point2d.Y);

            return new VertexProj
            {
                Coordinates = new Vector3fProj((int)point2d.X, (int)point2d.Y, point2d.Z),
                Normal = vertex.Normal,
                WorldCoordinates = vertex.Coordinates,
                TextureCoordinates = vertex.TextureCoordinates
            };
        }

        // Compute the cosine of the angle between the light vector and the normal vector
        // Returns a value between 0 and 1
        private float ComputeNDotL(ref Vector3f vertex, ref Vector3f normal, ref Vector3f buffv)
        {
            var lightPos = Light;
            Vector3f.SubAndNorm(ref lightPos, ref vertex, ref buffv);
            var dot = Math.Max(0, Vector3f.Dot(ref normal, ref buffv));
            var ret = dot * DirectLight + AmbientLight;
            return ret > 1.0f ? 1.0f : ret < 0.0f ? 0.0f : ret;
        }

        // drawing line between 2 points from left to right
        // papb -> pcpd
        // pa, pb, pc, pd must then be sorted before
        private void ProcessScanLine(RenderState state, int currentY, ref VertexProj va, ref VertexProj vb, ref VertexProj vc, ref VertexProj vd, Texture texture)
        {
            Vector3fProj pa = va.Coordinates;
            Vector3fProj pb = vb.Coordinates;
            Vector3fProj pc = vc.Coordinates;
            Vector3fProj pd = vd.Coordinates;

            // Thanks to current Y, we can compute the gradient to compute others values like
            // the starting X (sx) and ending X (ex) to draw between
            // if pa.Y == pb.Y or pc.Y == pd.Y, gradient is forced to 1
            var gradient1 = Math.Abs(pa.Y - pb.Y) > 0.0001 ? ((currentY - pa.Y) * 1.0f / (pb.Y - pa.Y)) : 1;
            var gradient2 = Math.Abs(pc.Y - pd.Y) > 0.0001 ? ((currentY - pc.Y) * 1.0f / (pd.Y - pc.Y)) : 1;

            int sx = (int)Interpolate(pa.X, pb.X, gradient1);
            int ex = (int)Interpolate(pc.X, pd.X, gradient2);
            if (ex < sx) return;

            // starting Z & ending Z
            float z1 = Interpolate(pa.Z, pb.Z, gradient1);
            float z2 = Interpolate(pc.Z, pd.Z, gradient2);

            var sqDistA = Camera.Position.SqDistBetween(ref va.WorldCoordinates);
            var sqDistB = Camera.Position.SqDistBetween(ref vb.WorldCoordinates);
            var sqDistC = Camera.Position.SqDistBetween(ref vc.WorldCoordinates);
            var sqDistD = Camera.Position.SqDistBetween(ref vd.WorldCoordinates);

            var sSqD = Interpolate(sqDistA, sqDistB, gradient1);
            var eSqD = Interpolate(sqDistC, sqDistD, gradient2);

            // Interpolating normals on Y
            var snl = Interpolate(va.NdotL, vb.NdotL, gradient1);
            var enl = Interpolate(vc.NdotL, vd.NdotL, gradient2);

            float su = 0;
            float eu = 0;
            float sv = 0;
            float ev = 0;

            // Interpolating texture coordinates on Y
            if (texture != null)
            {
                su = Interpolate(va.TextureCoordinates.X, vb.TextureCoordinates.X, gradient1);
                eu = Interpolate(vc.TextureCoordinates.X, vd.TextureCoordinates.X, gradient2);
                sv = Interpolate(va.TextureCoordinates.Y, vb.TextureCoordinates.Y, gradient1);
                ev = Interpolate(vc.TextureCoordinates.Y, vd.TextureCoordinates.Y, gradient2);
            }

            // drawing a line from left (sx) to right (ex), but only for what is visible on screen.
            for (int x = Math.Max(sx, 0); x < Math.Min(ex, state.Width); x++)
            {
                float gradient = (x - sx) / (float)(ex - sx);

                // Interpolating Z, normal and texture coordinates on X
                var z = Interpolate(z1, z2, gradient);
                var ndotl = Interpolate(snl, enl, gradient);
                var u = (Interpolate(su, eu, gradient));
                var v = (Interpolate(sv, ev, gradient));

                var sqD = Interpolate(sSqD, eSqD, gradient);

                // changing the native color value using the cosine of the angle
                // between the light vector and the normal vector
                // and the texture color
                MyColor textureColor = new MyColor();
                if (texture != null)
                {
                    texture.GetPixel(u, v, ref textureColor);
                    textureColor.ScaleSelf(ndotl);
                }
                else
                {
                    textureColor.WhiteScale(ndotl);
                }

                state.PutPixel(x, currentY, z, textureColor, u, v, sqD);
            }
        }

        private void DrawTriangle(RenderState state, ref VertexProj v1, ref VertexProj v2, ref VertexProj v3, Texture texture, ref Vector3f buffv)
        {
            // Sorting the points in order to always have this order on screen p1, p2 & p3
            // with p1 always up (thus having the Y the lowest possible to be near the top screen)
            // then p2 between p1 & p3
            if (v1.Coordinates.Y > v2.Coordinates.Y) Swap(ref v1, ref v2);
            if (v2.Coordinates.Y > v3.Coordinates.Y) Swap(ref v2, ref v3);
            if (v1.Coordinates.Y > v2.Coordinates.Y) Swap(ref v1, ref v2);

            // computing the cos of the angle between the light vector and the normal vector
            // it will return a value between 0 and 1 that will be used as the intensity of the color
            v1.NdotL = ComputeNDotL(ref v1.WorldCoordinates, ref v1.Normal, ref buffv);
            v2.NdotL = ComputeNDotL(ref v2.WorldCoordinates, ref v2.Normal, ref buffv);
            v3.NdotL = ComputeNDotL(ref v3.WorldCoordinates, ref v3.Normal, ref buffv);

            // computing lines' directions
            // http://en.wikipedia.org/wiki/Slope
            // Computing slopes
            Vector3fProj p1 = v1.Coordinates;
            Vector3fProj p2 = v2.Coordinates;
            Vector3fProj p3 = v3.Coordinates;

            // When the slope is zero, it doesn't give good
            // information. Then have to check manually
            //
            //          P1                    P1
            // First    | \       Second     / |
            // branch   |  P2     branch   P2  |
            // is for   | /       is for     \ |
            //          P3                    P3
            // However , when dP1P2 == null
            //          P1--P2            P2--P1
            // First    |   /     Second   \   |
            // branch   |  /      branch    \  |
            // is for   | /       is for     \ |
            //          P3                    P3
            // And dP1P3 cant be null without DP1P2 null first.

            bool useFirst;
            if (p1.Y == p2.Y)
            {
                useFirst = p1.X < p2.X;
            }
            else
            {
                float invSlopeP1P2 = (p2.X - p1.X) * 1.0f / (p2.Y - p1.Y);
                float invSlopeP1P3 = (p3.X - p1.X) * 1.0f / (p3.Y - p1.Y);
                useFirst = invSlopeP1P2 > invSlopeP1P3;
            }

            for (var y = Math.Max(0, p1.Y); y <= Math.Min(state.Height, p3.Y); y++)
            {
                if (useFirst)
                {
                    if (y < p2.Y)
                        ProcessScanLine(state, y, ref v1, ref v3, ref v1, ref v2, texture);
                    else
                        ProcessScanLine(state, y, ref v1, ref v3, ref v2, ref v3, texture);
                }
                else
                {
                    if (y < p2.Y)
                        ProcessScanLine(state, y, ref v1, ref v2, ref v1, ref v3, texture);
                    else
                        ProcessScanLine(state, y, ref v2, ref v3, ref v1, ref v3, texture);
                }
            }
        }

        private static void Swap(ref VertexProj v1, ref VertexProj v2)
        {
            var temp = v2;
            v2 = v1;
            v1 = temp;
        }

        // The main method of the engine that re-compute each vertex projection
        // during each frame
        public RenderState RenderInto(DirectBitmap bmp, float backToMeters, bool useHaze)
        {
            RenderState state = new RenderState(bmp, backToMeters, useHaze);

            // To understand this part, please read the prerequisites resources
            var viewMatrix = Matrix.LookAtLH(Camera.Position, Camera.Target, Camera.UpDirection);

            var projectionMatrix = Matrix.PerspectiveFovLH(
                Camera.FovRad,
                (float)state.Width / state.Height,
                0.01f,
                1.0f);

            Vector3f buffv = new Vector3f();
            Vector3f cameraTarget = Camera.Target;
            Vector3f cameraPos = Camera.Position;
            Vector3f lookDir = new Vector3f();
            Vector3f.SubAndNorm(ref cameraTarget, ref cameraPos, ref lookDir);
            var lookAngle = Math.Atan2(lookDir.X, lookDir.Y);

            foreach (Mesh mesh in Meshes.ToArray())
            {
                var transformMatrix = Matrix.Mul(viewMatrix, projectionMatrix);
                for (int faceIndex = 0; faceIndex < mesh.Faces.Length; faceIndex++)
                {
                    var face = mesh.Faces[faceIndex];

                    // First, check to see if all the vertices are *behind* the camera.
                    // We don't care about those.
                    if (IsBehind(ref mesh.Vertices[face.A].Coordinates, ref cameraPos, ref lookDir, ref buffv) ||
                        IsBehind(ref mesh.Vertices[face.B].Coordinates, ref cameraPos, ref lookDir, ref buffv) ||
                        IsBehind(ref mesh.Vertices[face.C].Coordinates, ref cameraPos, ref lookDir, ref buffv))
                    {
                        // It is possible we will get a gap when the triangle straddles the
                        // viewer's plane, but should be fine in most cases.
                        continue;
                    }

                    // This appears to be over aggressive, blocking triangles from being render that shoudl be
                    //// Face-back culling
                    //var transformedNormalZ =
                    //    face.Normal.X * viewMatrix.M13 +
                    //    face.Normal.Y * viewMatrix.M23 +
                    //    face.Normal.Z * viewMatrix.M33;
                    //if (transformedNormalZ < 0.0f)
                    {
                        // Render this face
                        var pixelA = Project(state, ref mesh.Vertices[face.A], Camera.FovRad, lookAngle);
                        var pixelB = Project(state, ref mesh.Vertices[face.B], Camera.FovRad, lookAngle);
                        var pixelC = Project(state, ref mesh.Vertices[face.C], Camera.FovRad, lookAngle);

                        DrawTriangle(state, ref pixelA, ref pixelB, ref pixelC, mesh.Texture, ref buffv);
                    }
                }
            }

            return state;
        }

        private bool IsBehind(ref Vector3f vertex, ref Vector3f cameraPos, ref Vector3f lookDir, ref Vector3f buffv)
        {
            Vector3f.SubAndNorm(ref vertex, ref cameraPos, ref buffv);
            return Vector3f.Dot(ref lookDir, ref buffv) < 0.00001;
        }

        public class RenderState
        {
            private readonly DirectBitmap Bmp;
            private readonly float[] DepthBuffer;
            private readonly Vector2f[] UVs;
            private readonly float?[] DistSq;
            private readonly float backToMeters;
            private readonly bool useHaze;
            public readonly int Width;
            public readonly int Height;

            public RenderState(DirectBitmap bmp, float backToMeters, bool useHaze)
            {
                Bmp = bmp;
                this.backToMeters = backToMeters;
                this.useHaze = useHaze;
                Width = bmp.Width;
                Height = bmp.Height;
                DepthBuffer = new float[Width * Height];
                UVs = new Vector2f[Width * Height];
                DistSq = new float?[Width * Height];

                // Clearing Back Buffer
                bmp.SetAllPixels(new MyColor(0, 0, 0, 0));

                // Clearing Depth Buffer
                for (var index = 0; index < DepthBuffer.Length; index++)
                {
                    DepthBuffer[index] = float.MaxValue;
                }
            }

            // Called to put a pixel on screen at a specific X,Y coordinates
            public void PutPixel(int x, int y, float z, MyColor color,
                float u, float v,
                float distSq)
            {
                // Clipping what's visible on screen
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    var index = ((Width - 1 - x) + y * Width);
                    if (DepthBuffer[index] > z)
                    {
                        DepthBuffer[index] = z;

                        if (useHaze)
                        {
                            double clearWeight = 0.2 + 0.8 / (1.0 + distSq * backToMeters * backToMeters * 1.0e-10);
                            color.R = (byte)(int)(color.R * clearWeight + View.skyColor.R * (1 - clearWeight));
                            color.G = (byte)(int)(color.G * clearWeight + View.skyColor.G * (1 - clearWeight));
                            color.B = (byte)(int)(color.B * clearWeight + View.skyColor.B * (1 - clearWeight));
                        }

                        Bmp.SetPixel(Width - 1 - x, Height - 1 - y, color);
                        UVs[index] = new Vector2f(u, v);
                        DistSq[index] = distSq;
                    }
                }
            }

            public Vector2f GetUV(int x, int y)
            {
                var index = ((Width - 1 - x) + y * Width);
                return UVs[index];
            }

            public float? GetDistSq(int x, int y)
            {
                var index = ((Width - 1 - x) + y * Width);
                return DistSq[index];
            }
        }
    }
}