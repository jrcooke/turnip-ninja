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
        /************************************************************************/
        /*                    AIGProcessRaw32bitFloatBlock()                    */
        /*                                                                      */
        /*      Process a block using ``00'' (32 bit) raw format.               */
        /************************************************************************/

        private static byte[] AIGProcessRaw32BitFloatBlockBuffer = new byte[4];

        internal static float[] AIGProcessRaw32BitFloatBlock(
            Byte[] pabyCur,
            int pabyCurOffset,
            int nDataSize,
            int nMin,
            int nBlockXSize,
            int nBlockYSize,
            float[] pafData)
        {
            if (nDataSize < nBlockXSize * nBlockYSize * 4)
            {
                throw new InvalidOperationException("Block too small");
            }

            /* -------------------------------------------------------------------- */
            /*      Collect raw data.                                               */
            /* -------------------------------------------------------------------- */
            int pabyCurIndex = pabyCurOffset;
            for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
            {
                pafData[i] = MyBitConverter.ToSingle(pabyCur, pabyCurIndex);
                pabyCurIndex += 4;
            }

            return pafData;
        }

        // /************************************************************************/
        // /*                      AIGProcessIntConstBlock()                       */
        // /*                                                                      */
        // /*      Process a block using ``00'' constant 32bit integer format.     */
        // /************************************************************************/
        //
        //internal static void AIGProcessIntConstBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    /* -------------------------------------------------------------------- */
        //    /* Apply constant min value.                                           */
        //    /* -------------------------------------------------------------------- */
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        panData[i] = nMin;
        //    }
        //}
        //
        // /************************************************************************/
        // /*                         AIGRolloverSignedAdd()                       */
        // /************************************************************************/
        //
        //internal static Int32 AIGRolloverSignedAdd(Int32 a, Int32 b)
        //{
        //    // Not really portable as assumes complement to 2 representation
        //    // but AIG assumes typical unsigned rollover on signed
        //    // integer operations.
        //    return (Int32)((UInt32)(a) + (UInt32)(b));
        //}

        // /************************************************************************/
        // /*                         AIGProcess32bitRawBlock()                    */
        // /*                                                                      */
        // /*      Process a block using ``20'' (thirty two bit) raw format.        */
        // /************************************************************************/
        //internal static Int32[] AIGProcessRaw32BitBlock(
        //    Byte[] pabyCur,
        //    int pabyCurIndex,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize)
        //{
        //    if (nDataSize < nBlockXSize * nBlockYSize * 4)
        //    {
        //        throw new InvalidOperationException("Block too small");
        //    }

        //    /* -------------------------------------------------------------------- */
        //    /*      Collect raw data.                                               */
        //    /* -------------------------------------------------------------------- */
        //    Int32[] panData = new Int32[nBlockXSize * nBlockYSize];
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        var data = MyBitConverter.ToInt32(pabyCur, pabyCurIndex);
        //        panData[i] = AIGRolloverSignedAdd(data, nMin);
        //        pabyCurIndex += 4;
        //    }
        //
        //    return panData;
        //}

        // /************************************************************************/
        // /*                         AIGProcess16bitRawBlock()                    */
        // /*                                                                      */
        // /*      Process a block using ``10'' (sixteen bit) raw format.          */
        // /************************************************************************/

        //internal static void AIGProcessRaw16BitBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    if (nDataSize < nBlockXSize * nBlockYSize * 2)
        //    {
        //        throw new InvalidOperationException("Block too small");
        //    }

        //    /* -------------------------------------------------------------------- */
        //    /*      Collect raw data.                                               */
        //    /* -------------------------------------------------------------------- */
        //    int pabyCurIndex = pabyCurOffset;
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        panData[i] = pabyCur[pabyCurIndex] * 256 + pabyCur[pabyCurIndex + 1] + nMin;
        //        pabyCurIndex += 2;
        //    }
        //}

        // /************************************************************************/
        // /*                         AIGProcess4BitRawBlock()                     */
        // /*                                                                      */
        // /*      Process a block using ``08'' raw format.                        */
        // /************************************************************************/

        //internal static void AIGProcessRaw4BitBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    if (nDataSize < (nBlockXSize * nBlockYSize + 1) / 2)
        //    {
        //        throw new InvalidOperationException("Block too small");
        //    }

        //    /* -------------------------------------------------------------------- */
        //    /*      Collect raw data.                                               */
        //    /* -------------------------------------------------------------------- */
        //    int pabyCurIndex = pabyCurOffset;
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        if (i % 2 == 0)
        //        {
        //            panData[i] = ((pabyCur[pabyCurIndex] & 0xf0) >> 4) + nMin;
        //        }
        //        else
        //        {
        //            panData[i] = (pabyCur[pabyCurIndex++] & 0xf) + nMin;
        //        }
        //    }
        //}

        // /************************************************************************/
        // /*                       AIGProcess1BitRawBlock()                       */
        // /*                                                                      */
        // /*      Process a block using ``0x01'' raw format.                      */
        // /************************************************************************/

        //internal static void AIGProcessRaw1BitBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    if (nDataSize < (nBlockXSize * nBlockYSize + 7) / 8)
        //    {
        //        throw new InvalidOperationException("Block too small");
        //    }

        //    /* -------------------------------------------------------------------- */
        //    /*      Collect raw data.                                               */
        //    /* -------------------------------------------------------------------- */
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        if ((pabyCur[(i + pabyCurOffset) >> 3] & (0x80 >> ((i + pabyCurOffset) & 0x7))) != 0)
        //        {
        //            panData[i] = 1 + nMin;
        //        }
        //        else
        //        {
        //            panData[i] = 0 + nMin;
        //        }
        //    }
        //}

        // /************************************************************************/
        // /*                         AIGProcessRawBlock()                         */
        // /*                                                                      */
        // /*      Process a block using ``08'' raw format.                        */
        // /************************************************************************/

        //internal static void AIGProcessRawBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    if (nDataSize < nBlockXSize * nBlockYSize)
        //    {
        //        throw new InvalidOperationException("Block too small");
        //    }

        //    /* -------------------------------------------------------------------- */
        //    /*      Collect raw data.                                               */
        //    /* -------------------------------------------------------------------- */
        //    for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    {
        //        panData[i] = pabyCur[i + pabyCurOffset] + nMin;
        //    }
        //}

        // /************************************************************************/
        // /*                         AIGProcessFFBlock()                          */
        // /*                                                                      */
        // /*      Process a type 0xFF (CCITT RLE) compressed block.               */
        // /************************************************************************/
        //
        //internal static void AIGProcessFFBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    /* -------------------------------------------------------------------- */
        //    /*      Convert CCITT compress bitstream into 1bit raw data.            */
        //    /* -------------------------------------------------------------------- */
        //    int nDstBytes = (nBlockXSize * nBlockYSize + 7) / 8;
        //    byte[] pabyIntermediate = new byte[nDstBytes];

        //    throw new NotImplementedException();
        //    //DecompressCCITTRLETile(ref pabyCur, nDataSize, ref pabyIntermediate, nDstBytes, nBlockXSize, nBlockYSize);

        //    ///* -------------------------------------------------------------------- */
        //    ///*      Convert the bit buffer into 32bit integers and account for      */
        //    ///*      nMin.                                                           */
        //    ///* -------------------------------------------------------------------- */
        //    //for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
        //    //{
        //    //    if ((pabyIntermediate[i >> 3] & (0x80 >> (i & 0x7))) != 0)
        //    //    {
        //    //        panData[i] = nMin + 1;
        //    //    }
        //    //    else
        //    //    {
        //    //        panData[i] = nMin;
        //    //    }
        //    //}
        //}
        //
        // /************************************************************************/
        // /*                          AIGProcessBlock()                           */
        // /*                                                                      */
        // /*      Process a block using ``D7'', ``E0'' or ``DF'' compression.     */
        // /************************************************************************/
        //
        //internal static void AIGProcessBlock(
        //    Byte[] pabyCur,
        //    int pabyCurOffset,
        //    int nDataSize,
        //    int nMin,
        //    int nMagic,
        //    int nBlockXSize,
        //    int nBlockYSize,
        //    Int32[] panData)
        //{
        //    int nTotPixels;
        //    int nPixels;
        //    int i;
        //    int pabyCurIndex = pabyCurOffset;

        //    /* ==================================================================== */
        //    /*     Process runs till we are done.                                  */
        //    /* ==================================================================== */
        //    nTotPixels = nBlockXSize * nBlockYSize;
        //    nPixels = 0;

        //    while (nPixels < nTotPixels && nDataSize > 0)
        //    {
        //        int nMarker = (pabyCurIndex++);

        //        nDataSize--;

        //        /* -------------------------------------------------------------------- */
        //        /*      Repeat data - four byte data block (0xE0)                       */
        //        /* -------------------------------------------------------------------- */
        //        if (nMagic == 0xE0)
        //        {
        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) +
        //                    " values, got " + nMarker + ".");
        //            }

        //            if (nDataSize < 4)
        //            {
        //                throw new InvalidOperationException("Block too small");
        //            }

        //            Int32 nValue = MyBitConverter.ToInt32(pabyCur, pabyCurIndex);
        //            pabyCurIndex += 4;
        //            nDataSize -= 4;

        //            nValue = AIGRolloverSignedAdd(nValue, nMin);
        //            for (i = 0; i < nMarker; i++)
        //            {
        //                panData[nPixels++] = nValue;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Repeat data - two byte data block (0xF0)                        */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMagic == 0xF0)
        //        {
        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker + ".");
        //            }

        //            if (nDataSize < 2)
        //            {
        //                throw new InvalidOperationException("Block too small");
        //            }

        //            Int32 nValue = (pabyCur[pabyCurIndex] * 256 + pabyCur[pabyCurIndex + 1]) + nMin;
        //            pabyCurIndex += 2;
        //            nDataSize -= 2;

        //            for (i = 0; i < nMarker; i++)
        //            {
        //                panData[nPixels++] = nValue;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Repeat data - one byte data block (0xFC)                        */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMagic == 0xFC || nMagic == 0xF8)
        //        {
        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker + ".");
        //            }

        //            if (nDataSize < 1)
        //            {
        //                throw new InvalidOperationException("Block too small");
        //            }

        //            int nValue = pabyCur[pabyCurIndex++] + nMin;
        //            nDataSize--;

        //            for (i = 0; i < nMarker; i++)
        //            {
        //                panData[nPixels++] = nValue;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Repeat data - no actual data, just assign minimum (0xDF)        */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMagic == 0xDF && nMarker < 128)
        //        {
        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker + ".");
        //            }

        //            for (i = 0; i < nMarker; i++)
        //            {
        //                panData[nPixels++] = nMin;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Literal data (0xD7): 8bit values.                               */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMagic == 0xD7 && nMarker < 128)
        //        {
        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker + ".");
        //            }

        //            while (nMarker > 0 && nDataSize > 0)
        //            {
        //                panData[nPixels++] = pabyCur[pabyCurIndex++] + nMin;
        //                nMarker--;
        //                nDataSize--;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Literal data (0xCF): 16 bit values.                             */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMagic == 0xCF && nMarker < 128)
        //        {
        //            Int32 nValue = new Int32();

        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker);
        //            }

        //            while (nMarker > 0 && nDataSize >= 2)
        //            {
        //                nValue = pabyCur[pabyCurIndex] * 256 + pabyCur[pabyCurIndex + 1] + nMin;
        //                panData[nPixels++] = nValue;
        //                pabyCurIndex += 2;

        //                nMarker--;
        //                nDataSize -= 2;
        //            }
        //        }

        //        /* -------------------------------------------------------------------- */
        //        /*      Nodata repeat                                                   */
        //        /* -------------------------------------------------------------------- */
        //        else if (nMarker > 128)
        //        {
        //            nMarker = 256 - nMarker;

        //            if (nMarker + nPixels > nTotPixels)
        //            {
        //                throw new InvalidOperationException("Run too long in AIGProcessBlock, needed " + (nTotPixels - nPixels) + " values, got " + nMarker);
        //            }

        //            while (nMarker > 0)
        //            {
        //                panData[nPixels++] = DefineConstants.ESRI_GRID_NO_DATA;
        //                nMarker--;
        //            }
        //        }

        //        else
        //        {
        //            throw new InvalidOperationException();
        //        }

        //    }

        //    if (nPixels < nTotPixels || nDataSize < 0)
        //    {
        //        throw new InvalidOperationException("Ran out of data processing block with nMagic=" + nMagic);
        //    }
        //}

        /************************************************************************/
        /*                            AIGReadBlock()                            */
        /*                                                                      */
        /*      Read a single block of integer grid data.                       */
        /************************************************************************/

        public static void AIGReadBlock(
            VSILFILE fp,
            UInt32 nBlockOffset,
            int nBlockSize,
            int nBlockXSize,
            int nBlockYSize,
            float[] panData,
            int nCellType,
            int bCompressed)
        {
            /* -------------------------------------------------------------------- */
            /*      If the block has zero size it is all dummies.                   */
            /* -------------------------------------------------------------------- */
            if (nBlockSize == 0)
            {
                for (int i = 0; i < nBlockXSize * nBlockYSize; i++)
                {
                    panData[i] = DefineConstants.ESRI_GRID_NO_DATA;
                }

                return;
            }

            /* -------------------------------------------------------------------- */
            /*      Read the block into memory.                                     */
            /* -------------------------------------------------------------------- */
            if (nBlockSize <= 0 || nBlockSize > 65535 * 2)
            {
                throw new InvalidOperationException("Invalid block size : " + nBlockSize);
            }

            Byte[] pabyRaw = new Byte[nBlockSize + 2];
            fp.Seek(nBlockOffset);
            fp.ReadByteArray(pabyRaw, nBlockSize + 2);

            /* -------------------------------------------------------------------- */
            /*      Verify the block size.                                          */
            /* -------------------------------------------------------------------- */
            if (nBlockSize != (pabyRaw[0] * 256 + pabyRaw[1]) * 2)
            {
                throw new InvalidOperationException("Block is corrupt, block size was " + ((pabyRaw[0] * 256 + pabyRaw[1]) * 2) +
                    ", but expected to be " + nBlockSize);
            }

            int nDataSize = nBlockSize;

            /* -------------------------------------------------------------------- */
            /*      Handle float files and uncompressed integer files directly.     */
            /* -------------------------------------------------------------------- */
            if (nCellType != AIGInfo_t.AIG_CELLTYPE_FLOAT)
            {
                throw new NotImplementedException();
            }

            AIGProcessRaw32BitFloatBlock(pabyRaw, 2, nDataSize, 0, nBlockXSize, nBlockYSize, panData);

            //      if (nCellType == AIGInfo_t.AIG_CELLTYPE_INT && bCompressed == 0)
            //      {
            //          throw new NotImplementedException();
            //          //panData = AIGProcessRaw32BitBlock(pabyRaw, 2, nDataSize, nMin, nBlockXSize, nBlockYSize);
            //          //return;
            //      }
            //
            //      /* -------------------------------------------------------------------- */
            //      /*      Collect minimum value.                                          */
            //      /* -------------------------------------------------------------------- */
            //
            //      /* The first 2 bytes that give the block size are not included in nDataSize */
            //      /* and have already been safely read */
            //      int pabyCurOffset = 2;
            //
            //      /* Need at least 2 byte to read the nMinSize and the nMagic */
            //      if (nDataSize < 2)
            //      {
            //          throw new InvalidOperationException("Corrupt block. Need 2 bytes to read nMagic and nMinSize, only " + nDataSize + " available");
            //      }
            //
            //      int nMagic = pabyRaw[pabyCurOffset];
            //      int nMinSize = pabyRaw[pabyCurOffset + 1];
            //      pabyCurOffset += 2;
            //      nDataSize -= 2;
            //
            //      /* Need at least nMinSize bytes to read the nMin value */
            //      if (nDataSize < nMinSize)
            //      {
            //          throw new InvalidOperationException("Corrupt block. Need " + nMinSize + " bytes to read nMin. Only " + nDataSize + " available");
            //      }
            //
            //      if (nMinSize > 4)
            //      {
            //          throw new InvalidOperationException("Corrupt 'minsize' of " + nMinSize + " in block header.  Read aborted.");
            //      }
            //
            //      int nMin = 0;
            //      if (nMinSize == 4)
            //      {
            //          nMin = MyBitConverter.ToInt32(pabyRaw, pabyCurOffset);
            //          pabyCurOffset += 4;
            //      }
            //      else
            //      {
            //          nMin = 0;
            //          for (int i = 0; i < nMinSize; i++)
            //          {
            //              nMin = nMin * 256 + pabyRaw[pabyCurOffset];
            //              pabyCurOffset++;
            //          }
            //
            //          /* If nMinSize = 0, then we might have only 4 bytes in pabyRaw */
            //          /* don't try to read the 5th one then */
            //          if (nMinSize != 0 && pabyRaw[4] > 127)
            //          {
            //              if (nMinSize == 2)
            //              {
            //                  nMin = nMin - 65536;
            //              }
            //              else if (nMinSize == 1)
            //              {
            //                  nMin = nMin - 256;
            //              }
            //              else if (nMinSize == 3)
            //              {
            //                  nMin = nMin - 256 * 256 * 256;
            //              }
            //          }
            //      }
            //
            //      nDataSize -= nMinSize;
            //
            // /* -------------------------------------------------------------------- */
            // /*	Call an appropriate handler depending on magic code.		*/
            // /* -------------------------------------------------------------------- */
            //if (nMagic == 0x08)
            //{
            //    AIGProcessRawBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else if (nMagic == 0x04)
            //{
            //    AIGProcessRaw4BitBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else if (nMagic == 0x01)
            //{
            //    AIGProcessRaw1BitBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else if (nMagic == 0x00)
            //{
            //    AIGProcessIntConstBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else if (nMagic == 0x10)
            //{
            //    AIGProcessRaw16BitBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else if (nMagic == 0x20)
            //{
            //    panData = AIGProcessRaw32BitBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize);
            //}
            //else if (nMagic == 0xFF)
            //{
            //    AIGProcessFFBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nBlockXSize, nBlockYSize, panData);
            //}
            //else
            //{
            //    AIGProcessBlock(pabyRaw, pabyCurOffset, nDataSize, nMin, nMagic, nBlockXSize, nBlockYSize, panData);

            //    //if (eErr == CE_Failure)
            //    //{
            //    //    //C++ TO C# CONVERTER NOTE: This static local variable declaration (not allowed in C#) has been moved just prior to the method:
            //    //    //			static int bHasWarned = 0;

            //    //    for (i = 0; i < nBlockXSize * nBlockYSize; i++)
            //    //    {
            //    //        panData[i] = DefineConstants.ESRI_GRID_NO_DATA;
            //    //    }

            //    //    if (AIGReadBlock_bHasWarned == 0)
            //    //    {
            //    //        CPLError(CE_Warning, CPLE_AppDefined, "Unsupported Arc/Info Binary Grid tile of type 0x%X" + " encountered.\n" + "This and subsequent unsupported tile types set to" + " no data value.\n", nMagic);
            //    //        AIGReadBlock_bHasWarned = 1;
            //    //    }
            //    //}
            //}
        }

        /************************************************************************/
        /*                           AIGReadHeader()                            */
        /*                                                                      */
        /*      Read the hdr.adf file, and populate the given info structure    */
        /*      appropriately.                                                  */
        /************************************************************************/

        public static void AIGReadHeader(string pszCoverName, AIGInfo_t psInfo)

        {
            string pszHDRFilename;
            VSILFILE fp;
            Byte[] abyData = new Byte[308];
            int nHDRFilenameLen = pszCoverName.Length + 30;

            /* -------------------------------------------------------------------- */
            /*      Open the file hdr.adf file.                                     */
            /* -------------------------------------------------------------------- */
            pszHDRFilename = Path.Combine(pszCoverName, "hdr.adf");

            fp = AigOpen.AIGLLOpen(pszHDRFilename);

            if (fp == null)
            {
                throw new InvalidOperationException("Failed to open grid header file:\n" + pszHDRFilename);
            }

            /* -------------------------------------------------------------------- */
            /*      Read the whole file (we expect it to always be 308 bytes        */
            /*      long.                                                           */
            /* -------------------------------------------------------------------- */

            fp.ReadByteArray(abyData, 308);
            fp.VSIFCloseL();

            /* -------------------------------------------------------------------- */
            /*      Read the block size information.                                */
            /* -------------------------------------------------------------------- */
            psInfo.nCellType = MyBitConverter.ToInt32(abyData, 16);
            psInfo.bCompressed = MyBitConverter.ToInt32(abyData, 20);
            psInfo.nBlocksPerRow = MyBitConverter.ToInt32(abyData, 288);
            psInfo.nBlocksPerColumn = MyBitConverter.ToInt32(abyData, 292);
            psInfo.nBlockXSize = MyBitConverter.ToInt32(abyData, 296);
            psInfo.nBlockYSize = MyBitConverter.ToInt32(abyData, 304);
            psInfo.dfCellSizeX = MyBitConverter.ToDouble(abyData, 256);
            psInfo.dfCellSizeY = MyBitConverter.ToDouble(abyData, 264);
        }

        /************************************************************************/
        /*                         AIGReadBlockIndex()                          */
        /*                                                                      */
        /*      Read the w001001x.adf file, and populate the given info         */
        /*      structure with the block offsets, and sizes.                    */
        /************************************************************************/

        public static void AIGReadBlockIndex(AIGInfo_t psInfo, AIGTileInfo psTInfo, string pszBasename)
        {
            /* -------------------------------------------------------------------- */
            /*      Open the file hdr.adf file.                                     */
            /* -------------------------------------------------------------------- */
            string pszHDRFilename = Path.Combine(psInfo.pszCoverName, pszBasename + "x.adf");
            VSILFILE fp = AigOpen.AIGLLOpen(pszHDRFilename);

            /* -------------------------------------------------------------------- */
            /*      Verify the magic number.  This is often corrupted by CR/LF      */
            /*      translation.                                                    */
            /* -------------------------------------------------------------------- */
            Byte[] abyHeader = new Byte[8];
            fp.ReadByteArray(abyHeader, 8);
            if (abyHeader[3] == 0x0D && abyHeader[4] == 0x0A)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("w001001x.adf file header has been corrupted by unix to dos text conversion.");
            }

            if (abyHeader[0] != 0x00 || abyHeader[1] != 0x00 || abyHeader[2] != 0x27 || abyHeader[3] != 0x0A || abyHeader[4] != 0xFF || abyHeader[5] != 0xFF)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException("w001001x.adf file header magic number is corrupt.");
            }

            /* -------------------------------------------------------------------- */
            /*      Get the file length (in 2 byte shorts)                          */
            /* -------------------------------------------------------------------- */
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

            /* -------------------------------------------------------------------- */
            /*      Allocate buffer, and read the file (from beyond the header)     */
            /*      into the buffer.                                                */
            /* -------------------------------------------------------------------- */
            psTInfo.nBlocks = (int)((nLength - 100) / 8);
            UInt32[] panIndex = new UInt32[2 * psTInfo.nBlocks];
            if (panIndex == null)
            {
                fp.VSIFCloseL();
                throw new InvalidOperationException();
            }

            fp.Seek(100);
            fp.ReadUInt32Array(panIndex, 2 * psTInfo.nBlocks);
            fp.VSIFCloseL();

            /* -------------------------------------------------------------------- */
            /*        Allocate AIGInfo block info arrays.                           */
            /* -------------------------------------------------------------------- */
            psTInfo.panBlockOffset = new UInt32[psTInfo.nBlocks];
            psTInfo.panBlockSize = new int[psTInfo.nBlocks];

            /* -------------------------------------------------------------------- */
            /*      Populate the block information.                                 */
            /* -------------------------------------------------------------------- */
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

        /************************************************************************/
        /*                         AIGReadStatistics()                          */
        /*                                                                      */
        /*      Read the sta.adf file for the layer statistics.                 */
        /************************************************************************/

        public static void AIGReadStatistics(string pszCoverName, AIGInfo_t psInfo)
        {
            psInfo.dfMin = 0.0;
            psInfo.dfMax = 0.0;
            psInfo.dfMean = 0.0;
            psInfo.dfStdDev = -1.0;

            /* -------------------------------------------------------------------- */
            /*      Open the file sta.adf file.                                     */
            /* -------------------------------------------------------------------- */
            string pszHDRFilename = Path.Combine(pszCoverName, "sta.adf");
            VSILFILE fp = AigOpen.AIGLLOpen(pszHDRFilename);

            /* -------------------------------------------------------------------- */
            /*      Get the contents - 3 or 4 doubles.                              */
            /* -------------------------------------------------------------------- */
            double[] adfStats = new double[4];
            fp.ReadDoubleArray(adfStats, 4);
            fp.VSIFCloseL();

            psInfo.dfMin = adfStats[0];
            psInfo.dfMax = adfStats[1];
            psInfo.dfMean = adfStats[2];
            psInfo.dfStdDev = adfStats[3];
        }
    }
}
