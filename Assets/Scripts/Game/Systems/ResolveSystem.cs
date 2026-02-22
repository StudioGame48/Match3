using Match3.Core;
using Match3.Game.Services;
using Match3.Game.Utils;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game.Systems
{
    public sealed class ResolveSystem
    {
        private readonly BoardModel _model;
        private readonly GemView[,] _views;

        private readonly int _width;
        private readonly int _height;

        private readonly BoardView _boardView;
        private readonly Transform _gridParent;

        private readonly GameObject[] _gemPrefabs;

        private readonly GameObject _bomb4Prefab;
        private readonly GameObject _bomb5Prefab;
        private readonly GameObject _bomb6Prefab;
        private readonly GameObject _bomb7Prefab;
        private readonly GameObject _cartPrefab;

        private readonly int _pointsPerGem;

        private readonly SwapService _swapService;
        private readonly ViewFactory _viewFactory;
        private readonly SpecialSystem _specials;
        private readonly ScoreMovesSystem _scoreMoves;
        private readonly System.Action<Piece> _onPieceCleared;

        private readonly System.Action<float> _onCartMeterChanged;
        private readonly int _cartChargeMax;
        private int _cartCharge;

        // запоминаем последний свап (для pivot бомбы)
        private Vector2Int _lastSwapA;
        private Vector2Int _lastSwapB;

        private readonly MonoBehaviour _runner;

        public ResolveSystem(
            MonoBehaviour runner,
            BoardModel model, GemView[,] views, int width, int height,
            BoardView boardView, Transform gridParent,
            GameObject[] gemPrefabs,
            GameObject bomb4Prefab, GameObject bomb5Prefab, GameObject bomb6Prefab, GameObject bomb7Prefab,
            GameObject cartPrefab,
            int pointsPerGem,
            SwapService swapService,
            ViewFactory viewFactory,
            SpecialSystem specials,
            ScoreMovesSystem scoreMoves,
            System.Action<Piece> onPieceCleared,
            int cartChargeMax,
            int cartChargeStart,
            System.Action<float> onCartMeterChanged)
        {
            _runner = runner;

            _model = model;
            _views = views;
            _width = width;
            _height = height;
            _onPieceCleared = onPieceCleared;

            _boardView = boardView;
            _gridParent = gridParent;

            _gemPrefabs = gemPrefabs;

            _bomb4Prefab = bomb4Prefab;
            _bomb5Prefab = bomb5Prefab;
            _bomb6Prefab = bomb6Prefab;
            _bomb7Prefab = bomb7Prefab;
            _cartPrefab = cartPrefab;

            _pointsPerGem = pointsPerGem;

            _swapService = swapService;
            _viewFactory = viewFactory;
            _specials = specials;
            _scoreMoves = scoreMoves;

            _cartChargeMax = cartChargeMax;
            _cartCharge = cartChargeStart;
            _onCartMeterChanged = onCartMeterChanged;
        }

        public void PushCartMeter() =>
            _onCartMeterChanged?.Invoke((float)_cartCharge / _cartChargeMax);

        private void AddCartCharge(int amount)
        {
            if (amount <= 0) return;

            _cartCharge += amount;

            while (_cartCharge >= _cartChargeMax)
            {
                _cartCharge -= _cartChargeMax;
                SpawnCartRandom();
            }

            PushCartMeter();
        }

        private void SpawnCartRandom()
        {
            var candidates = new List<Vector2Int>();

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    var p = _model.Get(x, y);
                    if (!p.HasValue) continue;
                    if (p.Value.special != SpecialType.None) continue;
                    candidates.Add(new Vector2Int(x, y));
                }

            if (candidates.Count == 0) return;

            var c = candidates[Random.Range(0, candidates.Count)];

            // модель: сохраняем type, добавляем special Cart
            var old = _model.Get(c.x, c.y).Value;
            _model.Set(c.x, c.y, new Piece(old.type, SpecialType.Cart));

            // view: заменить объект на cart prefab (с подписками)
            var oldView = _views[c.x, c.y];
            var view = _viewFactory.ReplaceCellWithPrefab(oldView, _cartPrefab, c.x, c.y);
            _views[c.x, c.y] = view;
        }

        /// <summary>
        /// Главный вход: swap + resolve.
        /// Новое правило UX: ход списываем ТОЛЬКО если действие успешно.
        /// </summary>
        public IEnumerator SwapAndResolve(
            int ax, int ay, int bx, int by,
            System.Func<GemView, IEnumerator> destroyRoutine,
            System.Action<GemView> rebindCartViewHandlers)
        {
            // 1) анимация + применение свапа
            yield return _swapService.AnimateAndApplySwap(_model, _views, ax, ay, bx, by);

            // 2) CART SWAP (успешное действие -> списываем ход)
            if (_specials.IsCartAt(ax, ay) || _specials.IsCartAt(bx, by))
            {
                int cartX, cartY, otherX, otherY;

                if (_specials.IsCartAt(ax, ay)) { cartX = ax; cartY = ay; otherX = bx; otherY = by; }
                else { cartX = bx; cartY = by; otherX = ax; otherY = ay; }

                var otherPiece = _model.Get(otherX, otherY);
                // активируем только если рядом обычная фишка
                if (otherPiece.HasValue && otherPiece.Value.special == SpecialType.None)
                {
                    // ✅ действие считается успешным
                    bool gameOverAfter = _scoreMoves.ConsumeMove();

                    int targetType = otherPiece.Value.type;

                    // гарантируем хендлеры (на всякий случай)
                    rebindCartViewHandlers?.Invoke(_views[cartX, cartY]);

                    yield return ActivateCartAt(cartX, cartY, targetType, destroyRoutine);

                    if (gameOverAfter) _scoreMoves.TriggerGameOver();
                    yield break;
                }

                // если свап с тележкой, но невалидный для активации (например рядом спец)
                // откатываем и ход не тратим
                yield return _swapService.AnimateAndRollbackSwap(_model, _views, ax, ay, bx, by);
                yield break;
            }

            // 3) SPECIAL SWAP (если после свапа на одной из позиций спец — детонируем; ход тратим)
            var pa = _model.Get(ax, ay);
            var pb = _model.Get(bx, by);

            bool aSpecial = pa.HasValue && pa.Value.special != SpecialType.None;
            bool bSpecial = pb.HasValue && pb.Value.special != SpecialType.None;

            if (aSpecial || bSpecial)
            {
                // ✅ действие считается успешным
                bool gameOverAfter = _scoreMoves.ConsumeMove();

                yield return DetonateSpecialsAfterSwap(ax, ay, bx, by, destroyRoutine);
                yield return ResolveLoop(destroyRoutine);

                if (gameOverAfter) _scoreMoves.TriggerGameOver();
                yield break;
            }

            // 4) validate обычный матч
            var matches = MatchFinder.FindMatches(_model);

            if (matches.Count == 0)
            {
                // ❌ неуспешный свайп -> откат, ход НЕ тратим
                yield return _swapService.AnimateAndRollbackSwap(_model, _views, ax, ay, bx, by);
                yield break;
            }

            // ✅ матч есть -> списываем ход
            bool gameOverAfterMatch = _scoreMoves.ConsumeMove();

            _lastSwapA = new Vector2Int(ax, ay);
            _lastSwapB = new Vector2Int(bx, by);

            yield return ResolveLoop(destroyRoutine);

            if (gameOverAfterMatch) _scoreMoves.TriggerGameOver();
        }

        private GameObject GetBombPrefab(int len)
        {
            if (len >= 7) return _bomb7Prefab;
            if (len == 6) return _bomb6Prefab;
            if (len == 5) return _bomb5Prefab;
            if (len == 4) return _bomb4Prefab;
            return null;
        }

        private void ReplaceCellWithBomb(int x, int y, int colorType, int len)
        {
            var prefab = GetBombPrefab(len);
            if (prefab == null) return;

            var old = _views[x, y];
            var view = _viewFactory.ReplaceCellWithPrefab(old, prefab, x, y);
            _views[x, y] = view;

            var id = view.GetComponent<GemIdView>();
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

            var st = len switch
            {
                4 => SpecialType.Bomb4,
                5 => SpecialType.Bomb5,
                6 => SpecialType.Bomb6,
                _ => SpecialType.Bomb7
            };

            _model.Set(x, y, new Piece(colorType, st));
        }

        public IEnumerator ResolveLoop(System.Func<GemView, IEnumerator> destroyRoutine)
        {
            while (true)
            {
                var matches = MatchFinder.FindMatches(_model);
                if (matches.Count == 0) yield break;

                var groups = MatchGrouping.BuildMatchGroups(_model, matches);

                int destroyed = 0;

                foreach (var group in groups)
                {
                    var first = group[0];
                    var piece = _model.Get(first.x, first.y);
                    int colorType = piece.HasValue ? piece.Value.type : 0;

                    Vector2Int pivot = group[0];
                    if (group.Contains(_lastSwapA)) pivot = _lastSwapA;
                    else if (group.Contains(_lastSwapB)) pivot = _lastSwapB;

                    bool makeBomb = group.Count >= 4;

                    if (makeBomb)
                        ReplaceCellWithBomb(pivot.x, pivot.y, colorType, group.Count);

                    var toDestroy = new HashSet<Vector2Int>(group);
                    if (makeBomb) toDestroy.Remove(pivot);

                    _specials.ExpandSpecials(toDestroy);

                    foreach (var c in toDestroy)
                    {
                        var cellPiece = _model.Get(c.x, c.y);
                        if (cellPiece.HasValue && cellPiece.Value.special == SpecialType.Cart)
                            continue; // тележка не умирает от бомб

                        if (cellPiece.HasValue) _onPieceCleared?.Invoke(cellPiece.Value);

                        _model.Set(c.x, c.y, null);

                        var view = _views[c.x, c.y];
                        _views[c.x, c.y] = null;

                        if (view != null)
                            _runner.StartCoroutine(destroyRoutine(view));

                        destroyed++;
                    }
                }

                _scoreMoves.AddDestroyed(destroyed, _pointsPerGem);

                yield return new WaitForSeconds(0.18f);

                GravitySolver.Apply(_model, out var moves, out var spawnCells);

                var moving = new List<GemView>();
                var targetPos = new Dictionary<GemView, Vector2>();

                foreach (var m in moves)
                {
                    var v = _views[m.from.x, m.from.y];
                    _views[m.from.x, m.from.y] = null;
                    _views[m.to.x, m.to.y] = v;

                    if (v != null)
                    {
                        v.SetCoords(m.to.x, m.to.y);
                        moving.Add(v);
                        targetPos[v] = _boardView.CellToWorld(m.to.x, m.to.y);
                    }
                }

                foreach (var c in spawnCells)
                {
                    int type = RefillSolver.GetSafeType(_model, c.x, c.y, _gemPrefabs.Length);
                    _model.Set(c.x, c.y, new Piece(type));

                    var view = _viewFactory.CreateGem(_gemPrefabs[type], c.x, c.y, _height,
                        spawnFromAbove: true, cellSize: _boardView.cellSize);
                    _views[c.x, c.y] = view;

                    if (view != null)
                    {
                        moving.Add(view);
                        targetPos[view] = _boardView.CellToWorld(c.x, c.y);
                    }
                }

                if (_boardView != null && moving.Count > 0)
                    yield return _boardView.AnimateMoves(moving, targetPos);
            }
        }

        private IEnumerator DetonateSpecialsAfterSwap(int ax, int ay, int bx, int by,
            System.Func<GemView, IEnumerator> destroyRoutine)
        {
            var start = new HashSet<Vector2Int>();

            var pa = _model.Get(ax, ay);
            if (pa.HasValue && pa.Value.special != SpecialType.None)
                start.Add(new Vector2Int(ax, ay));

            var pb = _model.Get(bx, by);
            if (pb.HasValue && pb.Value.special != SpecialType.None)
                start.Add(new Vector2Int(bx, by));

            _specials.ExpandSpecials(start);

            int add = 0;
            foreach (var cell in start)
            {
                var piece = _model.Get(cell.x, cell.y);
                add += _specials.ComputeChargeForPiece(piece);
            }
            AddCartCharge(add);

            foreach (var cell in start)
            {
                var cellPiece = _model.Get(cell.x, cell.y);
                if (cellPiece.HasValue && cellPiece.Value.special == SpecialType.Cart)
                    continue;
                if (cellPiece.HasValue) _onPieceCleared?.Invoke(cellPiece.Value);

                _model.Set(cell.x, cell.y, null);

                var v = _views[cell.x, cell.y];
                _views[cell.x, cell.y] = null;

                if (v != null)
                    _runner.StartCoroutine(destroyRoutine(v));
            }

            yield return new WaitForSeconds(0.18f);

            GravitySolver.Apply(_model, out var moves, out var spawnCells);

            var moving = new List<GemView>();
            var targetPos = new Dictionary<GemView, Vector2>();

            foreach (var m in moves)
            {
                var v = _views[m.from.x, m.from.y];
                _views[m.from.x, m.from.y] = null;
                _views[m.to.x, m.to.y] = v;

                if (v != null)
                {
                    v.SetCoords(m.to.x, m.to.y);
                    moving.Add(v);
                    targetPos[v] = _boardView.CellToWorld(m.to.x, m.to.y);
                }
            }

            foreach (var c in spawnCells)
            {
                int type = RefillSolver.GetSafeType(_model, c.x, c.y, _gemPrefabs.Length);
                _model.Set(c.x, c.y, new Piece(type));

                var view = _viewFactory.CreateGem(_gemPrefabs[type], c.x, c.y, _height,
                    spawnFromAbove: true, cellSize: _boardView.cellSize);
                _views[c.x, c.y] = view;

                if (view != null)
                {
                    moving.Add(view);
                    targetPos[view] = _boardView.CellToWorld(c.x, c.y);
                }
            }

            if (_boardView != null && moving.Count > 0)
                yield return _boardView.AnimateMoves(moving, targetPos);
        }

        public IEnumerator DetonateAtCell(int x, int y, System.Func<GemView, IEnumerator> destroyRoutine)
        {
            var toDestroy = new HashSet<Vector2Int> { new Vector2Int(x, y) };
            _specials.ExpandSpecials(toDestroy);

            var piece = _model.Get(x, y);
            AddCartCharge(_specials.ComputeChargeForPiece(piece));

            int destroyed = 0;

            foreach (var c in toDestroy)
            {

                var cellPiece = _model.Get(c.x, c.y);
                if (piece.HasValue) _onPieceCleared?.Invoke(piece.Value);

                _model.Set(c.x, c.y, null);

                var view = _views[c.x, c.y];
                _views[c.x, c.y] = null;

                if (view != null)
                    _runner.StartCoroutine(destroyRoutine(view));

                destroyed++;
            }

            _scoreMoves.AddDestroyed(destroyed, _pointsPerGem);

            yield return new WaitForSeconds(0.18f);

            GravitySolver.Apply(_model, out var moves, out var spawnCells);

            var moving = new List<GemView>();
            var targetPos = new Dictionary<GemView, Vector2>();

            foreach (var m in moves)
            {
                var v = _views[m.from.x, m.from.y];
                _views[m.from.x, m.from.y] = null;
                _views[m.to.x, m.to.y] = v;

                if (v != null)
                {
                    v.SetCoords(m.to.x, m.to.y);
                    moving.Add(v);
                    targetPos[v] = _boardView.CellToWorld(m.to.x, m.to.y);
                }
            }

            foreach (var c in spawnCells)
            {
                int type = RefillSolver.GetSafeType(_model, c.x, c.y, _gemPrefabs.Length);
                _model.Set(c.x, c.y, new Piece(type));

                var view = _viewFactory.CreateGem(_gemPrefabs[type], c.x, c.y, _height,
                    spawnFromAbove: true, cellSize: _boardView.cellSize);
                _views[c.x, c.y] = view;

                if (view != null)
                {
                    moving.Add(view);
                    targetPos[view] = _boardView.CellToWorld(c.x, c.y);
                }
            }

            if (_boardView != null && moving.Count > 0)
                yield return _boardView.AnimateMoves(moving, targetPos);

            yield return ResolveLoop(destroyRoutine);
        }

        public IEnumerator ActivateCartRandomAt(int cartX, int cartY, System.Func<GemView, IEnumerator> destroyRoutine)
        {
            var present = new List<int>();
            var seen = new HashSet<int>();

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    var p = _model.Get(x, y);
                    if (!p.HasValue) continue;
                    if (p.Value.special != SpecialType.None) continue;

                    int t = p.Value.type;
                    if (seen.Add(t)) present.Add(t);
                }

            if (present.Count == 0) yield break;

            int targetType = present[Random.Range(0, present.Count)];
            yield return ActivateCartAt(cartX, cartY, targetType, destroyRoutine);
        }

        private IEnumerator ActivateCartAt(int cartX, int cartY, int targetType, System.Func<GemView, IEnumerator> destroyRoutine)
        {
            var toDestroy = new HashSet<Vector2Int>();

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    var p = _model.Get(x, y);
                    if (p.HasValue && p.Value.type == targetType)
                        toDestroy.Add(new Vector2Int(x, y));
                }

            toDestroy.Add(new Vector2Int(cartX, cartY));

            int destroyed = 0;

            foreach (var c in toDestroy)
            {

                var piece = _model.Get(c.x, c.y);
                if (piece.HasValue) _onPieceCleared?.Invoke(piece.Value);

                _model.Set(c.x, c.y, null);

                var view = _views[c.x, c.y];
                _views[c.x, c.y] = null;

                if (view != null)
                    _runner.StartCoroutine(destroyRoutine(view));

                destroyed++;
            }

            _scoreMoves.AddDestroyed(destroyed, _pointsPerGem);

            yield return new WaitForSeconds(0.18f);

            GravitySolver.Apply(_model, out var moves, out var spawnCells);

            var moving = new List<GemView>();
            var targetPos = new Dictionary<GemView, Vector2>();

            foreach (var m in moves)
            {
                var v = _views[m.from.x, m.from.y];
                _views[m.from.x, m.from.y] = null;
                _views[m.to.x, m.to.y] = v;

                if (v != null)
                {
                    v.SetCoords(m.to.x, m.to.y);
                    moving.Add(v);
                    targetPos[v] = _boardView.CellToWorld(m.to.x, m.to.y);
                }
            }

            foreach (var c in spawnCells)
            {
                int type = RefillSolver.GetSafeType(_model, c.x, c.y, _gemPrefabs.Length);
                _model.Set(c.x, c.y, new Piece(type));

                var view = _viewFactory.CreateGem(_gemPrefabs[type], c.x, c.y, _height,
                    spawnFromAbove: true, cellSize: _boardView.cellSize);
                _views[c.x, c.y] = view;

                if (view != null)
                {
                    moving.Add(view);
                    targetPos[view] = _boardView.CellToWorld(c.x, c.y);
                }
            }

            if (_boardView != null && moving.Count > 0)
                yield return _boardView.AnimateMoves(moving, targetPos);

            yield return ResolveLoop(destroyRoutine);
        }
    }
}