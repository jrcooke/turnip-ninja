using Microsoft.WindowsAzure.Storage;
using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MountainView.Mesh
{
    public class Meshes
    {
        private static readonly string cachedFileTemplate = "{0}.v10.{1}";
        private static readonly string cachedFileContainer = "mapv8";

        private readonly string fileExt = "mdata";
        private readonly string description = "Meshes";
        private readonly int pixelDataSize = 4;
        public int SourceDataZoom { get; private set; } = 4;
        private readonly ConcurrentDictionary<long, string> filenameCache = new ConcurrentDictionary<long, string>();

        private static Lazy<Meshes> current = new Lazy<Meshes>(() => new Meshes());

        public static Meshes Current
        {
            get
            {
                return current.Value;
            }
        }

        private Meshes()
        {
        }

        public async Task<FriendlyMesh> GetData(StandardChunkMetadata template, TraceListener log)
        {
            var computedChunk = await GetComputedChunk(template, log);
            string fileName = computedChunk.Item1;
            FriendlyMesh ret = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                log?.WriteLine("Cached " + description + " chunk (" + template.ToString() + ") file exists: " + fileName);
                return computedChunk.Item2;
            }

            log?.WriteLine("Cached " + description + " chunk (" + template.ToString() + ") file does not exist: " + fileName + ", so starting generation...");

            ChunkHolder<float> pixels2 = null;
            try
            {
                pixels2 = await Heights.Current.GetData(template, log);
            }
            catch
            {
            }

            if (pixels2 == null)
            {
                //                throw new InvalidOperationException("Source heights not found for chunk " + template.ToString());
                return null;
            }

            ret = new FriendlyMesh(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                pixels2.Data, log);

            await WriteChunk(ret, fileName, log);
            log?.WriteLine("Finished generation of " + description + " cached chunk (" + template.ToString() + ") file: " + fileName);
            return ret;
        }

        private async Task<Tuple<string, FriendlyMesh>> GetComputedChunk(StandardChunkMetadata template, TraceListener log)
        {
            string filename = GetShortFilename(template);
            Tuple<string, FriendlyMesh> ret = new Tuple<string, FriendlyMesh>(GetFullFileName(template, filename), null);
            try
            {
                using (BlobHelper.DeletableFileStream ms = await BlobHelper.TryGetStreamAsync(cachedFileContainer, ret.Item1, log))
                {
                    if (ms != null)
                    {
                        ret = new Tuple<string, FriendlyMesh>(ret.Item1, ReadChunk(ms, template));
                    }
                }
            }
            catch (StorageException stex)
            {
                if (stex.RequestInformation.HttpStatusCode == 404)
                {
                    log?.WriteLine("Blob not found;");
                }
                else
                {
                    throw;
                }
            }

            return ret;
        }

        private string GetShortFilename(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = GetBaseFileName(template);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            return filename;
        }

        private string GetFullFileName(StandardChunkMetadata template, string filename)
        {
            return string.Format(cachedFileTemplate, filename, fileExt);
        }

        private static string GetBaseFileName(StandardChunkMetadata template)
        {
            return string.Format("{0}{1}{2:D2}",
                template.LatLo.ToLatString(),
                template.LonLo.ToLonString(),
                template.ZoomLevel);
        }

        private async Task WriteChunk(FriendlyMesh ret, string fileName, TraceListener log)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                int vertexCount = ret.Vertices.Length;
                int triangleIndexCount = ret.TriangleIndices.Length;
                int edgeIndicesCount = ret.EdgeIndices.Length;

                stream.Write(BitConverter.GetBytes(vertexCount), 0, 4);
                stream.Write(BitConverter.GetBytes(triangleIndexCount), 0, 4);
                stream.Write(BitConverter.GetBytes(edgeIndicesCount), 0, 4);

                for (int i = 0; i < vertexCount; i++)
                {
                    WriteFloat(stream, ret.Vertices[i].X);
                    WriteFloat(stream, ret.Vertices[i].Y);
                    WriteFloat(stream, ret.Vertices[i].Z);
                }

                for (int i = 0; i < triangleIndexCount; i++)
                {
                    stream.Write(BitConverter.GetBytes(ret.TriangleIndices[i]), 0, 4);
                }

                for (int i = 0; i < edgeIndicesCount; i++)
                {
                    stream.Write(BitConverter.GetBytes(ret.EdgeIndices[i]), 0, 4);
                }

                for (int i = 0; i < vertexCount; i++)
                {
                    WriteFloat(stream, ret.VertexNormals[i].X);
                    WriteFloat(stream, ret.VertexNormals[i].Y);
                    WriteFloat(stream, ret.VertexNormals[i].Z);
                }

                for (int i = 0; i < vertexCount; i++)
                {
                    WriteFloat(stream, ret.VertexToImage[i].X);
                    WriteFloat(stream, ret.VertexToImage[i].Y);
                }

                for (int i = 0; i < 4; i++)
                {
                    WriteFloat(stream, ret.Corners[i].X);
                    WriteFloat(stream, ret.Corners[i].Y);
                    WriteFloat(stream, ret.Corners[i].Z);
                }

                stream.Position = 0;
                await BlobHelper.WriteStream(cachedFileContainer, fileName, stream, log);
            }
        }

        private FriendlyMesh ReadChunk(BlobHelper.DeletableFileStream stream, StandardChunkMetadata template)
        {
            byte[] buffer = new byte[Math.Max(4, pixelDataSize)];

            stream.Read(buffer, 0, 4);
            int vertexCount = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            int triangleIndexCount = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            int edgeIndicesCount = BitConverter.ToInt32(buffer, 0);

            var ret = new FriendlyMesh(
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                vertexCount, triangleIndexCount, edgeIndicesCount);

            for (int i = 0; i < vertexCount; i++)
            {
                ret.Vertices[i].X = ReadFloat(stream, buffer);
                ret.Vertices[i].Y = ReadFloat(stream, buffer);
                ret.Vertices[i].Z = ReadFloat(stream, buffer);
            }

            for (int i = 0; i < triangleIndexCount; i++)
            {
                stream.Read(buffer, 0, 4);
                ret.TriangleIndices[i] = BitConverter.ToInt32(buffer, 0);
            }

            for (int i = 0; i < edgeIndicesCount; i++)
            {
                stream.Read(buffer, 0, 4);
                ret.EdgeIndices[i] = BitConverter.ToInt32(buffer, 0);
            }

            for (int i = 0; i < vertexCount; i++)
            {
                ret.VertexNormals[i].X = ReadFloat(stream, buffer);
                ret.VertexNormals[i].Y = ReadFloat(stream, buffer);
                ret.VertexNormals[i].Z = ReadFloat(stream, buffer);
            }

            for (int i = 0; i < vertexCount; i++)
            {
                ret.VertexToImage[i].X = ReadFloat(stream, buffer);
                ret.VertexToImage[i].Y = ReadFloat(stream, buffer);
            }

            for (int i = 0; i < 4; i++)
            {
                ret.Corners[i].X = ReadFloat(stream, buffer);
                ret.Corners[i].Y = ReadFloat(stream, buffer);
                ret.Corners[i].Z = ReadFloat(stream, buffer);
            }

            return ret;
        }

        private float ReadFloat(BlobHelper.DeletableFileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToSingle(buffer, 0);
        }

        private void WriteFloat(MemoryStream stream, double pixel)
        {
            float f = (float)pixel;
            stream.Write(BitConverter.GetBytes(f), 0, 4);
        }
    }
}
