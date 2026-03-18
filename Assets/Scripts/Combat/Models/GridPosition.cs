using System;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public struct GridPosition
    {
        public int X;
        public int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistanceTo(GridPosition other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public bool IsInside(int width, int height)
        {
            return X >= 0 && Y >= 0 && X < width && Y < height;
        }
    }
}
