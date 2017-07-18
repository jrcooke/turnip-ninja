/******************************************************************************
 * This code is derived from the GDAL  Arc/Info Binary Grid Translator code
 * at https://github.com/OSGeo/gdal/tree/trunk/gdal/frmts/aigrid
 * Original C Author:   Frank Warmerdam, warmerdam @pobox.com
 ****************************************************************************/

using System;
using System.IO;

namespace MountainView
{
    public class AdfReaderWorker
    {
        public static ChunkHolder<float> GetChunk(string folder)
        {
            byte[] buffer = new byte[8];

            // Read the header file.
            double dfCellSizeX, dfCellSizeY;
            int nBlocksPerRow, nBlocksPerColumn, nBlockXSize, nBlockYSize;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "hdr.adf")))
            {
                // Read the block size information.
                dfCellSizeX = ReadDouble(fs, buffer, 256);
                dfCellSizeY = ReadDouble(fs, buffer, 264);
                nBlocksPerRow = ReadInt32(fs, buffer, 288);
                nBlocksPerColumn = ReadInt32(fs, buffer, 292);
                nBlockXSize = ReadInt32(fs, buffer, 296);
                nBlockYSize = ReadInt32(fs, buffer, 304);
            }

            // Read the extents.
            double latLo, lonLo, latHi, lonHi;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "dblbnd.adf")))
            {
                lonLo = ReadDouble(fs, buffer);
                latLo = ReadDouble(fs, buffer);
                lonHi = ReadDouble(fs, buffer);
                latHi = ReadDouble(fs, buffer);
            }

            // Read the block index file.
            int[] panBlockOffset, panBlockSize;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "w001001x.adf")))
            {
                int nBlocks = (int)((ReadInt32(fs, buffer, 24) * 2 - 100) / 8);
                panBlockOffset = new int[nBlocks];
                panBlockSize = new int[nBlocks];
                fs.Seek(100, SeekOrigin.Begin);
                for (int i = 0; i < nBlocks; i++)
                {
                    panBlockOffset[i] = ReadInt32(fs, buffer) * 2;
                    panBlockSize[i] = ReadInt32(fs, buffer) * 2;
                }
            }

            // Compute the number of pixels and lines, and the number of tile files.
            int nPixels = (int)((lonHi - lonLo + 0.5 * dfCellSizeX) / dfCellSizeX);
            int nLines = (int)((latHi - latLo + 0.5 * dfCellSizeY) / dfCellSizeY);

            // Open the file w001001.adf file itself.
            ChunkHolder<float> output = null;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "w001001.adf")))
            {
                output = new ChunkHolder<float>(nLines,nPixels,
                    Angle.FromDecimalDegrees(latLo),
                    Angle.FromDecimalDegrees(lonLo),
                    Angle.FromDecimalDegrees(latHi),
                    Angle.FromDecimalDegrees(lonHi));

                float[] panRaster = new float[nBlockXSize * nBlockYSize];
                byte[] panRasterBuffer = new byte[4 * nBlockXSize * nBlockYSize];
                for (int nBlock = 0; nBlock < panBlockSize.Length; nBlock++)
                {
                    // Collect raw data.
                    fs.Seek(panBlockOffset[nBlock] + 2, SeekOrigin.Begin);
                    fs.Read(panRasterBuffer, 0, panRasterBuffer.Length);
                    for (int j = 0; j < panRaster.Length; j++)
                    {
                        byte tmp = panRasterBuffer[j * 4 + 3];
                        panRasterBuffer[j * 4 + 3] = panRasterBuffer[j * 4 + 0];
                        panRasterBuffer[j * 4 + 0] = tmp;
                        tmp = panRasterBuffer[j * 4 + 2];
                        panRasterBuffer[j * 4 + 2] = panRasterBuffer[j * 4 + 1];
                        panRasterBuffer[j * 4 + 1] = tmp;
                        panRaster[j] = BitConverter.ToSingle(panRasterBuffer, j * 4);
                    }

                    int tileOffsetX = (nBlock % nBlocksPerRow) * nBlockXSize;
                    int tileOffsetY = (nBlock / nBlocksPerRow) * nBlockYSize;
                    for (int j = 0; j < nBlockYSize && j < output.LonSteps - tileOffsetY; j++)
                    {
                        for (int i = 0; i < nBlockXSize && i < output.LatSteps - tileOffsetX; i++)
                        {
                            output.Data[nLines - 1 - j - tileOffsetY][nPixels - 1 - i - tileOffsetX] = panRaster[i + j * nBlockXSize];
                        }
                    }
                }
            }
            return output;
        }

        private static int ReadInt32(FileStream fs, byte[] bufferLen4, int? offsetFromOrigin = null)
        {
            if (offsetFromOrigin.HasValue)
            {
                fs.Seek(offsetFromOrigin.Value, SeekOrigin.Begin);
            }

            fs.Read(bufferLen4, 0, 4);
            for (int i = 0; i < 2; i++)
            {
                byte tmp = bufferLen4[3 - i];
                bufferLen4[3 - i] = bufferLen4[i];
                bufferLen4[i] = tmp;
            }
            return BitConverter.ToInt32(bufferLen4, 0);
        }

        private static double ReadDouble(FileStream fs, byte[] bufferLen8, int? offsetFromOrigin = null)
        {
            if (offsetFromOrigin.HasValue)
            {
                fs.Seek(offsetFromOrigin.Value, SeekOrigin.Begin);
            }

            fs.Read(bufferLen8, 0, 8);
            for (int i = 0; i < 4; i++)
            {
                byte tmp = bufferLen8[7 - i];
                bufferLen8[7 - i] = bufferLen8[i];
                bufferLen8[i] = tmp;
            }

            return BitConverter.ToDouble(bufferLen8, 0);
        }
    }
}
