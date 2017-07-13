using System;
using System.Collections.Generic;
using System.IO;

namespace AdfReader.NewFolder
{
    public class VSILFILE : IDisposable
    {
        private FileStream fs;

        public VSILFILE(string pszFilename)
        {
            this.fs = File.OpenRead(pszFilename);
        }

        internal void VSIFCloseL()
        {
            this.fs.Dispose();
        }

        internal void Seek(uint nBlockOffset)
        {
            this.fs.Seek(nBlockOffset, SeekOrigin.Begin);
        }

        internal byte ReadByte()
        {
            int data = this.fs.ReadByte();
            if (data == -1)
            {
                throw new InvalidOperationException();
            }

            return (byte)data;
        }

        internal void ReadByteArray(byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                value[i] = ReadByte();
            }
        }

        private byte[] readUInt32Buffer = new byte[4];
        internal UInt32 ReadUInt32()
        {
            ReadByteArray(readUInt32Buffer);
            return MyBitConverter.ToUInt32(readUInt32Buffer, 0);
        }

        internal void ReadUInt32Array(uint[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                value[i] = ReadUInt32();
            }
        }

        private byte[] readDoubleBuffer = new byte[8];
        internal double ReadDouble()
        {
            ReadByteArray(readDoubleBuffer);
            return MyBitConverter.ToDouble(readDoubleBuffer, 0);
        }

        private Dictionary<int, byte[]> readSingleBufferCache = new Dictionary<int, byte[]>();
        private byte[] readSingleBuffer2 = new byte[4];

        internal void ReadSingleArray(float[] panRaster)
        {
            byte[] readSingleBuffer;
            if (!readSingleBufferCache.TryGetValue(panRaster.Length, out readSingleBuffer))
            {
                readSingleBuffer = new byte[panRaster.Length * 4];
                readSingleBufferCache.Add(panRaster.Length, readSingleBuffer);
            }

            if (this.fs.Read(readSingleBuffer, 0, readSingleBuffer.Length) != readSingleBuffer.Length)
            {
                throw new InvalidOperationException();
            }

            for (int j = 0; j < panRaster.Length; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    readSingleBuffer2[3 - i] = readSingleBuffer[j * 4 + i];
                }

                panRaster[j] = BitConverter.ToSingle(readSingleBuffer2, 0);
            }
        }

        private byte[] readInt16Buffer = new byte[2];
        internal int ReadInt16()
        {
            ReadByteArray(readInt16Buffer);
            return MyBitConverter.ToInt16(readInt16Buffer, 0);
        }

        internal void ReadDoubleArray(double[] value, int blocks)
        {
            for (int i = 0; i < blocks; i++)
            {
                value[i] = ReadDouble();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.fs.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
