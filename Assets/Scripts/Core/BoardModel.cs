using System;

namespace Match3.Core
{
    public sealed class BoardModel
    {
        public readonly int width;
        public readonly int height;

        private readonly Piece?[,] grid;

        public BoardModel(int width, int height)
        {
            this.width = width;
            this.height = height;
            grid = new Piece?[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        public Piece? Get(int x, int y) => grid[x, y];

        public void Set(int x, int y, Piece? piece) => grid[x, y] = piece;

        public void Swap(int ax, int ay, int bx, int by)
        {
            var a = grid[ax, ay];
            var b = grid[bx, by];
            grid[ax, ay] = b;
            grid[bx, by] = a;
        }
    }
}
