namespace Match3.Core

{
    public enum SpecialType
    {
        None,
        LineHorizontal,
        LineVertical,
        Bomb4, Bomb5, Bomb6, Bomb7,
        Cart
    }

    public readonly struct Cell
    {
        public readonly int x, y;
        public Cell(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Piece
    {
        public int type;
        public SpecialType special;

        public Piece(int type, SpecialType special = SpecialType.None)
        {
            this.type = type;
            this.special = special;
        }
    }
}
