using System.Collections.Generic;
using UnityEngine;

namespace Match3.Core
{
    public static class RefillSolver
    {
        public static int GetSafeType(BoardModel board, int x, int y, int typesCount)
        {
            var available = new List<int>(typesCount);
            for (int i = 0; i < typesCount; i++) available.Add(i);

            // horizontal check (x-1, x-2)
            if (x >= 2)
            {
                var a = board.Get(x - 1, y);
                var b = board.Get(x - 2, y);
                if (a != null && b != null && a.Value.type == b.Value.type)
                    available.Remove(a.Value.type);
            }

            // vertical check (y-1, y-2)
            if (y >= 2)
            {
                var a = board.Get(x, y - 1);
                var b = board.Get(x, y - 2);
                if (a != null && b != null && a.Value.type == b.Value.type)
                    available.Remove(a.Value.type);
            }

            if (available.Count == 0)
                return Random.Range(0, typesCount);

            return available[Random.Range(0, available.Count)];
        }
    }
}
