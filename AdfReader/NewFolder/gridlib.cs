/******************************************************************************
 * $Id: gridlib.c 38227 2017-05-12 16:31:33Z rouault $
 *
 * Project:  Arc/Info Binary Grid Translator
 * Purpose:  Grid file reading code.
 * Author:   Frank Warmerdam, warmerdam@pobox.com
 *
 ******************************************************************************
 * Copyright (c) 1999, Frank Warmerdam
 * Copyright (c) 2007-2010, Even Rouault <even dot rouault at mines-paris dot org>
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
    public class GridLib
    {
        /// <summary>
        /// Read a single block of integer grid data.
        /// </summary>
        public static void AIGReadBlock(
            VSILFILE fp,
            UInt32 nBlockOffset,
            int nBlockSize,
            int nBlockXSize,
            int nBlockYSize,
            float[] panData)
        {
            // If the block has zero size it is all dummies.
            if (nBlockSize == 0)
            {
                for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
                {
                    panData[i] = float.NaN;
                }

                return;
            }

            // Verify the block size.
            fp.Seek(nBlockOffset);
            int actualBlockSize = fp.ReadInt16();
            if (nBlockSize != actualBlockSize * 2)
            {
                throw new InvalidOperationException("Block is corrupt, block size was " + (actualBlockSize * 2) +
                    ", but expected to be " + nBlockSize);
            }

            // Collect raw data.
            for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
            {
                panData[i] = fp.ReadSingle();
            }
        }

        /// <summary>
        /// Read the w001001x.adf file, and populate the given info
        /// structure with the block offsets, and sizes.
        /// </summary>
        public static void AIGReadHeader(string pszCoverName, AIGInfo_t psInfo)
        {
            // Open the file hdr.adf file.
            string pszHDRFilename = Path.Combine(pszCoverName, "hdr.adf");
            VSILFILE fp = AigOpen.AIGLLOpen(pszHDRFilename);

            // Read the whole file (we expect it to always be 308 bytes
            // long.
            Byte[] abyData = new Byte[308];
            fp.ReadByteArray(abyData);
            fp.VSIFCloseL();

            // Read the block size information.
            psInfo.nBlocksPerRow = MyBitConverter.ToInt32(abyData, 288);
            psInfo.nBlocksPerColumn = MyBitConverter.ToInt32(abyData, 292);
            psInfo.nBlockXSize = MyBitConverter.ToInt32(abyData, 296);
            psInfo.nBlockYSize = MyBitConverter.ToInt32(abyData, 304);
            psInfo.dfCellSizeX = MyBitConverter.ToDouble(abyData, 256);
            psInfo.dfCellSizeY = MyBitConverter.ToDouble(abyData, 264);
        }

        /// <summary>
        /// Read the w001001x.adf file, and populate the given info
        /// structure with the block offsets, and sizes.
        /// </summary>
        public static void AIGReadBlockIndex(AIGInfo_t psInfo, AIGTileInfo psTInfo, string pszBasename)
        {
            // Open the file hdr.adf file.
            string pszHDRFilename = Path.Combine(psInfo.pszCoverName, pszBasename + "x.adf");
            VSILFILE fp = AigOpen.AIGLLOpen(pszHDRFilename);

            // Verify the magic number.  This is often corrupted by CR/LF translation.
            Byte[] abyHeader = new Byte[8];
            fp.ReadByteArray(abyHeader);
            if (abyHeader[3] == 0x0D && abyHeader[4] == 0x0A)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("w001001x.adf file header has been corrupted.");
            }

            if (abyHeader[0] != 0x00 || abyHeader[1] != 0x00 || abyHeader[2] != 0x27 || abyHeader[3] != 0x0A || abyHeader[4] != 0xFF || abyHeader[5] != 0xFF)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("w001001x.adf file header magic number is corrupt.");
            }

            // Get the file length (in 2 byte shorts)
            fp.Seek(24);
            uint nValue = fp.ReadUInt32();
            if (nValue > int.MaxValue)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("AIGReadBlockIndex: Bad length");
            }

            uint nLength = nValue * 2;
            if (nLength <= 100)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("AIGReadBlockIndex: Bad length");
            }

            // Allocate buffer, and read the file (from beyond the header)
            // into the buffer.
            psTInfo.nBlocks = (int)((nLength - 100) / 8);
            UInt32[] panIndex = new UInt32[2 * psTInfo.nBlocks];
            if (panIndex == null)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException();
            }

            fp.Seek(100);
            fp.ReadUInt32Array(panIndex);
            fp.VSIFCloseL();

            // Allocate AIGInfo block info arrays.
            psTInfo.panBlockOffset = new UInt32[psTInfo.nBlocks];
            psTInfo.panBlockSize = new int[psTInfo.nBlocks];

            // Populate the block information.
            for (int i = 0; i < psTInfo.nBlocks; i++)
            {
                UInt32 nVal = panIndex[i * 2];
                if (nVal >= int.MaxValue)
                {
                    throw new InvalidOperationException("AIGReadBlockIndex: Bad offset for block " + i);
                }

                psTInfo.panBlockOffset[i] = nVal * 2;
                nVal = panIndex[i * 2 + 1];
                if (nVal >= int.MaxValue / 2)
                {
                    throw new InvalidOperationException("AIGReadBlockIndex: Bad offset for block " + i);
                }

                psTInfo.panBlockSize[i] = (int)(nVal * 2);
            }
        }

        /// <summary>
        /// Read the dblbnd.adf file for the georeferenced bounds.
        /// </summary>
        public static void AIGReadBounds(string pszCoverName, AIGInfo_t psInfo)
        {
            // Open the file dblbnd.adf file.
            string pszHDRFilename = Path.Combine(pszCoverName, "dblbnd.adf");
            VSILFILE fp = AigOpen.AIGLLOpen(pszHDRFilename);

            // Get the contents - four doubles.
            double[] adfBound = new double[4];
            fp.ReadDoubleArray(adfBound, 4);
            fp.VSIFCloseL();

            psInfo.dfLLX = adfBound[0];
            psInfo.dfLLY = adfBound[1];
            psInfo.dfURX = adfBound[2];
            psInfo.dfURY = adfBound[3];
        }
    }
}
