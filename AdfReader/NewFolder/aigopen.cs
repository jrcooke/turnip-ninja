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
using System.Diagnostics;
using System.IO;

namespace AdfReader.NewFolder
{
    public static class AigOpen
    {
        /************************************************************************/
        /*                              AIGOpen()                               */
        /************************************************************************/

        public static AIGInfo_t AIGOpen(string pszInputName)
        {
            /* -------------------------------------------------------------------- */
            /*      If the pass name ends in .adf assume a file within the          */
            /*      coverage has been selected, and strip that off the coverage     */
            /*      name.                                                           */
            /* -------------------------------------------------------------------- */
            string pszCoverName = pszInputName;
            if (pszCoverName.Substring(pszCoverName.Length - 4) == ".adf")
            {
                int i;

                for (i = (int)pszCoverName.Length - 1; i > 0; i--)
                {
                    if (pszCoverName[i] == '\\' || pszCoverName[i] == '/')
                    {
                        pszCoverName =
                            (i > 0 ? pszCoverName.Substring(0, i) : "") +
                            '\0'.ToString() +
                            (i < pszCoverName.Length - 1 ? pszCoverName.Substring(i + 1) : "");
                        break;
                    }
                }

                if (i == 0)
                {
                    pszCoverName = ".";
                }
            }

            /* -------------------------------------------------------------------- */
            /*      Allocate info structure.                                        */
            /* -------------------------------------------------------------------- */
            AIGInfo_t psInfo = new AIGInfo_t();
            psInfo.bHasWarned = 0;
            psInfo.pszCoverName = pszCoverName;

            /* -------------------------------------------------------------------- */
            /*      Read the header file.                                           */
            /* -------------------------------------------------------------------- */
            GridLib.AIGReadHeader(pszCoverName, psInfo);

            /* -------------------------------------------------------------------- */
            /*      Read the extents.                                               */
            /* -------------------------------------------------------------------- */
            GridLib.AIGReadBounds(pszCoverName, psInfo);

            /* -------------------------------------------------------------------- */
            /*      Compute the number of pixels and lines, and the number of       */
            /*      tile files.                                                     */
            /* -------------------------------------------------------------------- */
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
                psInfo.nTilesPerRow = 0; // to avoid int32 overflow in AIGClose()
                psInfo.nTilesPerColumn = 0;
                AIGClose(psInfo);
                throw new InvalidOperationException("Too many tiles");
            }

            /* -------------------------------------------------------------------- */
            /*      Setup tile infos, but defer reading of tile data.               */
            /* -------------------------------------------------------------------- */
            psInfo.pasTileInfo = new AIGTileInfo[psInfo.nTilesPerRow * psInfo.nTilesPerColumn];
            for (int i = 0; i < psInfo.pasTileInfo.Length; i++)
            {
                psInfo.pasTileInfo[i] = new AIGTileInfo();
            }

            /* -------------------------------------------------------------------- */
            /*      Read the statistics.                                            */
            /* -------------------------------------------------------------------- */
            GridLib.AIGReadStatistics(pszCoverName, psInfo);

            return psInfo;
        }

        /************************************************************************/
        /*                           AIGAccessTile()                            */
        /************************************************************************/

        public static void AIGAccessTile(AIGInfo_t psInfo, int iTileX, int iTileY)
        {
            /* -------------------------------------------------------------------- */
            /*      Identify our tile.                                              */
            /* -------------------------------------------------------------------- */
            if (iTileX < 0 || iTileX >= psInfo.nTilesPerRow || iTileY < 0 || iTileY >= psInfo.nTilesPerColumn)
            {
                throw new InvalidOperationException();
            }

            var psTInfo = psInfo.pasTileInfo[iTileX + iTileY * psInfo.nTilesPerRow];
            if (psTInfo.fpGrid == null && psTInfo.bTriedToLoad == 0)
            {
                /* -------------------------------------------------------------------- */
                /*      Compute the basename.                                           */
                /* -------------------------------------------------------------------- */
                string szBasename = "w" + (iTileX + 1).ToString("D3") + ((iTileY == 0 ? 2 : iTileY) - 1).ToString("D3");

                /* -------------------------------------------------------------------- */
                /*      Open the file w001001.adf file itself.                          */
                /* -------------------------------------------------------------------- */
                psTInfo.fpGrid = AIGLLOpen(Path.Combine(psInfo.pszCoverName, szBasename + ".adf"));
                psTInfo.bTriedToLoad = 1;

                /* -------------------------------------------------------------------- */
                /*      Read the block index file.                                      */
                /* -------------------------------------------------------------------- */
                GridLib.AIGReadBlockIndex(psInfo, psTInfo, szBasename);
            }
        }

        /************************************************************************/
        /*                            AIGReadTile()                             */
        /************************************************************************/

