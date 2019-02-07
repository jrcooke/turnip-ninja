// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Mesh;
using MountainView.Render;
using System;
using System.Linq;

namespace SoftEngine
{
    public class Mesh : IDisposable
    {
        public Vertex[] Vertices { get; set; }
        public Face[] Faces { get; set; }
        public Texture Texture { get; set; }

        public Mesh(int verticesCount, int facesCount)
        {
            Vertices = new Vertex[verticesCount];
            Faces = new Face[facesCount];
        }

        public Mesh(Vector3f[] vertices, Vector3f[] normals, int[] triangleIndices)
        {
            Vertices = new Vertex[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                normals[i].Normalize();
                Vertices[i] = new Vertex()
                {
                    Coordinates = vertices[i],
                    Normal = normals[i],
                };
            }

            Faces = new Face[triangleIndices.Length / 3];
            int faceIndex = 0;
            int triIndex = 0;

            Vector3f buff1 = new Vector3f();
            Vector3f buff2 = new Vector3f();
            while (faceIndex < Faces.Length)
            {
                Vector3f n = new Vector3f();

                // Get a hint for the right directions
                Vector3f.AvgAndNorm(
                    ref normals[triangleIndices[triIndex + 0]],
                    ref normals[triangleIndices[triIndex + 1]],
                    ref normals[triangleIndices[triIndex + 2]],
                    ref n);

                // Then compute based on crossing the edges

                Vector3f.SubAndNorm(
                    ref vertices[triangleIndices[triIndex + 0]],
                    ref vertices[triangleIndices[triIndex + 1]],
                    ref buff1);
                Vector3f.SubAndNorm(
                    ref vertices[triangleIndices[triIndex + 0]],
                    ref vertices[triangleIndices[triIndex + 2]],
                    ref buff2);

                Vector3f newN = new Vector3f();
                Vector3f.Cross(ref buff1, ref buff2, ref newN);
                newN.Normalize();

                if (Vector3f.Dot(ref n, ref newN) < 0.0f)
                {
                    newN.X *= -1;
                    newN.Y *= -1;
                    newN.Z *= -1;
                }

                Faces[faceIndex++] = new Face()
                {
                    A = triangleIndices[triIndex++],
                    B = triangleIndices[triIndex++],
                    C = triangleIndices[triIndex++],
                    Normal = newN,
                };
            }

            while (faceIndex < Faces.Length)
            {
                Vector3f n = new Vector3f();

                // Get a hint for the right directions
                Vector3f.AvgAndNorm(
                    ref normals[triangleIndices[triIndex + 0]],
                    ref normals[triangleIndices[triIndex + 1]],
                    ref normals[triangleIndices[triIndex + 2]],
                    ref n);

                // Then compute based on crossing the edges

                Vector3f.SubAndNorm(
                    ref vertices[triangleIndices[triIndex + 0]],
                    ref vertices[triangleIndices[triIndex + 1]],
                    ref buff1);
                Vector3f.SubAndNorm(
                    ref vertices[triangleIndices[triIndex + 0]],
                    ref vertices[triangleIndices[triIndex + 2]],
                    ref buff2);

                Vector3f newN = new Vector3f();
                Vector3f.Cross(ref buff1, ref buff2, ref newN);
                newN.Normalize();

                if (Vector3f.Dot(ref n, ref newN) < 0.0f)
                {
                    newN.X *= -1;
                    newN.Y *= -1;
                    newN.Z *= -1;
                }

                Faces[faceIndex++] = new Face()
                {
                    A = triangleIndices[triIndex++],
                    B = triangleIndices[triIndex++],
                    C = triangleIndices[triIndex++],
                    Normal = newN,
                };
            }

            //var x = Faces
            //    .Select((p, i) => new[] { new { i, Ind = p.A }, new { i, Ind = p.B }, new { i, Ind = p.C } })
            //    .SelectMany(p => p)
            //    .GroupBy(p => p.Ind)
            //    .Select(p => new { VertInd = p.Key, TriInds = p.Select(q => q.i).ToArray() })
            //    .ToDictionary(p => p.VertInd, p => p.TriInds);
            //foreach (int vertInd in x.Keys)
            //{
            //    Vector3f.AvgAndNorm(x[vertInd].Select(p => Faces[p]).Select(p => p.Normal).ToArray(), ref Vertices[vertInd].Normal);
            //}
        }

        public static Mesh GetMesh(FriendlyMesh mesh)
        {
            var ret = new Mesh(
                mesh.Vertices.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray(),
                mesh.VertexNormals.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray(),
                mesh.TriangleIndices)
            {
                Texture = new Texture(mesh.ImageData)
            };

            for (int i = 0; i < mesh.VertexToImage.Length; i++)
            {
                var orig = mesh.VertexToImage[i];
                ret.Vertices[i].TextureCoordinates = new Vector2f(Clamp(orig.X), Clamp(orig.Y));
            }

            return ret;
        }

        private static float Clamp(double d)
        {
            var v = (float)d;
            return v < 0.0f ? 0.0f : v > 1.0f ? 1.0f : v;
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Texture != null)
                    {
                        Texture.Dispose();
                    }
                }

                Vertices = null;
                Faces = null;
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
