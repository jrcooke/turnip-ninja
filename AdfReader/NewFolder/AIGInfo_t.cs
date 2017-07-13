/******************************************************************************
 * $Id: aigrid.h 34521 2016-07-02 21:26:43Z goatbar $
 *
 * Project:  Arc/Info Binary Grid Translator
 * Purpose:  Grid file access include file.
 * Author:   Frank Warmerdam, warmerdam@pobox.com
 *
 ******************************************************************************
 * Copyright (c) 1999, Frank Warmerdam
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 ****************************************************************************/

using System;
using System.IO;

namespace AdfReader.NewFolder
{
    public class AIGInfo_t
    {
        // Compute the basename.
        private const string szBasename = "w001001";

        // From constructor
        public string pszCoverName; // Path of coverage directory.

        // From hdr.adf
        public Int32 nBlocksPerRow;
        public Int32 nBlocksPerColumn;
        public Int32 nBlockXSize;
        public Int32 nBlockYSize;
        public double dfCellSizeX;
        public double dfCellSizeY;

        // From dblbnd.adf
        public double dfLLX;
        public double dfLLY;
        public double dfURX;
        public double dfURY;

        // Computed
        public int nTileXSize;
        public int nTileYSize;
        public int nTilesPerRow;
        public int nTilesPerColumn;
        public int nPixels;
        public int nLines;

        public int nBlocks;
        public UInt32[] panBlockOffset;
        public int[] panBlockSize;

        public AIGInfo_t(string pszCoverName)
        {
            this.pszCoverName = pszCoverName;
        }

        public static ChunkHolder<float> GetChunk(string folder)
        {
            var ret = new AIGInfo_t(folder);
            return ret.GetChunk();
        }

