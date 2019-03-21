// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using MountainView.Mesh;
using MountainView.Render;
using MountainView.SkyColor;
using System;

namespace SoftEngine
{
    public static class Device
    {
        // Interpolating the value between 2 vertices
        // min is the starting point, max the ending point
        // and gradient the % between the 2 points
        private static float Interpolate(float min, float max, float gradient)
        {
            return min + (max - min) * gradient;
        }

        // drawing line between 2 points from left to right
        // papb -> pcpd
        // pa, pb, pc, pd must then be sorted before
        private static void ProcessScanLine(
            RenderState state,
            int currentY,
            ref VertexProj va,
            ref VertexProj vb,
            ref VertexProj vc,
            ref VertexProj vd,
            Texture texture)
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

            var sqDistA = state.Camera.Position.SqDistBetween(ref va.WorldCoordinates);
            var sqDistB = state.Camera.Position.SqDistBetween(ref vb.WorldCoordinates);
            var sqDistC = state.Camera.Position.SqDistBetween(ref vc.WorldCoordinates);
            var sqDistD = state.Camera.Position.SqDistBetween(ref vd.WorldCoordinates);

            var sSqD = Interpolate(sqDistA, sqDistB, gradient1);
            var eSqD = Interpolate(sqDistC, sqDistD, gradient2);

            // Interpolating normals on Y
            var snx = Interpolate(va.Normal.X, vb.Normal.X, gradient1);
            var enx = Interpolate(vc.Normal.X, vd.Normal.X, gradient2);
            var sny = Interpolate(va.Normal.Y, vb.Normal.Y, gradient1);
            var eny = Interpolate(vc.Normal.Y, vd.Normal.Y, gradient2);
            var snz = Interpolate(va.Normal.Z, vb.Normal.Z, gradient1);
            var enz = Interpolate(vc.Normal.Z, vd.Normal.Z, gradient2);

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
                var nx = Interpolate(snx, enx, gradient);
                var ny = Interpolate(sny, eny, gradient);
                var nz = Interpolate(snz, enz, gradient);
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
                }
                else
                {
                    textureColor = MyColor.White;
                }

