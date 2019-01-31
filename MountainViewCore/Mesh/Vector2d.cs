namespace MountainView.Mesh
{
    public struct Vector2d
    {
        public double X;
        public double Y;

        public Vector2d(double x, double y) : this()
        {
            X = x;
            Y = y;
        }

        internal double DistSqTo(ref Vector2d b)
        {
            double dx = X - b.X;
            double dy = Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
