using Match3.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game.Utils
{
    public static class MatchGrouping
    {
        // Группируем совпадения в “островки” по 4-соседству, но только одинакового типа
        public static List<List<Vector2Int>> BuildMatchGroups(BoardModel model, List<Cell> cells)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var c in cells)
                set.Add(new Vector2Int(c.x, c.y));

            var groups = new List<List<Vector2Int>>();
            var used = new HashSet<Vector2Int>();

            foreach (var start in set)
            {
                if (used.Contains(start)) continue;

                var piece = model.Get(start.x, start.y);
                if (!piece.HasValue)
                {
                    used.Add(start);
                    continue;
                }

                int startType = piece.Value.type;

                var group = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                used.Add(start);

                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    group.Add(p);

                    var neighbors = new[]
                    {
                        p + Vector2Int.right,
                        p + Vector2Int.left,
                        p + Vector2Int.up,
                        p + Vector2Int.down
                    };

                    foreach (var n in neighbors)
                    {
                        if (!set.Contains(n) || used.Contains(n)) continue;

                        var np = model.Get(n.x, n.y);
                        if (!np.HasValue) continue;
                        if (np.Value.type != startType) continue;

                        used.Add(n);
                        queue.Enqueue(n);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }
    }
}