namespace AdfReader
{
    public class ChunkHolder<T>
    {
        public T[][] Data { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public ChunkHolder(int width, int height)
        {
            this.Width = width;
            this.Height = height;

            Data = new T[width][];
            for (int i = 0; i < width; i++)
            {
                Data[i] = new T[height];
            }
        }
    }
}
