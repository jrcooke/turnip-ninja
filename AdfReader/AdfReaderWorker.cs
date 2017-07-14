/******************************************************************************
 * This code is derived from the GDAL  Arc/Info Binary Grid Translator code
 * at https://github.com/OSGeo/gdal/tree/trunk/gdal/frmts/aigrid
 * Original C Author:   Frank Warmerdam, warmerdam @pobox.com
 ****************************************************************************/

using System;
using System.IO;

namespace AdfReader
{
    public class AdfReaderWorker
    {
        public static ChunkHolder<float> GetChunk(string folder)
        {
            byte[] buffer = new byte[8];

            // Read the header file.
            int nBlocksPerRow, nBlocksPerColumn, nBlockXSize, nBlockYSize, nTileXSize, nTileYSize;
            double dfCellSizeX, dfCellSizeY;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "hdr.adf")))
            {
                // Read the block size information.
                nBlocksPerRow = ReadInt32(fs, buffer, 288);
                nBlocksPerColumn = ReadInt32(fs, buffer, 292);
                nBlockXSize = ReadInt32(fs, buffer, 296);
                nBlockYSize = ReadInt32(fs, buffer, 304);
                dfCellSizeX = ReadDouble(fs, buffer, 256);
                dfCellSizeY = ReadDouble(fs, buffer, 264);
                nTileXSize = nBlockXSize * nBlocksPerRow;
                nTileYSize = nBlockYSize * nBlocksPerColumn;
            }

            // Read the extents.
            double latLo, lonLo, latHi, lonHi;
            int nPixels, nLines, nTilesPerRow, nTilesPerColumn;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "dblbnd.adf")))
            {
                lonLo = ReadDouble(fs, buffer);
                latLo = ReadDouble(fs, buffer);
                lonHi = ReadDouble(fs, buffer);
                latHi = ReadDouble(fs, buffer);

                // Compute the number of pixels and lines, and the number of tile files.
                nPixels = (int)((lonHi - lonLo + 0.5 * dfCellSizeX) / dfCellSizeX);
                nLines = (int)((latHi - latLo + 0.5 * dfCellSizeY) / dfCellSizeY);
                nTilesPerRow = (nPixels - 1) / nTileXSize + 1;
                nTilesPerColumn = (nLines - 1) / nTileYSize + 1;
            }

            // Read the block index file.
            int[] panBlockOffset, panBlockSize;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "w001001x.adf")))
            {
                // Get the file length (in 2 byte shorts)
                int nBlocks = (int)((ReadInt32(fs, buffer, 24) * 2 - 100) / 8);

                // Allocate AIGInfo block info arrays.
                panBlockOffset = new int[nBlocks];
                panBlockSize = new int[nBlocks];
                fs.Seek(100, SeekOrigin.Begin);
                for (int i = 0; i < nBlocks; i++)
                {
                    panBlockOffset[i] = ReadInt32(fs, buffer) * 2;
                    panBlockSize[i] = ReadInt32(fs, buffer) * 2;
                }
            }

            // Open the file w001001.adf file itself.
            ChunkHolder<float> output = null;
            using (FileStream fs = File.OpenRead(Path.Combine(folder, "w001001.adf")))
            {
                output = new ChunkHolder<float>(nPixels, nLines,
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
                    for (int i = 0; i < nBlockXSize && i < output.Width - tileOffsetX; i++)
                    {
                        for (int j = 0; j < nBlockYSize && j < output.Height - tileOffsetY; j++)
                        {
                            output.Data[i + tileOffsetX][j + tileOffsetY] = panRaster[i + j * nBlockXSize];
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
