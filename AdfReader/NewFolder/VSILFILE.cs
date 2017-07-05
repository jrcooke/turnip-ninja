using System;
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

        internal void ReadByteArray(byte[] value, int number)
        {
            for (int i = 0; i < number; i++)
            {
                value[i] = ReadByte();
            }
        }

        private byte[] readUInt32Buffer = new byte[4];
        internal UInt32 ReadUInt32()
        {
            ReadByteArray(readUInt32Buffer, 4);
            return MyBitConverter.ToUInt32(readUInt32Buffer, 0);
        }

        internal void ReadUInt32Array(uint[] value, int blocks)
        {
            for (int i = 0; i < blocks; i++)
            {
                value[i] = ReadUInt32();
            }
        }


        private byte[] readDoubleBuffer = new byte[8];
        internal double ReadDouble()
        {
            ReadByteArray(readDoubleBuffer, 8);
            return MyBitConverter.ToDouble(readDoubleBuffer, 0);
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
