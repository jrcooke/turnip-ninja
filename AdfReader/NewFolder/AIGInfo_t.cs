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
            var fpGrid = new VSILFILE(Path.Combine(pszCoverName, szBasename + ".adf"));
            ChunkHolder<float> output = null;
            try
            {
                output = new ChunkHolder<float>(nPixels, nLines,
                    Angle.FromDecimalDegrees(dfLLY),
                    Angle.FromDecimalDegrees(dfLLX),
                    Angle.FromDecimalDegrees(dfURY),
                    Angle.FromDecimalDegrees(dfURX));

                float[] panRaster = new float[nBlockXSize * nBlockYSize];
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
                        fpGrid.Seek(panBlockOffset[nBlock]);
                        int actualBlockSize = fpGrid.ReadInt16();
                        if (panBlockSize[nBlock] != actualBlockSize * 2)
                        {
                            throw new InvalidOperationException("Block is corrupt, block size was " + (actualBlockSize * 2) + ", but expected to be " + panBlockSize[nBlock]);
                        }

                        // Collect raw data.
                        fpGrid.ReadSingleArray(panRaster);
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
            finally
            {
                fpGrid.VSIFCloseL();
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
            VSILFILE fp = new VSILFILE(Path.Combine(pszCoverName, "hdr.adf"));

            // Read the whole file (we expect it to always be 308 bytes).
            Byte[] abyData = new Byte[308];
            fp.ReadByteArray(abyData);
            fp.VSIFCloseL();

            // Read the block size information.
            this.nBlocksPerRow = MyBitConverter.ToInt32(abyData, 288);
            this.nBlocksPerColumn = MyBitConverter.ToInt32(abyData, 292);
            this.nBlockXSize = MyBitConverter.ToInt32(abyData, 296);
            this.nBlockYSize = MyBitConverter.ToInt32(abyData, 304);
            this.dfCellSizeX = MyBitConverter.ToDouble(abyData, 256);
            this.dfCellSizeY = MyBitConverter.ToDouble(abyData, 264);
        }


        /// <summary>
        /// Read the dblbnd.adf file for the georeferenced bounds.
        /// </summary>
        private void AIGReadBounds()
        {
            // Open the file dblbnd.adf file.
            VSILFILE fp = new VSILFILE(Path.Combine(pszCoverName, "dblbnd.adf"));
            this.dfLLX = fp.ReadDouble();
            this.dfLLY = fp.ReadDouble();
            this.dfURX = fp.ReadDouble();
            this.dfURY = fp.ReadDouble();
            fp.VSIFCloseL();
        }

        /// <summary>
        /// Read the w001001x.adf file, and populate the given info
        /// structure with the block offsets, and sizes.
        /// </summary>
        public void AIGReadBlockIndex()
        {
            // Open the file hdr.adf file.
            VSILFILE fp = new VSILFILE(Path.Combine(this.pszCoverName, szBasename + "x.adf"));

            // Verify the magic number.  This is often corrupted by CR/LF translation.
            Byte[] abyHeader = new Byte[8];
            fp.ReadByteArray(abyHeader);
            if (abyHeader[0] != 0x00 || abyHeader[1] != 0x00 || abyHeader[2] != 0x27 || abyHeader[3] != 0x0A || abyHeader[4] != 0xFF || abyHeader[5] != 0xFF)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("w001001x.adf file header magic number is corrupt.");
            }

            // Get the file length (in 2 byte shorts)
            fp.Seek(24);
            uint nLength = fp.ReadUInt32() * 2;
            if (nLength <= 100)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("AIGReadBlockIndex: Bad length");
            }

            // Allocate buffer, and read the file (from beyond the header)
            // into the buffer.
            this.nBlocks = (int)((nLength - 100) / 8);
            UInt32[] panIndex = new UInt32[2 * this.nBlocks];

            fp.Seek(100);
            fp.ReadUInt32Array(panIndex);
            fp.VSIFCloseL();

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

    }
}
