using Match3.Core;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;


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




        void Start()
        {
            model = new BoardModel(width, height);
            views = new GemView[width, height];
            movesLeft = maxMoves;
            OnMovesChanged?.Invoke(movesLeft);
            score = 0;
            movesLeft = maxMoves;
            PushUIState();


            if (boardView == null)
                boardView = FindFirstObjectByType<BoardView>();

            GenerateNoMatches();
            StartCoroutine(ResolveLoop()); // чистим случайные стартовые совпадения
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

        public void PushUIState()
        {
            OnScoreChanged?.Invoke(score);
            OnMovesChanged?.Invoke(movesLeft);
        }

        void SpawnViewAtCell(int x, int y, int type, bool spawnFromAbove)
        {
            Vector2 pos = boardView.CellToWorld(x, y);
            if (spawnFromAbove)
                pos = boardView.CellToWorld(x, height) + Vector2.up * boardView.cellSize; // чуть выше

            var go = Instantiate(gemPrefabs[type], pos, Quaternion.identity, gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(x, y);
            view.OnSwipe += OnGemSwipe;

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

        IEnumerator SwapAndResolve(int ax, int ay, int bx, int by)
        {
            busy = true;

            var aView = views[ax, ay];
            var bView = views[bx, by];

            // ... после проверки matches.Count == 0 (когда откатили) просто выходим
            movesLeft--;
            OnMovesChanged?.Invoke(movesLeft);
            if (movesLeft <= 0) { OnGameOver?.Invoke(); busy = true; yield break; }

            // анимация свапа
            if (boardView != null && aView != null && bView != null)
                yield return boardView.AnimateSwap(aView, bView);

            // swap model
            model.Swap(ax, ay, bx, by);

            // swap views
            (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
            views[ax, ay].SetCoords(ax, ay);
            views[bx, by].SetCoords(bx, by);

            // validate
            var matches = MatchFinder.FindMatches(model);
            if (matches.Count == 0)
            {
                // revert
                if (boardView != null && aView != null && bView != null)
                    yield return boardView.AnimateSwap(bView, aView);

                model.Swap(ax, ay, bx, by);
                (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
                views[ax, ay].SetCoords(ax, ay);
                views[bx, by].SetCoords(bx, by);

                busy = false;
                yield break;
            }

            yield return ResolveLoop();

            busy = false;
        }

        IEnumerator ResolveLoop()
        {
            while (true)
            {
                var matches = MatchFinder.FindMatches(model);
                if (matches.Count == 0) yield break;

                // ✅ SCORE — ДО уничтожения
                int gained = matches.Count * pointsPerGem;

                score += gained;
                OnScoreChanged?.Invoke(score);

                // 1) destroy matched (model + view)
                var destroyed = new List<GemView>(matches.Count);

                foreach (var c in matches)
                {
                    model.Set(c.x, c.y, null);

                    var v = views[c.x, c.y];
                    views[c.x, c.y] = null;

                    if (v != null)
                        StartCoroutine(DestroyViewRoutine(v));
                }

                yield return new WaitForSeconds(0.18f);

                // 2) gravity in model
                GravitySolver.Apply(model, out var moves, out var spawnCells);

                // 3) apply moves to views array (без анимации пока)
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

                // 4) spawn новых сверху + добавить в moving для падения
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

                // 5) animate all moves
                if (boardView != null && moving.Count > 0)
                    yield return boardView.AnimateMoves(moving, targetPos);
            }
        }


        IEnumerator DestroyViewRoutine(GemView v)
        {
            // если кто-то уже удалил
            if (v == null) yield break;

            yield return v.PlayDestroy();

            // мог быть уничтожен во время анимации
            if (v != null)
                Destroy(v.gameObject);
        }

    }
}

