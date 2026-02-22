using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public readonly struct Move
    {
        public readonly Cell from;
        public readonly Cell to;

        public Move(Cell from, Cell to)
        {
            this.from = from;
            this.to = to;
        }
    }

    public static class GravitySolver
    {
        // hasCell(x,y) == true  -> клетка существует
        // hasCell(x,y) == false -> дырка (клетки нет), туда падать нельз€
        public static void Apply(
            BoardModel board,
            System.Func<int, int, bool> hasCell,
            out List<Move> moves,
            out List<Cell> spawnCells)
        {
            hasCell ??= ((x, y) => true);

            moves = new List<Move>(board.width * board.height);
            spawnCells = new List<Cell>(board.width * board.height);

            for (int x = 0; x < board.width; x++)
            {
                int y = 0;

                while (y < board.height)
                {
                    // 1) пропускаем дырки
                    while (y < board.height && !hasCell(x, y)) y++;
                    if (y >= board.height) break;

                    // 2) нашли сегмент существующих клеток [segStart .. segEnd]
                    int segStart = y;
                    while (y < board.height && hasCell(x, y)) y++;
                    int segEnd = y - 1;

                    // 3) "сжимаем" фишки ¬Ќ”“–» сегмента вниз (к segStart)
                    int writeY = segStart;

                    for (int readY = segStart; readY <= segEnd; readY++)
                    {
                        var p = board.Get(x, readY);
                        if (p == null) continue;

                        if (readY != writeY)
                        {
                            board.Set(x, writeY, p);
                            board.Set(x, readY, null);
                            moves.Add(new Move(new Cell(x, readY), new Cell(x, writeY)));
                        }

                        writeY++;
                    }

                    // 4) всЄ, что выше writeY в сегменте Ч пустые клетки сегмента, туда спавним
                    for (int yy = writeY; yy <= segEnd; yy++)
                        spawnCells.Add(new Cell(x, yy));
                }
            }
        }
    }
}