using Match3.Core;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game
{
    public sealed class Match3Controller : MonoBehaviour
    {
        [Header("Board")]
        public int width = 6;
        public int height = 6;

        [Header("Prefabs")]
        public GameObject[] gemPrefabs;
        public Transform gridParent;

        [Header("Links")]
        public BoardView boardView;

        private BoardModel model;
        private GemView[,] views;

        [Header("Rules")]
        public int maxMoves = 20;
        [SerializeField] private int pointsPerGem = 10;

        private int movesLeft;
        private int score;

        public System.Action<int> OnMovesChanged;
        public System.Action<int> OnScoreChanged;
        public System.Action OnGameOver;

        private bool busy;

        [Header("Bomb Prefabs (match 4-7+)")]
        [SerializeField] private GameObject bomb4Prefab;
        [SerializeField] private GameObject bomb5Prefab;
        [SerializeField] private GameObject bomb6Prefab;
        [SerializeField] private GameObject bomb7Prefab;

        // запоминаем последний свап (чтобы ставить бомбу в “ход игрока”)
        private Vector2Int lastSwapA;
        private Vector2Int lastSwapB;

        void Start()
        {
            model = new BoardModel(width, height);
            views = new GemView[width, height];

            movesLeft = maxMoves;
            score = 0;
            PushUIState();

            if (boardView == null)
                boardView = FindFirstObjectByType<BoardView>();

            GenerateNoMatches();
            StartCoroutine(ResolveLoop()); // чистим случайные стартовые совпадения
        }

        public void PushUIState()
        {
            OnScoreChanged?.Invoke(score);
            OnMovesChanged?.Invoke(movesLeft);
        }

        void GenerateNoMatches()
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int type = RefillSolver.GetSafeType(model, x, y, gemPrefabs.Length);
                    model.Set(x, y, new Piece(type));
                    SpawnViewAtCell(x, y, type, spawnFromAbove: false);
                }
        }

        void SpawnViewAtCell(int x, int y, int type, bool spawnFromAbove)
        {
            Vector2 pos = boardView.CellToWorld(x, y);
            if (spawnFromAbove)
                pos = boardView.CellToWorld(x, height) + Vector2.up * boardView.cellSize;

            var go = Instantiate(gemPrefabs[type], pos, Quaternion.identity, gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(x, y);
            view.OnSwipe += OnGemSwipe;
            view.OnDoubleTap += OnGemDoubleTap;


            var id = go.GetComponent<GemIdView>();
            if (id != null)
            {
                id.ColorType = type;
                id.Special = SpecialKind.None;
            }

            views[x, y] = view;
        }

        void OnGemSwipe(GemView view, Vector2Int dir)
        {
            if (busy) return;

            int ax = view.X;
            int ay = view.Y;
            int bx = ax + dir.x;
            int by = ay + dir.y;

            if (!model.InBounds(bx, by)) return;

            StartCoroutine(SwapAndResolve(ax, ay, bx, by));
        }

        private IEnumerator DetonateSpecialsAfterSwap(int ax, int ay, int bx, int by)
        {
            var start = new HashSet<Vector2Int>();

            var pa = model.Get(ax, ay);
            if (pa.HasValue && pa.Value.special != SpecialType.None)
                start.Add(new Vector2Int(ax, ay));

            var pb = model.Get(bx, by);
            if (pb.HasValue && pb.Value.special != SpecialType.None)
                start.Add(new Vector2Int(bx, by));

            // расширяем область взрыва
            ExpandSpecials(start);

            // уничтожаем всё из start (включая сами бомбы)
            foreach (var p in start)
            {
                model.Set(p.x, p.y, null);

                var v = views[p.x, p.y];
                views[p.x, p.y] = null;

                if (v != null)
                    StartCoroutine(DestroyViewRoutine(v));
            }

            yield return new WaitForSeconds(0.18f);

            // гравитация + спавн
            GravitySolver.Apply(model, out var moves, out var spawnCells);

            var moving = new List<GemView>();
            var targetPos = new Dictionary<GemView, Vector2>();

            foreach (var m in moves)
            {
                var v = views[m.from.x, m.from.y];
                views[m.from.x, m.from.y] = null;
                views[m.to.x, m.to.y] = v;

                if (v != null)
                {
                    v.SetCoords(m.to.x, m.to.y);
                    moving.Add(v);
                    targetPos[v] = boardView.CellToWorld(m.to.x, m.to.y);
                }
            }

            foreach (var c in spawnCells)
            {
                int type = RefillSolver.GetSafeType(model, c.x, c.y, gemPrefabs.Length);
                model.Set(c.x, c.y, new Piece(type));
                SpawnViewAtCell(c.x, c.y, type, spawnFromAbove: true);

                var v = views[c.x, c.y];
                if (v != null)
                {
                    moving.Add(v);
                    targetPos[v] = boardView.CellToWorld(c.x, c.y);
                }
            }

            if (boardView != null && moving.Count > 0)
                yield return boardView.AnimateMoves(moving, targetPos);
        }

        private void OnGemDoubleTap(GemView v)
        {
            if (busy) return;

            int x = v.X;
            int y = v.Y;

            var p = model.Get(x, y);
            if (!p.HasValue) return;

            // ✅ взрываем только спец-гемы/бомбы
            if (p.Value.special == SpecialType.None) return;

            StartCoroutine(DetonateAtCell(x, y));
        }
        private IEnumerator DetonateAtCell(int x, int y)
        {
            busy = true;

            var toDestroy = new HashSet<Vector2Int> { new Vector2Int(x, y) };

            // расширяем область по special (цепочки тоже сработают)
            ExpandSpecials(toDestroy);

            int destroyed = 0;

            foreach (var c in toDestroy)
            {
                model.Set(c.x, c.y, null);

                var view = views[c.x, c.y];
                views[c.x, c.y] = null;

                if (view != null)
                    StartCoroutine(DestroyViewRoutine(view));

                destroyed++;
            }

            score += destroyed * pointsPerGem;
            OnScoreChanged?.Invoke(score);

            yield return new WaitForSeconds(0.18f);

            // потом стандартная гравитация/спавн/анимация через ваш ResolveLoop
            GravitySolver.Apply(model, out var moves, out var spawnCells);

            var moving = new List<GemView>();
            var targetPos = new Dictionary<GemView, Vector2>();

            foreach (var m in moves)
            {
                var v = views[m.from.x, m.from.y];
                views[m.from.x, m.from.y] = null;
                views[m.to.x, m.to.y] = v;

                if (v != null)
                {
                    v.SetCoords(m.to.x, m.to.y);
                    moving.Add(v);
                    targetPos[v] = boardView.CellToWorld(m.to.x, m.to.y);
                }
            }

            foreach (var c in spawnCells)
            {
                int type = RefillSolver.GetSafeType(model, c.x, c.y, gemPrefabs.Length);
                model.Set(c.x, c.y, new Piece(type));

                SpawnViewAtCell(c.x, c.y, type, spawnFromAbove: true);

                var v = views[c.x, c.y];
                if (v != null)
                {
                    moving.Add(v);
                    targetPos[v] = boardView.CellToWorld(c.x, c.y);
                }
            }

            if (boardView != null && moving.Count > 0)
                yield return boardView.AnimateMoves(moving, targetPos);

            // дочищаем возможные каскады от падения
            yield return ResolveLoop();

            busy = false;
        }



        IEnumerator SwapAndResolve(int ax, int ay, int bx, int by)
        {
            busy = true;

            var aView = views[ax, ay];
            var bView = views[bx, by];

            // ход списываем сразу (как у вас было)
            movesLeft--;
            OnMovesChanged?.Invoke(movesLeft);
            if (movesLeft <= 0)
            {
                OnGameOver?.Invoke();
                busy = true;
                yield break;
            }

            // анимация свапа
            if (boardView != null && aView != null && bView != null)
                yield return boardView.AnimateSwap(aView, bView);

            // swap model
            model.Swap(ax, ay, bx, by);

            // swap views
            (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
            views[ax, ay].SetCoords(ax, ay);
            views[bx, by].SetCoords(bx, by);

            // ✅ если после свапа на одной из позиций стоит спец — активируем сразу
            var pa = model.Get(ax, ay);
            var pb = model.Get(bx, by);

            bool aSpecial = pa.HasValue && pa.Value.special != SpecialType.None;
            bool bSpecial = pb.HasValue && pb.Value.special != SpecialType.None;

            if (aSpecial || bSpecial)
            {
                yield return DetonateSpecialsAfterSwap(ax, ay, bx, by);
                yield return ResolveLoop();
                busy = false;
                yield break;
            }


            // validate
            var matches = MatchFinder.FindMatches(model);
            if (matches.Count == 0)
            {
                // revert anim
                if (boardView != null && aView != null && bView != null)
                    yield return boardView.AnimateSwap(bView, aView);

                // revert model
                model.Swap(ax, ay, bx, by);

                // revert views
                (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
                views[ax, ay].SetCoords(ax, ay);
                views[bx, by].SetCoords(bx, by);

                busy = false;
                yield break;
            }

            // pivot для бомбы
            lastSwapA = new Vector2Int(ax, ay);
            lastSwapB = new Vector2Int(bx, by);

            yield return ResolveLoop();

            busy = false;
        }

        private GameObject GetBombPrefab(int len)
        {
            if (len >= 7) return bomb7Prefab;
            if (len == 6) return bomb6Prefab;
            if (len == 5) return bomb5Prefab;
            if (len == 4) return bomb4Prefab;
            return null;
        }

        private void ReplaceCellWithBomb(int x, int y, int colorType, int len)
        {
            var prefab = GetBombPrefab(len);
            if (prefab == null) return;

            // удалить старый view
            var old = views[x, y];
            if (old != null) Destroy(old.gameObject);

            // создать новый
            var pos = boardView.CellToWorld(x, y);
            var go = Instantiate(prefab, pos, Quaternion.identity, gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(x, y);
            view.OnSwipe += OnGemSwipe;
            view.OnDoubleTap += OnGemDoubleTap;

            views[x, y] = view;

            // пометить как бомбу (чисто для будущего)
            var id = go.GetComponent<GemIdView>();
            if (id != null)
            {
                id.ColorType = colorType;
                id.Special = len switch
                {
                    4 => SpecialKind.Bomb4,
                    5 => SpecialKind.Bomb5,
                    6 => SpecialKind.Bomb6,
                    _ => SpecialKind.Bomb7
                };
            }

            // в модели клетка должна остаться занята
            var st = len switch
            {
                4 => SpecialType.Bomb4,
                5 => SpecialType.Bomb5,
                6 => SpecialType.Bomb6,
                _ => SpecialType.Bomb7
            };

            model.Set(x, y, new Piece(colorType, st));

        }

        // Группируем совпадения в “островки” по 4-соседству
        private List<List<Vector2Int>> BuildMatchGroups(List<Match3.Core.Cell> cells)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var c in cells) set.Add(new Vector2Int(c.x, c.y));

            var groups = new List<List<Vector2Int>>();
            var used = new HashSet<Vector2Int>();

            foreach (var start in set)
            {
                if (used.Contains(start)) continue;

                var group = new List<Vector2Int>();
                var q = new Queue<Vector2Int>();
                q.Enqueue(start);
                used.Add(start);

                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    group.Add(p);

                    var n = new[]
                    {
                        p + Vector2Int.right, p + Vector2Int.left,
                        p + Vector2Int.up,    p + Vector2Int.down
                    };

                    foreach (var nb in n)
                        if (set.Contains(nb) && !used.Contains(nb))
                        { used.Add(nb); q.Enqueue(nb); }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static readonly Vector2Int[] Neigh4 =
{
    Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
};

        private void AddIfInBounds(HashSet<Vector2Int> set, int x, int y)
        {
            if (model.InBounds(x, y))
                set.Add(new Vector2Int(x, y));
        }

        private void ExpandSpecials(HashSet<Vector2Int> toDestroy)
        {
            // спец-цепочки: бомба взрывает другую бомбу
            var q = new Queue<Vector2Int>(toDestroy);

            while (q.Count > 0)
            {
                var c = q.Dequeue();
                var p = model.Get(c.x, c.y);
                if (!p.HasValue) continue;

                var sp = p.Value.special;
                if (sp == SpecialType.None) continue;

                var extra = new HashSet<Vector2Int>();

                switch (sp)
                {
                    case SpecialType.Bomb4:
                        {
                            // крест: центр + 4 соседа
                            AddIfInBounds(extra, c.x, c.y);
                            AddIfInBounds(extra, c.x + 1, c.y);
                            AddIfInBounds(extra, c.x - 1, c.y);
                            AddIfInBounds(extra, c.x, c.y + 1);
                            AddIfInBounds(extra, c.x, c.y - 1);
                            break;
                        }


                    case SpecialType.Bomb5:
                        {
                            // 5x5 без четырёх углов
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                for (int dy = -2; dy <= 2; dy++)
                                {
                                    // пропускаем углы
                                    if (Mathf.Abs(dx) == 2 && Mathf.Abs(dy) == 2)
                                        continue;

                                    AddIfInBounds(extra, c.x + dx, c.y + dy);
                                }
                            }
                            break;
                        }


                    case SpecialType.Bomb6:
                        {
                            // 7x7 “октагон”: 3/5/7/7/7/5/3 (как на рисунке)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                for (int dy = -3; dy <= 3; dy++)
                                {
                                    int ax = Mathf.Abs(dx);
                                    int ay = Mathf.Abs(dy);

                                    // условие октагона:
                                    // 1) не выходим за 3 по каждой оси
                                    // 2) срезаем углы: |dx| + |dy| <= 4
                                    if (ax <= 3 && ay <= 3 && (ax + ay) <= 4)
                                        AddIfInBounds(extra, c.x + dx, c.y + dy);
                                }
                            }
                            break;
                        }


                    case SpecialType.Bomb7:
                        {
                            // 9x9 “октагон”: 5/7/9/9/9/9/9/7/5 (как на рисунке)
                            for (int dx = -4; dx <= 4; dx++)
                            {
                                for (int dy = -4; dy <= 4; dy++)
                                {
                                    int ax = Mathf.Abs(dx);
                                    int ay = Mathf.Abs(dy);

                                    // срез углов
                                    if (ax <= 4 && ay <= 4 && (ax + ay) <= 6)
                                        AddIfInBounds(extra, c.x + dx, c.y + dy);
                                }
                            }
                            break;
                        }

                }

                foreach (var e in extra)
                    if (toDestroy.Add(e))
                        q.Enqueue(e);
            }
        }



        IEnumerator ResolveLoop()
        {
            while (true)
            {
                var matches = MatchFinder.FindMatches(model);
                if (matches.Count == 0) yield break;

                var groups = BuildMatchGroups(matches);

                int destroyed = 0;

                foreach (var group in groups)
                {
                    // цвет читаем ДО обнуления
                    var first = group[0];
                    var piece = model.Get(first.x, first.y);
                    int colorType = piece.HasValue ? piece.Value.type : 0;

                    // pivot: приоритет клетки последнего свайпа
                    Vector2Int pivot = group[0];
                    if (group.Contains(lastSwapA)) pivot = lastSwapA;
                    else if (group.Contains(lastSwapB)) pivot = lastSwapB;

                    bool makeBomb = group.Count >= 4;

                    // 1) Если создаём бомбу — ставим её в pivot, но НЕ уничтожаем pivot
                    if (makeBomb)
                        ReplaceCellWithBomb(pivot.x, pivot.y, colorType, group.Count);

                    // 2) Собираем set клеток на уничтожение из матча
                    var toDestroy = new HashSet<Vector2Int>(group);
                    if (makeBomb) toDestroy.Remove(pivot);

                    // 3) Расширяем уничтожение спецами (цепочки)
                    ExpandSpecials(toDestroy);

                    // 4) Уничтожаем ВСЁ из toDestroy
                    foreach (var p in toDestroy)
                    {
                        // могли попасть в pivot через спец-расширение — если хотите “бомба не самоуничтожается”, оставьте так:
                        if (makeBomb && p == pivot) continue;

                        model.Set(p.x, p.y, null);

                        var v = views[p.x, p.y];
                        views[p.x, p.y] = null;

                        if (v != null)
                            StartCoroutine(DestroyViewRoutine(v));

                        destroyed++;
                    }
                }

                score += destroyed * pointsPerGem;
                OnScoreChanged?.Invoke(score);

                yield return new WaitForSeconds(0.18f);

                GravitySolver.Apply(model, out var moves, out var spawnCells);

                var moving = new List<GemView>();
                var targetPos = new Dictionary<GemView, Vector2>();

                foreach (var m in moves)
                {
                    var v = views[m.from.x, m.from.y];
                    views[m.from.x, m.from.y] = null;
                    views[m.to.x, m.to.y] = v;

                    if (v != null)
                    {
                        v.SetCoords(m.to.x, m.to.y);
                        moving.Add(v);
                        targetPos[v] = boardView.CellToWorld(m.to.x, m.to.y);
                    }
                }

                foreach (var c in spawnCells)
                {
                    int type = RefillSolver.GetSafeType(model, c.x, c.y, gemPrefabs.Length);
                    model.Set(c.x, c.y, new Piece(type));

                    SpawnViewAtCell(c.x, c.y, type, spawnFromAbove: true);

                    var v = views[c.x, c.y];
                    if (v != null)
                    {
                        moving.Add(v);
                        targetPos[v] = boardView.CellToWorld(c.x, c.y);
                    }
                }

                if (boardView != null && moving.Count > 0)
                    yield return boardView.AnimateMoves(moving, targetPos);
            }
        }


        IEnumerator DestroyViewRoutine(GemView v)
        {
            if (v == null) yield break;

            yield return v.PlayDestroy();

            if (v != null)
                Destroy(v.gameObject);
        }
    }
}