        public static void AIGReadTile(AIGInfo_t psInfo, int nBlockXOff, int nBlockYOff, float[] panData)
        {
            /* -------------------------------------------------------------------- */
            /*      Compute our tile, and ensure it is accessible (open).  Then     */
            /*      reduce block x/y values to be the block within that tile.       */
            /* -------------------------------------------------------------------- */
            int iTileX = nBlockXOff / psInfo.nBlocksPerRow;
            int iTileY = nBlockYOff / psInfo.nBlocksPerColumn;

            AigOpen.AIGAccessTile(psInfo, iTileX, iTileY);

            AIGTileInfo psTInfo = psInfo.pasTileInfo[iTileX + iTileY * psInfo.nTilesPerRow];

            nBlockXOff -= iTileX * psInfo.nBlocksPerRow;
            nBlockYOff -= iTileY * psInfo.nBlocksPerColumn;

            /* -------------------------------------------------------------------- */
            /*      Request for tile from a file which does not exist - treat as    */
            /*      all nodata.                                                     */
            /* -------------------------------------------------------------------- */
            if (psTInfo.fpGrid == null)
            {
                for (int i = psInfo.nBlockXSize * psInfo.nBlockYSize - 1; i >= 0; i--)
                {
                    panData[i] = DefineConstants.ESRI_GRID_NO_DATA;
                }

                return;
            }

            /* -------------------------------------------------------------------- */
            /*      validate block id.                                              */
            /* -------------------------------------------------------------------- */
            int nBlockID = nBlockXOff + nBlockYOff * psInfo.nBlocksPerRow;
            if (nBlockID < 0 || nBlockID >= psInfo.nBlocksPerRow * psInfo.nBlocksPerColumn)
            {
                throw new InvalidOperationException("Illegal block requested.");
            }

            if (nBlockID >= psTInfo.nBlocks)
            {
                Debug.WriteLine("Request legal block, but from beyond end of block map.\n" + "Assuming all nodata.");
                for (int i = psInfo.nBlockXSize * psInfo.nBlockYSize - 1; i >= 0; i--)
                {
                    panData[i] = DefineConstants.ESRI_GRID_NO_DATA;
                }
            }

            /* -------------------------------------------------------------------- */
            /*      Read block.                                                     */
            /* -------------------------------------------------------------------- */
            GridLib.AIGReadBlock(
                psTInfo.fpGrid,
                psTInfo.panBlockOffset[nBlockID],
                psTInfo.panBlockSize[nBlockID],
                psInfo.nBlockXSize,
                psInfo.nBlockYSize,
                panData,
                psInfo.nCellType,
                psInfo.bCompressed);
        }

        /************************************************************************/
        /*                          AIGReadFloatTile()                          */
        /************************************************************************/

        public static void AIGReadFloatTile(AIGInfo_t psInfo, int nBlockXOff, int nBlockYOff, float[] pafData)

        {
            /* -------------------------------------------------------------------- */
            /*      Compute our tile, and ensure it is accessible (open).  Then     */
            /*      reduce block x/y values to be the block within that tile.       */
            /* -------------------------------------------------------------------- */
            int iTileX = nBlockXOff / psInfo.nBlocksPerRow;
            int iTileY = nBlockYOff / psInfo.nBlocksPerColumn;

            AIGAccessTile(psInfo, iTileX, iTileY);
            AIGTileInfo psTInfo = psInfo.pasTileInfo[iTileX + iTileY * psInfo.nTilesPerRow];


            nBlockXOff -= iTileX * psInfo.nBlocksPerRow;
            nBlockYOff -= iTileY * psInfo.nBlocksPerColumn;

            /* -------------------------------------------------------------------- */
            /*      Request for tile from a file which does not exist - treat as    */
            /*      all nodata.                                                     */
            /* -------------------------------------------------------------------- */
            if (psTInfo.fpGrid == null)
            {
                for (int i = psInfo.nBlockXSize * psInfo.nBlockYSize - 1; i >= 0; i--)
                {
                    pafData[i] = DefineConstants.ESRI_GRID_FLOAT_NO_DATA;
                }
                return;
            }

            /* -------------------------------------------------------------------- */
            /*      validate block id.                                              */
            /* -------------------------------------------------------------------- */
            int nBlockID = nBlockXOff + nBlockYOff * psInfo.nBlocksPerRow;
            if (nBlockID < 0 || nBlockID >= psInfo.nBlocksPerRow * psInfo.nBlocksPerColumn)
            {
                throw new InvalidOperationException("Illegal block requested.");
            }

            if (nBlockID >= psTInfo.nBlocks)
            {
                Debug.WriteLine("AIG", "Request legal block, but from beyond end of block map.\n" + "Assuming all nodata.");
                for (int i = psInfo.nBlockXSize * psInfo.nBlockYSize - 1; i >= 0; i--)
                {
                    pafData[i] = DefineConstants.ESRI_GRID_FLOAT_NO_DATA;
                }
                return;
            }

            throw new NotImplementedException();
            ///* -------------------------------------------------------------------- */
            ///*      Read block.                                                     */
            ///* -------------------------------------------------------------------- */
            //gridlib.AIGReadBlock(psTInfo.fpGrid, 
            //    psTInfo.panBlockOffset[nBlockID], 
            //    psTInfo.panBlockSize[nBlockID], 
            //    psInfo.nBlockXSize, psInfo.nBlockYSize, 
            //    (Int32)pafData, psInfo.nCellType, 
            //    psInfo.bCompressed);

            ///* -------------------------------------------------------------------- */
            ///*      Perform integer post processing.                                */
            ///* -------------------------------------------------------------------- */
            //UInt32[] panData = (UInt32)pafData;
            //int nPixels = psInfo.nBlockXSize * psInfo.nBlockYSize;

            //for (i = 0; i < nPixels; i++)
            //{
            //    pafData[i] = (float)panData[i];
            //}
        }

        /************************************************************************/
        /*                              AIGClose()                              */
        /************************************************************************/

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

        /************************************************************************/
        /*                             AIGLLOpen()                              */
        /*                                                                      */
        /*      Low level fopen() replacement that will try provided, and       */
        /*      upper cased versions of file names.                             */
        /************************************************************************/

        public static VSILFILE AIGLLOpen(string pszFilename)
        {
            return new VSILFILE(pszFilename);
        }
    }
}
