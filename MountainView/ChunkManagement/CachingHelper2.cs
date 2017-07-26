//using MountainView.Base;
//using MountainView.ChunkManagement;
//using System;
//using System.IO;

//namespace MountainView
//{
//    public class CachingHelper2<T>
//    {
//        private string cachedFileTemplate;
//        private string description;
//        private object locker = new object();
//        private Action<T, FileStream> writeElement;
//        private Func<FileStream, byte[], T> readElement;
//        private Func<int, int, Angle, Angle, Angle, Angle, Angle, Angle, ChunkHolder<T>> generateData;
//        private Cache<long, ChunkHolder<T>> chunkCache;

//        public CachingHelper2(
//            string cachedFileTemplate,
//            string description,
//            Action<T, FileStream> writeElement,
//            Func<FileStream, byte[], T> readElement)
//        {
//            chunkCache = new Cache<long, ChunkHolder<T>>(TimeSpan.FromSeconds(15));
//            this.cachedFileTemplate = cachedFileTemplate;
//            this.description = description;
//            this.writeElement = writeElement;
//            this.readElement = readElement;
//        }

//        //public T GetValue(long key)
//        //{
//        //    double latDec = lat.DecimalDegree;
//        //    double lonDec = lon.DecimalDegree;
//        //    int zoomLevel = GetZoomLevel(latDec, lonDec, cosLat, metersPerElement);
//        //    ChunkHolder<T> chunk = null; //  GetValuesFromCache(lat, lon, zoomLevel);
//        //    var size = GetSize(zoomLevel);

//        //    throw new NotImplementedException();
//        //    //int minLatTotMin = Math.Min(
//        //    //    Utils.AddAwayFromZero(Utils.TruncateTowardsZero(latDec * 60 * 60) / size, 1),
//        //    //    (Utils.TruncateTowardsZero(latDec * 60 * 60) / size)) * size / 60;
//        //    //int minLonTotMin = Math.Min(
//        //    //    Utils.AddAwayFromZero(Utils.TruncateTowardsZero(lonDec * 60 * 60) / size, 1),
//        //    //    (Utils.TruncateTowardsZero(lonDec * 60 * 60) / size)) * size / 60;

//        //    //int targetLat = (int)Math.Round(((latDec * 60 - minLatTotMin) * SmallBatch * 60 / size));
//        //    //int targetLon = (int)Math.Round(((lonDec * 60 - minLonTotMin) * SmallBatch * 60 / size));

//        //    //if (targetLat >= 0 && targetLat < chunk.LatSteps && targetLon >= 0 && targetLon < chunk.LonSteps)
//        //    //{
//        //    //    return chunk.Data[targetLat][targetLon];
//        //    //}
//        //    //else
//        //    //{
//        //    //    return default(T);
//        //    //}
//        //}

//        public bool TryGetValuesFromCache(long key, out ChunkHolder<T> ret)
//        {
//            while (!chunkCache.TryGetValue(key, out ret) || ret == null)
//            {
//                string filename = Utils.GetFileName(key);
//                string fullName = string.Format(cachedFileTemplate, filename);
//                lock (locker)
//                {
//                    if (File.Exists(fullName))
//                    {
//                        Console.WriteLine("Reading " + description + " chunk file '" + filename + "'to cache...");
//                        ret = ReadChunk(fullName);
//                        chunkCache.Add(key, ret);
//                        Console.WriteLine("Read " + description + " chunk file '" + filename + "'to cache.");
//                    }
//                }
//            }

//            return ret;
//        }

//        //private void WriteChunk(ChunkHolder<T> ret, string fullName)
//        //{
//        //    using (FileStream stream = File.OpenWrite(fullName))
//        //    {
//        //        stream.Write(BitConverter.GetBytes(ret.LatSteps), 0, 4);
//        //        stream.Write(BitConverter.GetBytes(ret.LonSteps), 0, 4);
//        //        stream.Write(BitConverter.GetBytes(ret.LatLo.TotalSeconds), 0, 4);
//        //        stream.Write(BitConverter.GetBytes(ret.LonLo.TotalSeconds), 0, 4);
//        //        stream.Write(BitConverter.GetBytes(ret.LatHi.TotalSeconds), 0, 4);
//        //        stream.Write(BitConverter.GetBytes(ret.LonHi.TotalSeconds), 0, 4);
//        //        for (int i = 0; i < ret.LatSteps; i++)
//        //        {
//        //            for (int j = 0; j < ret.LonSteps; j++)
//        //            {
//        //                writeElement(ret.Data[i][j], stream);
//        //            }
//        //        }
//        //    }
//        //}

//        private ChunkHolder<T> ReadChunk(string fullName)
//        {
//            ChunkHolder<T> ret = null;
//            byte[] buffer = new byte[4];
//            using (var stream = File.OpenRead(fullName))
//            {
//                int width = Utils.ReadInt(stream, buffer);
//                int height = Utils.ReadInt(stream, buffer);

//                Angle latLo = Angle.FromSeconds(Utils.ReadInt(stream, buffer));
//                Angle lonLo = Angle.FromSeconds(Utils.ReadInt(stream, buffer));
//                Angle latHi = Angle.FromSeconds(Utils.ReadInt(stream, buffer));
//                Angle lonHi = Angle.FromSeconds(Utils.ReadInt(stream, buffer));

//                ret = new ChunkHolder<T>(width, height, latLo, lonLo, latHi, lonHi);
//                for (int i = 0; i < width; i++)
//                {
//                    for (int j = 0; j < height; j++)
//                    {
//                        ret.Data[i][j] = readElement(stream, buffer);
//                    }
//                }
//            }

//            return ret;
//        }
//    }
//}
