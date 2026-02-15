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
        // ¬озвращает список перемещений (from -> to) дл€ всех кусков,
        // и список пустых клеток в верхней части колонок (куда надо заспавнить новые).
        public static void Apply(BoardModel board, out List<Move> moves, out List<Cell> spawnCells)
        {
            moves = new List<Move>(board.width * board.height);
            spawnCells = new List<Cell>(board.width * board.height);

            for (int x = 0; x < board.width; x++)
            {
                int writeY = 0; // куда "упадЄт" следующий кусок (снизу вверх)

                for (int y = 0; y < board.height; y++)
                {
                    var p = board.Get(x, y);
                    if (p == null) continue;

                    if (y != writeY)
                    {
                        // перемещаем piece в модель
                        board.Set(x, writeY, p);
                        board.Set(x, y, null);

                        moves.Add(new Move(new Cell(x, y), new Cell(x, writeY)));
                    }

                    writeY++;
                }

                // всЄ, что выше writeY - пустоты (спавн сверху)
                for (int y = writeY; y < board.height; y++)
                    spawnCells.Add(new Cell(x, y));
            }
        }
    }
}
