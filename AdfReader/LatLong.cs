using System;
using System.Collections.Generic;
using System.IO;

namespace AdfReader
{

    public class LatLon
    {
        const int boundaryElements = 6;
        const int trueElements = 10800;

        private static Dictionary<string, float[][]> cache = new Dictionary<string, float[][]>();

        public static IEnumerable<Tuple<double, double, float>> GetHeightsInMeters(double lat, double lon)
        {
            int latRoot = (int)lat;
            int lonRoot = (int)lon - 1;

            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

            if (!cache.ContainsKey(fileName))
            {
                string inputFile = @"C:\Users\jcooke\Desktop\Map\" + fileName + @"\grd" + fileName + @"_13\w001001.adf";
                if (!File.Exists(inputFile))
                {
                    throw new InvalidOperationException("Missing data file: " + inputFile);
                }

                cache[fileName] = ReadDataToChunks(inputFile);
            }

            var chunk = cache[fileName];
            for (int i = 0; i < trueElements; i++)
            {
                for (int j = 0; j < trueElements; j++)
                {
                    double lon2 = lonRoot + i * 1.0 / trueElements;
                    double lat2 = latRoot + j * 1.0 / trueElements;
                    float value = chunk[trueElements - 1 - j + boundaryElements][i + boundaryElements];
                    yield return new Tuple<double, double, float>(lat2, lon2, value);
                }
            }
        }

        private static float[][] ReadDataToChunks(string adfFile)
        {
            int elements = trueElements + boundaryElements * 2;
            var bytes = File.ReadAllBytes(adfFile);

            byte[] buff = new byte[4];

            int frameIndex = 0;
            int indexWithinFrame = 0;
            float[] currentFrame2 = null;

            int index = 16 * 6 + 4;
            int terminator1 = bytes[index];
            int terminator2 = bytes[index + 1];

            int numberOfBatches = bytes[index] / 2;

            List<float[]> runningList = new List<float[]>();
            while (index < bytes.Length)
            {
                if (bytes[index] == terminator1 && bytes[index + 1] == terminator2)
                {
                    index += 2;
                    frameIndex++;

                    indexWithinFrame = 0;
                    currentFrame2 = new float[256];
                }

                buff[0] = bytes[index + 3];
                buff[1] = bytes[index + 2];
                buff[2] = bytes[index + 1];
                buff[3] = bytes[index + 0];
                index += 4;

                currentFrame2[indexWithinFrame] = BitConverter.ToSingle(buff, 0);

                indexWithinFrame++;
                if (indexWithinFrame % 256 == 0)
                {
                    runningList.Add(currentFrame2);
                    indexWithinFrame = 0;
                }
            }

            if (indexWithinFrame != 0)
            {
                throw new InvalidOperationException("Incomplete frame read");
            }

            float[][] rows = new float[numberOfBatches][];
            for (int i = 0; i < numberOfBatches; i++)
            {
                rows[i] = new float[elements];
            }

            int widthIndex = 0;

            float[][] batchData = new float[numberOfBatches][];
            int batch = 0;

            float[][] data = new float[elements][];
            int dataIndex = 0;

            foreach (var part in runningList)
            {
                batchData[batch] = part;
                batch++;

                if (batch == numberOfBatches)
                {
                    batch = 0;
                    for (int k = 0; k < 256; k++)
                    {
                        if (widthIndex < elements)
                        {
                            for (int i = 0; i < numberOfBatches; i++)
                            {
                                rows[i][widthIndex] = (batchData[i][k]);
                            }

                            widthIndex++;
                        }
                    }

                    if (widthIndex == elements)
                    {
                        widthIndex = 0;
                        for (int i = 0; i < numberOfBatches; i++)
                        {
                            if (dataIndex < data.Length)
                            {
                                data[dataIndex++] = rows[i];
                            }
                            else
                            {

                            }
                            rows[i] = new float[elements];
                        }
                    }
                }
            }

            return data;
        }
    }
}
