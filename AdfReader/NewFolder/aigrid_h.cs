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

namespace AdfReader.NewFolder
{
    internal static class DefineConstants
    {
        public const int ESRI_GRID_NO_DATA = -2147483647;
        public const float ESRI_GRID_FLOAT_NO_DATA = (float)(-340282346638528859811704183484516925440.0);
    }

    public class AIGTileInfo
    {
        public int nBlocks;
        public UInt32[] panBlockOffset;
        public int[] panBlockSize;
        public VSILFILE fpGrid;  // The w001001.adf file.
        public int bTriedToLoad;
    }

    public class AIGInfo_t
    {
        /* Private information */
        public AIGTileInfo[] pasTileInfo;
        public int bHasWarned;

        /* public information */
        public string pszCoverName; // Path of coverage directory.
        public Int32 nCellType;
        public Int32 bCompressed;
        public const int AIG_CELLTYPE_INT = 1;
        public const int AIG_CELLTYPE_FLOAT = 2;
        public Int32 nBlockXSize;
        public Int32 nBlockYSize;
        public Int32 nBlocksPerRow;
        public Int32 nBlocksPerColumn;
        public int nTileXSize;
        public int nTileYSize;
        public int nTilesPerRow;
        public int nTilesPerColumn;
        public double dfLLX;
        public double dfLLY;
        public double dfURX;
        public double dfURY;
        public double dfCellSizeX;
        public double dfCellSizeY;
        public int nPixels;
        public int nLines;
        public double dfMin;
        public double dfMax;
        public double dfMean;
        public double dfStdDev;
    }
}
