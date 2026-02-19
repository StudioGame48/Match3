using System.Collections.Generic;

namespace Match3.Core
{
    public static class MatchFinder
    {
        public static List<Cell> FindMatches(BoardModel board)
        {
            var result = new HashSet<(int x, int y)>();

            for (int x = 0; x < board.width; x++)
                for (int y = 0; y < board.height; y++)
                {
                    var p = board.Get(x, y);
                    if (p == null) continue;

                    // ✅ спецки НЕ участвуют в матчах
                    if (p.Value.special != SpecialType.None) continue;

                    int type = p.Value.type;

                    // horizontal
                    if (x <= board.width - 3)
                    {
                        var p1 = board.Get(x + 1, y);
                        var p2 = board.Get(x + 2, y);

                        if (p1 != null && p2 != null &&
                            p1.Value.special == SpecialType.None &&
                            p2.Value.special == SpecialType.None &&
                            p1.Value.type == type && p2.Value.type == type)
                        {
                            result.Add((x, y));
                            result.Add((x + 1, y));
                            result.Add((x + 2, y));
                        }
                    }

                    // vertical
                    if (y <= board.height - 3)
                    {
                        var p1 = board.Get(x, y + 1);
                        var p2 = board.Get(x, y + 2);

                        if (p1 != null && p2 != null &&
                            p1.Value.special == SpecialType.None &&
                            p2.Value.special == SpecialType.None &&
                            p1.Value.type == type && p2.Value.type == type)
                        {
                            result.Add((x, y));
                            result.Add((x, y + 1));
                            result.Add((x, y + 2));
                        }
                    }
                }

            var list = new List<Cell>(result.Count);
            foreach (var (mx, my) in result)
                list.Add(new Cell(mx, my));

            return list;
        }
    }
}
