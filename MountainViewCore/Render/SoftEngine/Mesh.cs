// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using MountainView.Mesh;
using MountainView.Render;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoftEngine
{
    public class Mesh
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

            var x = Faces
                .Select((p, i) => new[] { new { i, Ind = p.A }, new { i, Ind = p.B }, new { i, Ind = p.C } })
                .SelectMany(p => p)
                .GroupBy(p => p.Ind)
                .Select(p => new { VertInd = p.Key, TriInds = p.Select(q => q.i).ToArray() })
                .ToDictionary(p => p.VertInd, p => p.TriInds);
            foreach (int vertInd in x.Keys)
            {
                Vector3f.AvgAndNorm(x[vertInd].Select(p => Faces[p]).Select(p => p.Normal).ToArray(), ref Vertices[vertInd].Normal);
            }
        }

        public static Mesh GetMesh(DirectBitmap bm, FriendlyMesh mesh)
        {
            var ret = new Mesh(
                mesh.Vertices.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray(),
                mesh.VertexNormals.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray(),
                mesh.TriangleIndices);

            ret.Texture = new Texture(bm);

            for (int i = 0; i < mesh.VertexToImage.Length; i++)
            {
                var orig = mesh.VertexToImage[i];
                ret.Vertices[i].TextureCoordinates = new Vector2f((float)orig.X, (float)orig.Y);
            }

            return ret;
        }

        public static Mesh MakeCube()
        {
            var triangleIndices = new List<int>()
            {
                0, 1, 2,
                1, 2, 3,
                1, 3, 6,
                1, 5, 6,
                0, 1, 4,
                1, 4, 5,
                2, 3, 7,
                3, 6, 7,
                0, 2, 7,
                0, 4, 7,
                4, 5, 6,
                4, 6, 7,
            };

            var vertices = new List<Vector3f>
            {
                new Vector3f(-1, +1, +1),
                new Vector3f(+1, +1, +1),
                new Vector3f(-1, -1, +1),
                new Vector3f(+1, -1, +1),
                new Vector3f(-1, +1, -1),
                new Vector3f(+1, +1, -1),
                new Vector3f(+1, -1, -1),
                new Vector3f(-1, -1, -1),
            };

            var mesh = new Mesh(vertices.ToArray(), vertices.ToArray(), triangleIndices.ToArray());
            return mesh;

        }

        // Loading the JSON file
        public static Mesh LoadJSONFile(string fileName)
        {
            var materials = new Dictionary<string, Material>();
            var data = File.ReadAllText(fileName);
            dynamic jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(data);

            for (var materialIndex = 0; materialIndex < jsonObject.materials.Count; materialIndex++)
            {
                var material = new Material
                {
                    Name = jsonObject.materials[materialIndex].name.Value,
                    ID = jsonObject.materials[materialIndex].id.Value
                };
                if (jsonObject.materials[materialIndex].diffuseTexture != null)
                    material.DiffuseTextureName = jsonObject.materials[materialIndex].diffuseTexture.name.Value;

                materials.Add(material.ID, material);
            }

            //for (
            var meshIndex = 0; //meshIndex < jsonObject.meshes.Count; meshIndex++)
            {
                var verticesArray = jsonObject.meshes[meshIndex].vertices;
                // Faces
                var indicesArray = jsonObject.meshes[meshIndex].indices;

                var uvCount = jsonObject.meshes[meshIndex].uvCount.Value;
                var verticesStep = 1;

                // Depending of the number of texture's coordinates per vertex
                // we're jumping in the vertices array  by 6, 8 & 10 windows frame
                switch ((int)uvCount)
                {
                    case 0:
                        verticesStep = 6;
                        break;
                    case 1:
                        verticesStep = 8;
                        break;
                    case 2:
                        verticesStep = 10;
                        break;
                }

                // the number of interesting vertices information for us
                var verticesCount = verticesArray.Count / verticesStep;
                // number of faces is logically the size of the array divided by 3 (A, B, C)
                var facesCount = indicesArray.Count / 3;
                var mesh = new Mesh(jsonObject.meshes[meshIndex].name.Value, verticesCount, facesCount);

                // Filling the Vertices array of our mesh first
                for (var index = 0; index < verticesCount; index++)
                {
                    var x = (float)verticesArray[index * verticesStep].Value;
                    var y = (float)verticesArray[index * verticesStep + 1].Value;
                    var z = (float)verticesArray[index * verticesStep + 2].Value;
                    // Loading the vertex normal exported by Blender
                    var nx = (float)verticesArray[index * verticesStep + 3].Value;
                    var ny = (float)verticesArray[index * verticesStep + 4].Value;
                    var nz = (float)verticesArray[index * verticesStep + 5].Value;

                    var norm = new Vector3f(nx, ny, nz);
                    norm.Normalize();
                    mesh.Vertices[index] = new Vertex
                    {
                        Coordinates = new Vector3f(x, y, z),
                        Normal = norm,
                    };

                    if (uvCount > 0)
                    {
                        // Loading the texture coordinates
                        float u = (float)verticesArray[index * verticesStep + 6].Value;
                        float v = (float)verticesArray[index * verticesStep + 7].Value;
                        mesh.Vertices[index].TextureCoordinates = new Vector2f(u, v);
                    }
                }

                // Then filling the Faces array
                for (var index = 0; index < facesCount; index++)
                {
                    var a = (int)indicesArray[index * 3].Value;
                    var b = (int)indicesArray[index * 3 + 1].Value;
                    var c = (int)indicesArray[index * 3 + 2].Value;
                    mesh.Faces[index] = new Face { A = a, B = b, C = c };
                }

                if (uvCount > 0)
                {
                    // Texture
                    var meshTextureID = jsonObject.meshes[meshIndex].materialId.Value;
                    var meshTextureName = materials[meshTextureID].DiffuseTextureName;
                    mesh.Texture = new Texture(meshTextureName);
                }

                for (int faceIndex = 0; faceIndex < mesh.Faces.Length; faceIndex++)
                {
                    Vector3f.AvgAndNorm(
                        ref mesh.Vertices[mesh.Faces[faceIndex].A].Normal,
                        ref mesh.Vertices[mesh.Faces[faceIndex].B].Normal,
                        ref mesh.Vertices[mesh.Faces[faceIndex].C].Normal,
                        ref mesh.Faces[faceIndex].Normal);
                };

                return mesh;
            }
            return null;
        }

    }
}
