using Match3.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game.Systems
{
    public sealed class SpecialSystem
    {
        private readonly BoardModel _model;

        public SpecialSystem(BoardModel model)
        {
            _model = model;
        }

        public bool IsCartAt(int x, int y)
        {
            var p = _model.Get(x, y);
            return p.HasValue && p.Value.special == SpecialType.Cart;
        }

        private void AddIfInBounds(HashSet<Vector2Int> set, int x, int y)
        {
            if (_model.InBounds(x, y))
                set.Add(new Vector2Int(x, y));
        }

        // спец-цепочки: бомба взрывает другую бомбу
        public void ExpandSpecials(HashSet<Vector2Int> toDestroy)
        {
            var q = new Queue<Vector2Int>(toDestroy);

            while (q.Count > 0)
            {
                var c = q.Dequeue();
                var p = _model.Get(c.x, c.y);
                if (!p.HasValue) continue;

                var sp = p.Value.special;
                if (sp == SpecialType.None) continue;

                var extra = new HashSet<Vector2Int>();

                switch (sp)
                {
                    case SpecialType.Bomb4:
                        AddIfInBounds(extra, c.x, c.y);
                        AddIfInBounds(extra, c.x + 1, c.y);
                        AddIfInBounds(extra, c.x - 1, c.y);
                        AddIfInBounds(extra, c.x, c.y + 1);
                        AddIfInBounds(extra, c.x, c.y - 1);
                        break;

                    case SpecialType.Bomb5:
                        for (int dx = -2; dx <= 2; dx++)
                            for (int dy = -2; dy <= 2; dy++)
                            {
                                if (Mathf.Abs(dx) == 2 && Mathf.Abs(dy) == 2) continue; // пропускаем углы
                                AddIfInBounds(extra, c.x + dx, c.y + dy);
                            }
                        break;

                    case SpecialType.Bomb6:
                        for (int dx = -3; dx <= 3; dx++)
                            for (int dy = -3; dy <= 3; dy++)
                            {
                                int ax = Mathf.Abs(dx);
                                int ay = Mathf.Abs(dy);
                                if (ax <= 3 && ay <= 3 && (ax + ay) <= 4)
                                    AddIfInBounds(extra, c.x + dx, c.y + dy);
                            }
                        break;

                    case SpecialType.Bomb7:
                        for (int dx = -4; dx <= 4; dx++)
                            for (int dy = -4; dy <= 4; dy++)
                            {
                                int ax = Mathf.Abs(dx);
                                int ay = Mathf.Abs(dy);
                                if (ax <= 4 && ay <= 4 && (ax + ay) <= 6)
                                    AddIfInBounds(extra, c.x + dx, c.y + dy);
                            }
                        break;
                }

                foreach (var e in extra)
                    if (toDestroy.Add(e))
                        q.Enqueue(e);
            }
        }

        public int ComputeChargeForPiece(Piece? piece)
        {
            if (!piece.HasValue) return 0;

            return piece.Value.special switch
            {
                SpecialType.Bomb4 => 12,
                SpecialType.Bomb5 => 20,
                SpecialType.Bomb6 => 32,
                SpecialType.Bomb7 => 45,
                _ => 0
            };
        }
    }
}