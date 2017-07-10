/******************************************************************************
 * $Id: aitest.c 36393 2016-11-21 14:25:42Z rouault $
 *
 * Project:  Arc/Info Binary Grid Translator
 * Purpose:  Test mainline for examining AIGrid files.
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
 *****************************************************************************/

using System;

namespace AdfReader.NewFolder
{
    public class AiTest
    {
        public static float[][] Test(string folder)
        {
            // Open dataset.
            AIGInfo_t psInfo = AigOpen.AIGOpen(folder);
            AigOpen.AIGAccessTile(psInfo, 0, 0);

            AIGTileInfo psTInfo = psInfo.pasTileInfo[0];
            float[][] output = new float[psInfo.nPixels][];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = new float[psInfo.nLines];
            }

            float[] panRaster = new float[psInfo.nBlockXSize * psInfo.nBlockYSize];
            for (int nBlock = 0; nBlock < psTInfo.nBlocks; nBlock++)
            {
                GridLib.AIGReadBlock(
                    psTInfo.fpGrid,
                    psTInfo.panBlockOffset[nBlock],
                    psTInfo.panBlockSize[nBlock],
                    psInfo.nBlockXSize,
                    psInfo.nBlockYSize,
                    panRaster);

                int tileOffsetX = (nBlock % psInfo.nBlocksPerRow) * psInfo.nBlockXSize;
                int tileOffsetY = (nBlock / psInfo.nBlocksPerRow) * psInfo.nBlockYSize;
                for (int i = 0; i < psInfo.nBlockXSize && i < output.Length - tileOffsetX; i++)
                {
                    for (int j = 0; j < psInfo.nBlockYSize && j < output[i + tileOffsetX].Length - tileOffsetY; j++)
                    {
                        output[j + tileOffsetY][i + tileOffsetX] = panRaster[i + j * psInfo.nBlockXSize];
                        //                        output[i + tileOffsetX][j + tileOffsetY] = panRaster[i + j * psInfo.nBlockXSize];
                    }
                }
            }

            AigOpen.AIGClose(psInfo);
            return output;
        }
    }
}