        private ChunkHolder<float> GetChunk()
        {
            // Read the header file.
            AIGReadHeader();

            // Read the extents.
            AIGReadBounds();

            // Compute the number of pixels and lines, and the number of tile files.
            nPixels = (int)((dfURX - dfLLX + 0.5 * dfCellSizeX) / dfCellSizeX);
            nLines = (int)((dfURY - dfLLY + 0.5 * dfCellSizeY) / dfCellSizeY);

            nTileXSize = nBlockXSize * nBlocksPerRow;
            nTileYSize = nBlockYSize * nBlocksPerColumn;

            nTilesPerRow = (nPixels - 1) / nTileXSize + 1;
            nTilesPerColumn = (nLines - 1) / nTileYSize + 1;

            // Read the block index file.
            AIGReadBlockIndex();

            // Open the file w001001.adf file itself.
            ChunkHolder<float> output = null;
            using (FileStream fs = File.OpenRead(Path.Combine(pszCoverName, szBasename + ".adf")))
            {
                //                var fpGrid = new VSILFILE(Path.Combine(pszCoverName, szBasename + ".adf"));
                output = new ChunkHolder<float>(nPixels, nLines,
                    Angle.FromDecimalDegrees(dfLLY),
                    Angle.FromDecimalDegrees(dfLLX),
                    Angle.FromDecimalDegrees(dfURY),
                    Angle.FromDecimalDegrees(dfURX));

                float[] panRaster = new float[nBlockXSize * nBlockYSize];
                byte[] panRasterBuffer = new byte[4 * nBlockXSize * nBlockYSize];
                byte[] readInt16Buffer = new byte[2];

                for (int nBlock = 0; nBlock < nBlocks; nBlock++)
                {
                    // If the block has zero size it is all dummies.
                    if (panBlockSize[nBlock] == 0)
                    {
                        for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
                        {
                            panRaster[i] = float.NaN;
                        }
                    }
                    else
                    {
                        // Verify the block size.
                        fs.Seek(panBlockOffset[nBlock], SeekOrigin.Begin);
                        int actualBlockSize = ReadInt16(fs, readInt16Buffer);
                        if (panBlockSize[nBlock] != actualBlockSize * 2)
                        {
                            throw new InvalidOperationException("Block is corrupt, block size was " + (actualBlockSize * 2) + ", but expected to be " + panBlockSize[nBlock]);
                        }

                        // Collect raw data.
                        ReadSingleArray(fs, panRaster, panRasterBuffer);
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

        /// <summary>
        /// Read the w001001x.adf file, and populate the given info
        /// structure with the block offsets, and sizes.
        /// </summary>
        private void AIGReadHeader()
        {
            // Open the file hdr.adf file.
            byte[] abyData = new byte[308];
            using (FileStream fs = File.OpenRead(Path.Combine(pszCoverName, "hdr.adf")))
            {
                // Read the whole file (we expect it to always be 308 bytes).
                ReadByteArray(fs, abyData);
            }

            // Read the block size information.
            this.nBlocksPerRow = BitConverter.ToInt32(OrderSwap4Bytes(abyData, 288), 288);
            this.nBlocksPerColumn = BitConverter.ToInt32(OrderSwap4Bytes(abyData, 292), 292);
            this.nBlockXSize = BitConverter.ToInt32(OrderSwap4Bytes(abyData, 296), 296);
            this.nBlockYSize = BitConverter.ToInt32(OrderSwap4Bytes(abyData, 304), 304);
            this.dfCellSizeX = BitConverter.ToDouble(OrderSwap8Bytes(abyData, 256), 256);
            this.dfCellSizeY = BitConverter.ToDouble(OrderSwap8Bytes(abyData, 264), 264);
        }


        /// <summary>
        /// Read the dblbnd.adf file for the georeferenced bounds.
        /// </summary>
        private void AIGReadBounds()
        {
            // Open the file dblbnd.adf file.
            byte[] readDoubleBuffer = new byte[8];
            using (FileStream fs = File.OpenRead(Path.Combine(pszCoverName, "dblbnd.adf")))
            {
                this.dfLLX = ReadDouble(fs, readDoubleBuffer);
                this.dfLLY = ReadDouble(fs, readDoubleBuffer);
                this.dfURX = ReadDouble(fs, readDoubleBuffer);
                this.dfURY = ReadDouble(fs, readDoubleBuffer);
            }
        }

        /// <summary>
        /// Read the w001001x.adf file, and populate the given info
        /// structure with the block offsets, and sizes.
        /// </summary>
        public void AIGReadBlockIndex()
        {
            // Open the file hdr.adf file.
            UInt32[] panIndex = null;
            using (FileStream fs = File.OpenRead(Path.Combine(pszCoverName, szBasename + "x.adf")))
            {
                // Verify the magic number.  This is often corrupted by CR/LF translation.
                byte[] abyHeader = new byte[8];
                ReadByteArray(fs, abyHeader);
                if (abyHeader[0] != 0x00 || abyHeader[1] != 0x00 || abyHeader[2] != 0x27 || abyHeader[3] != 0x0A || abyHeader[4] != 0xFF || abyHeader[5] != 0xFF)
                {
                    throw new InvalidOperationException("w001001x.adf file header magic number is corrupt.");
                }

                // Get the file length (in 2 byte shorts)
                byte[] bufferLen4 = new byte[4];
                fs.Seek(24, SeekOrigin.Begin);
                uint nLength = ReadUInt32(fs, bufferLen4) * 2;
                if (nLength <= 100)
                {
                    throw new InvalidOperationException("AIGReadBlockIndex: Bad length");
                }

                // Allocate buffer, and read the file (from beyond the header)
                // into the buffer.
                this.nBlocks = (int)((nLength - 100) / 8);
                panIndex = new UInt32[2 * this.nBlocks];

                fs.Seek(100, SeekOrigin.Begin);
                ReadUInt32Array(fs, bufferLen4, panIndex);
            }

            // Allocate AIGInfo block info arrays.
            this.panBlockOffset = new UInt32[this.nBlocks];
            this.panBlockSize = new int[this.nBlocks];

            // Populate the block information.
            for (int i = 0; i < this.nBlocks; i++)
            {
                this.panBlockOffset[i] = panIndex[i * 2] * 2;
                this.panBlockSize[i] = (int)(panIndex[i * 2 + 1] * 2);
            }
        }

        internal static void ReadByteArray(FileStream fs, byte[] value)
        {
            int len = fs.Read(value, 0, value.Length);
            if (len != value.Length)
            {
                throw new InvalidOperationException();
            }
        }

        internal static UInt32 ReadUInt32(FileStream fs,
            byte[] bufferLen4)
        {
            if (bufferLen4.Length != 4)
            {
                throw new InvalidOperationException();
            }

            ReadByteArray(fs, bufferLen4);
            OrderSwap4Bytes(bufferLen4, 0);
            return BitConverter.ToUInt32(bufferLen4, 0);
        }

        internal static void ReadUInt32Array(FileStream fs, byte[] bufferLen4, uint[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                value[i] = ReadUInt32(fs, bufferLen4);
            }
        }

        internal static double ReadDouble(FileStream fs, byte[] bufferLen8)
        {
            if (bufferLen8.Length != 8)
            {
                throw new InvalidOperationException();
            }

            ReadByteArray(fs, bufferLen8);
            OrderSwap8Bytes(bufferLen8, 0);
            return BitConverter.ToDouble(bufferLen8, 0);
        }

        internal static void ReadSingleArray(FileStream fs, float[] data, byte[] bufferLen4xDataLen)
        {
            if (bufferLen4xDataLen.Length != data.Length * 4)
            {
                throw new ArgumentException("bufferLen4xDataLen");
            }

            if (fs.Read(bufferLen4xDataLen, 0, bufferLen4xDataLen.Length) != bufferLen4xDataLen.Length)
            {
                throw new InvalidOperationException();
            }

            for (int j = 0; j < data.Length; j++)
            {
                byte tmp = bufferLen4xDataLen[j * 4 + 3];
                bufferLen4xDataLen[j * 4 + 3] = bufferLen4xDataLen[j * 4 + 0];
                bufferLen4xDataLen[j * 4 + 0] = tmp;
                tmp = bufferLen4xDataLen[j * 4 + 2];
                bufferLen4xDataLen[j * 4 + 2] = bufferLen4xDataLen[j * 4 + 1];
                bufferLen4xDataLen[j * 4 + 1] = tmp;
                data[j] = BitConverter.ToSingle(bufferLen4xDataLen, j * 4);
            }
        }

        internal static int ReadInt16(FileStream fs, byte[] bufferLen2)
        {
            if (bufferLen2.Length != 2)
            {
                throw new InvalidOperationException();
            }

            ReadByteArray(fs, bufferLen2);
            byte tmp = bufferLen2[1];
            bufferLen2[1] = bufferLen2[0];
            bufferLen2[0] = tmp;
            return BitConverter.ToInt16(bufferLen2, 0);
        }

        internal static byte[] OrderSwap8Bytes(byte[] value, int startIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                byte tmp = value[startIndex + 7 - i];
                value[startIndex + 7 - i] = value[startIndex + i];
                value[startIndex + i] = tmp;
            }

            return value;
        }

        internal static byte[] OrderSwap4Bytes(byte[] value, int startIndex)
        {
            for (int i = 0; i < 2; i++)
            {
                byte tmp = value[startIndex + 3 - i];
                value[startIndex + 3 - i] = value[startIndex + i];
                value[startIndex + i] = tmp;
            }

            return value;
        }
    }
}
