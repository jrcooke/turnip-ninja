/******************************************************************************
 * $Id: aigopen.c 34521 2016-07-02 21:26:43Z goatbar $
 *
 * Project:  Arc/Info Binary Grid Translator
 * Purpose:  Grid file access cover API for non-GDAL use.
 * Author:   Frank Warmerdam, warmerdam@pobox.com
 *
 ******************************************************************************
 * Copyright (c) 1999, Frank Warmerdam
 * Copyright (c) 2009-2010, Even Rouault <even dot rouault at mines-paris dot org>
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
    public static class AigOpen
    {
        public static AIGInfo_t AIGOpen(string pszInputName)
        {
            // If the pass name ends in .adf assume a file within the
            // coverage has been selected, and strip that off the coverage
            // name.
            string pszCoverName = pszInputName;
            if (pszCoverName.Substring(pszCoverName.Length - 4) == ".adf")
            {
                int lastPathSep = pszCoverName.LastIndexOf(Path.DirectorySeparatorChar);
                if (lastPathSep > 0)
                {
                    pszCoverName = pszCoverName.Substring(0, lastPathSep);
                }
            }

            // Allocate info structure.
            AIGInfo_t psInfo = new AIGInfo_t();
            psInfo.bHasWarned = 0;
            psInfo.pszCoverName = pszCoverName;

            // Read the header file.
            GridLib.AIGReadHeader(pszCoverName, psInfo);

            // Read the extents.
            GridLib.AIGReadBounds(pszCoverName, psInfo);

            // Compute the number of pixels and lines, and the number of tile files.
            if (psInfo.dfCellSizeX <= 0 || psInfo.dfCellSizeY <= 0)
            {
                AIGClose(psInfo);
                throw new InvalidOperationException(
                          "Illegal cell size : " +
                          psInfo.dfCellSizeX + " x " + psInfo.dfCellSizeY);
            }

            psInfo.nPixels = (int)
                ((psInfo.dfURX - psInfo.dfLLX + 0.5 * psInfo.dfCellSizeX)
                 / psInfo.dfCellSizeX);
            psInfo.nLines = (int)
                ((psInfo.dfURY - psInfo.dfLLY + 0.5 * psInfo.dfCellSizeY)
                / psInfo.dfCellSizeY);

            if (psInfo.nPixels <= 0 || psInfo.nLines <= 0)
            {
                AIGClose(psInfo);
                throw new InvalidOperationException("Invalid raster dimensions : " + psInfo.nPixels + " x " + psInfo.nLines);
            }

            if (psInfo.nBlockXSize <= 0 || psInfo.nBlockYSize <= 0 ||
                psInfo.nBlocksPerRow <= 0 || psInfo.nBlocksPerColumn <= 0 ||
                psInfo.nBlockXSize > int.MaxValue / psInfo.nBlocksPerRow ||
                psInfo.nBlockYSize > int.MaxValue / psInfo.nBlocksPerColumn)
            {
                AIGClose(psInfo);
                throw new InvalidOperationException("Invalid block characteristics: nBlockXSize=" + psInfo.nBlockXSize + "nBlockYSize=" + psInfo.nBlockYSize + ", nBlocksPerRow =" + psInfo.nBlocksPerRow + ", nBlocksPerColumn=" + psInfo.nBlocksPerColumn);
            }

            if (psInfo.nBlocksPerRow > int.MaxValue / psInfo.nBlocksPerColumn)
            {
                AIGClose(psInfo);
                throw new InvalidOperationException("Too many blocks");
            }

            psInfo.nTileXSize = psInfo.nBlockXSize * psInfo.nBlocksPerRow;
            psInfo.nTileYSize = psInfo.nBlockYSize * psInfo.nBlocksPerColumn;

            psInfo.nTilesPerRow = (psInfo.nPixels - 1) / psInfo.nTileXSize + 1;
            psInfo.nTilesPerColumn = (psInfo.nLines - 1) / psInfo.nTileYSize + 1;

            if (psInfo.nTilesPerRow > int.MaxValue / psInfo.nTilesPerColumn)
            {
                psInfo.nTilesPerRow = 0;
                psInfo.nTilesPerColumn = 0;
                AIGClose(psInfo);
                throw new InvalidOperationException("Too many tiles");
            }

            // Setup tile infos, but defer reading of tile data.
            psInfo.pasTileInfo = new AIGTileInfo[psInfo.nTilesPerRow * psInfo.nTilesPerColumn];
            for (int i = 0; i < psInfo.pasTileInfo.Length; i++)
            {
                psInfo.pasTileInfo[i] = new AIGTileInfo();
            }

            // Read the statistics.
            GridLib.AIGReadStatistics(pszCoverName, psInfo);

            return psInfo;
        }

        public static void AIGAccessTile(AIGInfo_t psInfo, int iTileX, int iTileY)
        {
            // Identify our tile.
            if (iTileX < 0 || iTileX >= psInfo.nTilesPerRow || iTileY < 0 || iTileY >= psInfo.nTilesPerColumn)
            {
                throw new InvalidOperationException();
            }

            var psTInfo = psInfo.pasTileInfo[iTileX + iTileY * psInfo.nTilesPerRow];
            if (psTInfo.fpGrid == null && psTInfo.bTriedToLoad == 0)
            {
                // Compute the basename.
                string szBasename = "w" + (iTileX + 1).ToString("D3") + ((iTileY == 0 ? 2 : iTileY) - 1).ToString("D3");

                // Open the file w001001.adf file itself.
                psTInfo.fpGrid = AIGLLOpen(Path.Combine(psInfo.pszCoverName, szBasename + ".adf"));
                psTInfo.bTriedToLoad = 1;

                // Read the block index file.
                GridLib.AIGReadBlockIndex(psInfo, psTInfo, szBasename);
            }
        }

        public static void AIGClose(AIGInfo_t psInfo)
        {
            if (psInfo.pasTileInfo != null)
            {
                for (int iTile = 0; iTile < psInfo.nTilesPerRow * psInfo.nTilesPerColumn; iTile++)
                {
                    if (psInfo.pasTileInfo[iTile].fpGrid != null)
                    {
                        psInfo.pasTileInfo[iTile].fpGrid.VSIFCloseL();
                    }
                }
            }
        }

        /// <summary>
        /// Low level fopen() replacement that will try provided, and
        /// upper cased versions of file names.
        /// </summary>
        public static VSILFILE AIGLLOpen(string pszFilename)
        {
            return new VSILFILE(pszFilename);
        }
    }
}
