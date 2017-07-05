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
        /************************************************************************/
        /*                             DumpMagic()                              */
        /*                                                                      */
        /*      Dump the magic ``block type byte'' for each existing block.     */
        /************************************************************************/

        internal static void DumpMagic(AIGInfo_t psInfo, int bVerbose)
        {
            AIGTileInfo psTInfo = psInfo.pasTileInfo[0];
            for (int i = 0; i < psTInfo.nBlocks; i++)
            {
                byte byMagic = 0;
                int bReport = bVerbose;
                byte[] abyBlockSize = new byte[2];
                string pszMessage = "";

                if (psTInfo.panBlockSize[i] == 0)
                {
                    continue;
                }

                psTInfo.fpGrid.Seek(psTInfo.panBlockOffset[i]);
                psTInfo.fpGrid.ReadByteArray(abyBlockSize, 2);

                if (psInfo.nCellType == AIGInfo_t.AIG_CELLTYPE_INT && psInfo.bCompressed != 0)
                {
                    byMagic = psTInfo.fpGrid.ReadByte();

                    if (byMagic != 0 && byMagic != 0x43 && byMagic != 0x04
                        && byMagic != 0x08 && byMagic != 0x10 && byMagic != 0xd7
                        && byMagic != 0xdf && byMagic != 0xe0 && byMagic != 0xfc
                        && byMagic != 0xf8 && byMagic != 0xff && byMagic != 0x41
                        && byMagic != 0x40 && byMagic != 0x42 && byMagic != 0xf0
                        && byMagic != 0xcf && byMagic != 0x01)
                    {
                        pszMessage = "(unhandled magic number)";
                        bReport = 1;
                    }

                    if (byMagic == 0 && psTInfo.panBlockSize[i] > 8)
                    {
                        pszMessage = "(wrong size for 0x00 block, should be 8 bytes)";
                        bReport = 1;
                    }

                    if ((abyBlockSize[0] * 256 + abyBlockSize[1]) * 2 !=
                        psTInfo.panBlockSize[i])
                    {
                        pszMessage = "(block size in data doesn't match index)";
                        bReport = 1;
                    }
                }
                else
                {
                    if (psTInfo.panBlockSize[i] !=
                        psInfo.nBlockXSize * psInfo.nBlockYSize * sizeof(float))
                    {
                        pszMessage = "(floating point block size is wrong)";
                        bReport = 1;
                    }
                }

                if (bReport != 0)
                {
                    Console.Write(" {0:x2} {1,5:D} {2,5:D} @ {3:D} {4}\n", byMagic, i,
                        psTInfo.panBlockSize[i],
                        psTInfo.panBlockOffset[i],
                        pszMessage);
                }
            }
        }

        /************************************************************************/
        /*                                main()                                */
        /************************************************************************/
        public static float[][] Test(string folder, bool showMagic)
        {
            /* -------------------------------------------------------------------- */
            /*      Open dataset.                                                   */
            /* -------------------------------------------------------------------- */
            AIGInfo_t psInfo = AigOpen.AIGOpen(folder);
            AigOpen.AIGAccessTile(psInfo, 0, 0);

            /* -------------------------------------------------------------------- */
            /*      Dump general information                                        */
            /* -------------------------------------------------------------------- */
            Console.Write("{0:D} pixels x {1:D} lines.\n", psInfo.nPixels, psInfo.nLines);
            Console.Write("Lower Left = ({0:f},{1:f})   Upper Right = ({2:f},{3:f})\n", psInfo.dfLLX, psInfo.dfLLY, psInfo.dfURX, psInfo.dfURY);

            if (psInfo.nCellType == AIGInfo_t.AIG_CELLTYPE_INT)
            {
                Console.Write("{0} Integer coverage, {1:D}x{2:D} blocks.\n", psInfo.bCompressed != 0 ? "Compressed" : "Uncompressed", psInfo.nBlockXSize, psInfo.nBlockYSize);
            }
            else
            {
                Console.Write("{0} Floating point coverage, {1:D}x{2:D} blocks.\n", psInfo.bCompressed != 0 ? "Compressed" : "Uncompressed", psInfo.nBlockXSize, psInfo.nBlockYSize);
            }

            Console.Write("Stats - Min={0:f}, Max={1:f}, Mean={2:f}, StdDev={3:f}\n", psInfo.dfMin, psInfo.dfMax, psInfo.dfMean, psInfo.dfStdDev);

            /* -------------------------------------------------------------------- */
            /*      Do we want a dump of all the ``magic'' numbers for              */
            /*      instantiated blocks?                                            */
            /* -------------------------------------------------------------------- */
            if (showMagic)
            {
                DumpMagic(psInfo, 0);
            }

            /* -------------------------------------------------------------------- */
            /*      Read a block, and report its contents.                          */
            /* -------------------------------------------------------------------- */
            AIGTileInfo psTInfo = psInfo.pasTileInfo[0];
            float[][] output = new float[psInfo.nPixels][];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = new float[psInfo.nLines];
            }

            int blocksPerX = psInfo.nPixels / psInfo.nBlockXSize + 1;

            float[] panRaster = new float[psInfo.nBlockXSize * psInfo.nBlockYSize];
            for (int nBlock = 0; nBlock < psTInfo.nBlocks; nBlock++)
            {
                GridLib.AIGReadBlock(
                    psTInfo.fpGrid,
                    psTInfo.panBlockOffset[nBlock],
                    psTInfo.panBlockSize[nBlock],
                    psInfo.nBlockXSize,
                    psInfo.nBlockYSize,
                    panRaster,
                    psInfo.nCellType,
                    psInfo.bCompressed);

                int tileOffsetX = (nBlock % blocksPerX) * psInfo.nBlockXSize;
                int tileOffsetY = (nBlock / blocksPerX) * psInfo.nBlockYSize;


                for (int j = 0; j < psInfo.nBlockYSize; j++)
                {
                    for (int i = 0; i < psInfo.nBlockXSize; i++)
                    {
                        float value = panRaster[i + j * psInfo.nBlockXSize];

                        if (i + tileOffsetX >= output.Length || j + tileOffsetY >= output[i + tileOffsetX].Length)
                        {
                           // Console.WriteLine("Warning! Out of bounds");
                        }
                        else
                        {
                            if (output[i + tileOffsetX][j + tileOffsetY] != 0.0)
                            {
                                Console.WriteLine("Warning! Overwriting values");
                            }

                            output[i + tileOffsetX][j + tileOffsetY] = value;
                        }

                    }
                }
            }

            AigOpen.AIGClose(psInfo);

            return output;
        }
    }
}