                state.PutPixel(x, currentY, z, textureColor, nx, ny, nz, u, v, sqD);
            }
        }

        private static void DrawTriangle(
            RenderState state,
            ref VertexProj v1,
            ref VertexProj v2,
            ref VertexProj v3,
            Texture texture,
            ref Vector3f buffv)
        {
            // Sorting the points in order to always have this order on screen p1, p2 & p3
            // with p1 always up (thus having the Y the lowest possible to be near the top screen)
            // then p2 between p1 & p3
            if (v1.Coordinates.Y > v2.Coordinates.Y) Swap(ref v1, ref v2);
            if (v2.Coordinates.Y > v3.Coordinates.Y) Swap(ref v2, ref v3);
            if (v1.Coordinates.Y > v2.Coordinates.Y) Swap(ref v1, ref v2);

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
        public static void RenderInto(RenderState state, params Mesh[] meshes)
        {
            Vector3f buffv = new Vector3f();
            GeoPolar3d polarA = new GeoPolar3d();
            GeoPolar3d polarB = new GeoPolar3d();
            GeoPolar3d polarC = new GeoPolar3d();
            Vector3f midAB = new Vector3f();
            Vector3f midAC = new Vector3f();
            Vector3f midBC = new Vector3f();
            GeoPolar3d polarMidAB = new GeoPolar3d();
            GeoPolar3d polarMidAC = new GeoPolar3d();
            GeoPolar3d polarMidBC = new GeoPolar3d();
            foreach (Mesh mesh in meshes)
            {
                for (int faceIndex = 0; faceIndex < mesh.Faces.Length; faceIndex++)
                {
                    var face = mesh.Faces[faceIndex];

                    state.TransformToPolar(ref mesh.Vertices[face.A].Coordinates, ref polarA);
                    state.TransformToPolar(ref mesh.Vertices[face.B].Coordinates, ref polarB);
                    state.TransformToPolar(ref mesh.Vertices[face.C].Coordinates, ref polarC);

                    // TODO: Skip this triangle if it contains the point we are at?

                    // Also need to maintain the triangle topology that can be distrupted by
                    // discontinuities in arctan.

                    Vector3f.Avg(ref mesh.Vertices[face.A].Coordinates, ref mesh.Vertices[face.B].Coordinates, ref midAB);
                    Vector3f.Avg(ref mesh.Vertices[face.A].Coordinates, ref mesh.Vertices[face.C].Coordinates, ref midAC);
                    Vector3f.Avg(ref mesh.Vertices[face.B].Coordinates, ref mesh.Vertices[face.C].Coordinates, ref midBC);
                    state.TransformToPolar(ref midAB, ref polarMidAB);
                    state.TransformToPolar(ref midAC, ref polarMidAC);
                    state.TransformToPolar(ref midBC, ref polarMidBC);

                    bool abOK = CheckBetween(polarA.Lat, polarMidAB.Lat, polarB.Lat);
                    bool acOK = CheckBetween(polarA.Lat, polarMidAC.Lat, polarC.Lat);
                    bool bcOK = CheckBetween(polarB.Lat, polarMidBC.Lat, polarC.Lat);
                    if (!(abOK && acOK && bcOK))
                    {
                        if (abOK) polarC.Lat += (polarC.Lat < polarA.Lat ? 1 : -1) * 2 * Math.PI;
                        if (acOK) polarB.Lat += (polarB.Lat < polarA.Lat ? 1 : -1) * 2 * Math.PI;
                        if (bcOK) polarA.Lat += (polarA.Lat < polarC.Lat ? 1 : -1) * 2 * Math.PI;
                    }

                    // Now check if any triangles need to be shifted by multiple of 2Pi to move into view
                    // First, shift neg
                    while (
                        polarA.Lat - 2 * Math.PI >= state.Camera.MinAngleRad ||
                        polarB.Lat - 2 * Math.PI >= state.Camera.MinAngleRad ||
                        polarC.Lat - 2 * Math.PI >= state.Camera.MinAngleRad)
                    {
                        polarC.Lat -= 2 * Math.PI;
                        polarB.Lat -= 2 * Math.PI;
                        polarA.Lat -= 2 * Math.PI;
                    }

                    while (
                        polarA.Lat <= state.Camera.MaxAngleRad ||
                        polarB.Lat <= state.Camera.MaxAngleRad ||
                        polarC.Lat <= state.Camera.MaxAngleRad)
                    {
                        var pixelA = state.Project(ref mesh.Vertices[face.A], ref polarA);
                        var pixelB = state.Project(ref mesh.Vertices[face.B], ref polarB);
                        var pixelC = state.Project(ref mesh.Vertices[face.C], ref polarC);

                        DrawTriangle(state, ref pixelA, ref pixelB, ref pixelC, mesh.Texture, ref buffv);

                        polarC.Lat += 2 * Math.PI;
                        polarB.Lat += 2 * Math.PI;
                        polarA.Lat += 2 * Math.PI;
                    }
                }
            }
        }

        private const double CheckBetweenEps = 0.00001;
        private static bool CheckBetween(double a, double ab, double b)
        {
            if (Math.Abs(a - b) < CheckBetweenEps) return Math.Abs(a - ab) < 2 * CheckBetweenEps;
            if (Math.Abs(a - ab) < CheckBetweenEps) return true;
            if (Math.Abs(b - ab) < CheckBetweenEps) return true;
            return (a < ab) == (ab < b);
        }

        public class RenderState
        {
            private readonly DirectBitmap Bmp;
            private readonly float[] DepthBuffer;
            private readonly Vector3f[] ns;
            private readonly Vector2f[] UVs;
            private readonly Vector2d?[][] latLons;
            private readonly float?[] DistSq;
            public readonly int Width;
            public readonly int Height;
            public Camera Camera { get; set; }

            public RenderState(DirectBitmap bmp)
            {
                Bmp = bmp;
                Width = bmp.Width;
                Height = bmp.Height;
                DepthBuffer = new float[Width * Height];
                UVs = new Vector2f[Width * Height];
                ns = new Vector3f[Width * Height];
                DistSq = new float?[Width * Height];

                // Clearing Depth Buffer
                for (var index = 0; index < DepthBuffer.Length; index++)
                {
                    DepthBuffer[index] = float.MaxValue;
                }

                latLons = new Vector2d?[Width][];
                for (int i = 0; i < Width; i++)
                {
                    latLons[i] = new Vector2d?[Height];
                }
            }

            // Project takes some 3D coordinates and transform them
            // in 2D coordinates using the transformation matrix
            // It also transform the same coordinates and the normal to the vertex
            // in the 3D world
            public VertexProj Project(ref Vertex vertex, ref GeoPolar3d polar)
            {
                double fovRad = Camera.MaxAngleRad - Camera.MinAngleRad;
                return new VertexProj
                {
                    Coordinates = new Vector3fProj(
                        (int)(Width * 0.5 - Width / fovRad * (polar.Lat - (Camera.MaxAngleRad + Camera.MinAngleRad) * 0.5)),
                        (int)(Height * 0.5 - Width / fovRad * polar.Lon),
                        (float)polar.Height),
                    Normal = vertex.Normal,
                    WorldCoordinates = vertex.Coordinates,
                    TextureCoordinates = vertex.TextureCoordinates
                };
            }

            public void TransformToPolar(ref Vector3f p, ref GeoPolar3d delta)
            {
                // Figure out angle in x-y plane.
                delta.Lat = Math.Atan2(p.X, p.Y);
                // Then the "height" angle.
                var xy = p.Y * p.Y + p.X * p.X;
                var dZ = p.Z - Camera.HeightOffset;
                delta.Lon = Math.Atan2(dZ, Math.Sqrt(xy));
                delta.Height = Math.Sqrt(xy + dZ * dZ);
            }

            // Called to put a pixel on screen at a specific X,Y coordinates
            public void PutPixel(int x, int y, float z, MyColor color,
                float nx, float ny, float nz,
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
                        Bmp.SetPixel(Width - 1 - x, Height - 1 - y, color);
                        UVs[index] = new Vector2f(u, v);
                        ns[index].X = nx;
                        ns[index].Y = ny;
                        ns[index].Z = nz;
                        DistSq[index] = distSq;
                    }
                }
            }

            //private void PutPixel(int x, int y, MyColor color)
            //{
            //    Bmp.SetPixel(Width - 1 - x, Height - 1 - y, color);
            //}

            //public float? GetDistSq(int x, int y)
            //{
            //    var index = ((Width - 1 - x) + y * Width);
            //    return DistSq[index];
            //}

            public DirectBitmap RenderLight(Vector3f light, float directLight, float ambientLight, Nishita skyColor)
            {
                var ret = new DirectBitmap(Width, Height);
                MyColor color = new MyColor();
                double fovRad = Camera.MaxAngleRad - Camera.MinAngleRad;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        var skyPt = new GeoPolar2d(
                            (Width * 0.5 - x) * fovRad / Width + -(Camera.MaxAngleRad + Camera.MinAngleRad) * 0.5,
                            (Height * 0.5 - y) * fovRad / Width);

                        var index = (Width - 1 - x) + y * Width;
                        var distSq = DistSq[index];
                        if (!distSq.HasValue)
                        {
                            color = skyColor.SkyColorAtPoint(Camera.HeightOffset, skyPt);
                        }
                        else
                        {
                            Bmp.GetPixel(Width - 1 - x, y, ref color);
                            ns[index].Normalize();
                            var dot = Math.Max(0, Vector3f.Dot(ref ns[index], ref light));
                            var l = dot * directLight;// + ambientLight;
                            var ndotl = l > 1.0f ? 1.0f : l < 0.0f ? 0.0f : l;

                            color = skyColor.SkyColorAtPointDist(Camera.HeightOffset, skyPt, Math.Sqrt(distSq.Value), color, dot *directLight, ambientLight);
                        }

                        ret.SetPixel(Width - 1 - x, Height - 1 - y, color);
                    }
                }

                return ret;
            }

            public void UpdateLatLons(Angle latLo, Angle latDelta, Angle lonLo, Angle lonDelta)
            {
                for (int i = 0; i < latLons.Length; i++)
                {
                    for (int j = 0; j < latLons[i].Length; j++)
                    {
                        int index = i + (Height - 1 - j) * Width;
                        var r = UVs[index];
                        if (r != null)
                        {
                            latLons[i][j] = new Vector2d(
                                latLo.DecimalDegree + latDelta.DecimalDegree * (1.0 - r.Y),
                                lonLo.DecimalDegree + lonDelta.DecimalDegree * r.X);
                            UVs[index] = null;
                        }
                    }
                }
            }

            public Vector2d?[][] GetLatLons()
            {
                return latLons;
            }
        }
    }
}