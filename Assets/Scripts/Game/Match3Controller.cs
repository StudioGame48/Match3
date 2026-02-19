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
        [SerializeField] private GameObject cartPrefab;
        [Header("Cart Meter")]
        [SerializeField] private int cartChargeMax = 100;
        [SerializeField] private int cartCharge = 0;

        public System.Action<float> OnCartMeterChanged; // 0..1



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
            OnCartMeterChanged?.Invoke((float)cartCharge / cartChargeMax);

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

            // ✅ Вариант A: суммируем заряд по всем спецам, которые детонируют
            int add = 0;
            foreach (var cell in start)
            {
                var piece = model.Get(cell.x, cell.y);
                if (!piece.HasValue) continue;

                add += piece.Value.special switch
                {
                    SpecialType.Bomb4 => 12,
                    SpecialType.Bomb5 => 20,
                    SpecialType.Bomb6 => 32,
                    SpecialType.Bomb7 => 45,
                    _ => 0
                };
            }
            AddCartCharge(add);


            // уничтожаем всё из start (включая сами бомбы)
            foreach (var cell in start)
            {
                model.Set(cell.x, cell.y, null);

                var v = views[cell.x, cell.y];
                views[cell.x, cell.y] = null;


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

        private void AddCartCharge(int amount)
        {
            if (amount <= 0) return;

            cartCharge += amount;

            // 🔥 если переполнилась — спавним тележку и сохраняем остаток
            while (cartCharge >= cartChargeMax)
            {
                cartCharge -= cartChargeMax;
                SpawnCartRandom();
            }

            OnCartMeterChanged?.Invoke((float)cartCharge / cartChargeMax);
        }


        private void OnGemDoubleTap(GemView v)
        {
            if (busy) return;

            int x = v.X;
            int y = v.Y;

            var piece = model.Get(x, y);
            if (!piece.HasValue) return;

            // ✅ если это тележка — удаляем случайный цвет
            if (piece.Value.special == SpecialType.Cart)
            {
                StartCoroutine(ActivateCartRandomAt(x, y));
                return;
            }

            // ✅ если это обычная фишка — ничего
            if (piece.Value.special == SpecialType.None)
                return;

            // ✅ иначе это бомба/спец — взрываем
            StartCoroutine(DetonateAtCell(x, y));
        }



        private void SpawnCartRandom()
        {
            var candidates = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var p = model.Get(x, y);
                    if (!p.HasValue) continue;
                    if (p.Value.special != SpecialType.None) continue; // не поверх спецов

                    candidates.Add(new Vector2Int(x, y));
                }

            if (candidates.Count == 0) return;

            var c = candidates[Random.Range(0, candidates.Count)];

            // модель: сохраняем type, добавляем special Cart
            var old = model.Get(c.x, c.y).Value;
            model.Set(c.x, c.y, new Piece(old.type, SpecialType.Cart));

            // view: заменить объект в клетке на cart prefab
            var oldView = views[c.x, c.y];
            if (oldView != null) Destroy(oldView.gameObject);

            var pos = boardView.CellToWorld(c.x, c.y);
            var go = Instantiate(cartPrefab, pos, Quaternion.identity, gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(c.x, c.y);
            view.OnSwipe += OnGemSwipe;
            view.OnDoubleTap += OnGemDoubleTap; // важно!
            views[c.x, c.y] = view;
        }

        private IEnumerator ActivateCartAt(int cartX, int cartY, int targetType)
        {
            busy = true;

            var toDestroy = new HashSet<Vector2Int>();

            // все фишки выбранного цвета
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var p = model.Get(x, y);
                    if (p.HasValue && p.Value.type == targetType)
                        toDestroy.Add(new Vector2Int(x, y));
                }

            // тележка тоже исчезает
            toDestroy.Add(new Vector2Int(cartX, cartY));

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

            // гравитация + спавн + анимация + каскады
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

            yield return ResolveLoop();
            busy = false;
        }

        private IEnumerator ActivateCartRandomAt(int cartX, int cartY)
        {
            // собрать список типов, которые реально есть на поле (обычные фишки)
            var present = new List<int>();
            var seen = new HashSet<int>();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var p = model.Get(x, y);
                    if (!p.HasValue) continue;
                    if (p.Value.special != SpecialType.None) continue; // не выбираем по спецам

                    int t = p.Value.type;
                    if (seen.Add(t)) present.Add(t);
                }

            if (present.Count == 0) yield break;

            int targetType = present[Random.Range(0, present.Count)];
            yield return ActivateCartAt(cartX, cartY, targetType);
        }


        private IEnumerator DetonateAtCell(int x, int y)
        {
            busy = true;

            var toDestroy = new HashSet<Vector2Int> { new Vector2Int(x, y) };

            // расширяем область по special (цепочки тоже сработают)
            ExpandSpecials(toDestroy);
            // ✅ начисляем заряд по типу бомбы (Вариант A)
            var piece = model.Get(x, y);
            if (piece.HasValue)
            {
                int add = piece.Value.special switch
                {
                    SpecialType.Bomb4 => 12,
                    SpecialType.Bomb5 => 20,
                    SpecialType.Bomb6 => 32,
                    SpecialType.Bomb7 => 45,
                    _ => 0
                };

                AddCartCharge(add);
            }

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

            // ✅ CART SWAP: если тележка участвует в свапе — активируем сразу
            if (IsCartAt(ax, ay) || IsCartAt(bx, by))
            {
                // определяем где тележка, а где обычная фишка
                int cartX, cartY, otherX, otherY;

                if (IsCartAt(ax, ay)) { cartX = ax; cartY = ay; otherX = bx; otherY = by; }
                else { cartX = bx; cartY = by; otherX = ax; otherY = ay; }

                var otherPiece = model.Get(otherX, otherY);
                // если вдруг рядом тоже спец — не активируем (можно расширить потом)
                if (otherPiece.HasValue && otherPiece.Value.special == SpecialType.None)
                {
                    int targetType = otherPiece.Value.type;
                    yield return ActivateCartAt(cartX, cartY, targetType);
                    busy = false;
                    yield break;
                }
            }

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

                        // ✅ ВАЖНО: только одинаковый тип
                        if (np.Value.type != startType) continue;

                        used.Add(n);
                        queue.Enqueue(n);
                    }
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

        private bool IsCartAt(int x, int y)
        {
            var p = model.Get(x, y);
            return p.HasValue && p.Value.special == SpecialType.Cart;
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
