using Match3.Core;
using Match3.Game.Services;
using Match3.Game.State;
using Match3.Game.Systems;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
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

        public System.Action<int> OnMovesChanged;
        public System.Action<int> OnScoreChanged;
        public System.Action OnGameOver;

        [Header("Bomb Prefabs (match 4-7+)")]
        [SerializeField] private GameObject bomb4Prefab;
        [SerializeField] private GameObject bomb5Prefab;
        [SerializeField] private GameObject bomb6Prefab;
        [SerializeField] private GameObject bomb7Prefab;
        [SerializeField] private GameObject cartPrefab;

        [Header("Cart Meter")]
        [SerializeField] private int cartChargeMax = 50;
        [SerializeField] private int cartCharge = 0;
        public System.Action<float> OnCartMeterChanged;

        private bool busy;

        // systems
        private GameStateMachine fsm;
        private InputRouter input;
        private ScoreMovesSystem scoreMoves;
        private SpecialSystem specials;
        private ViewFactory viewFactory;
        private SwapService swapService;
        private ResolveSystem resolve;

        void Start()
        {
            model = new BoardModel(width, height);
            views = new GemView[width, height];

            if (boardView == null)
                boardView = FindFirstObjectByType<BoardView>();

            fsm = new GameStateMachine();

            // будет установлено после создания input (хендлеры нужны фабрике)
            input = new InputRouter(
                fsm, model,
                isBusy: () => busy,
                requestSwap: (ax, ay, bx, by) => StartCoroutine(SwapAndResolve(ax, ay, bx, by)),
                requestDetonate: (x, y) => StartCoroutine(DetonateAtCell(x, y)),
                requestCartRandom: (x, y) => StartCoroutine(ActivateCartRandomAt(x, y))
            );

            viewFactory = new ViewFactory(boardView, gridParent, input.OnGemSwipe, input.OnGemDoubleTap);
            swapService = new SwapService(boardView);

            scoreMoves = new ScoreMovesSystem(OnMovesChanged, OnScoreChanged, OnGameOver);
            scoreMoves.Init(maxMoves);

            specials = new SpecialSystem(model);

            resolve = new ResolveSystem(
                runner: this,
                model: model, views: views, width: width, height: height,
                boardView: boardView, gridParent: gridParent,
                gemPrefabs: gemPrefabs,
                bomb4Prefab: bomb4Prefab, bomb5Prefab: bomb5Prefab, bomb6Prefab: bomb6Prefab, bomb7Prefab: bomb7Prefab,
                cartPrefab: cartPrefab,
                pointsPerGem: pointsPerGem,
                swapService: swapService,
                viewFactory: viewFactory,
                specials: specials,
                scoreMoves: scoreMoves,
                cartChargeMax: cartChargeMax,
                cartChargeStart: cartCharge,
                onCartMeterChanged: OnCartMeterChanged
            );

            GenerateNoMatches();
            StartCoroutine(resolve.ResolveLoop(DestroyViewRoutine)); // чистим случайные стартовые совпадения
            resolve.PushCartMeter();
        }

        public void PushUIState()
        {
            scoreMoves?.PushUIState();
            resolve?.PushCartMeter();
        }

        private void GenerateNoMatches()
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int type = RefillSolver.GetSafeType(model, x, y, gemPrefabs.Length);
                    model.Set(x, y, new Piece(type));

                    var view = viewFactory.CreateGem(gemPrefabs[type], x, y, height, spawnFromAbove: false, cellSize: boardView.cellSize);
                    views[x, y] = view;
                }
        }

        private IEnumerator SwapAndResolve(int ax, int ay, int bx, int by)
        {
            busy = true;
            fsm.Set(GameState.Swapping);

            yield return resolve.SwapAndResolve(
                ax, ay, bx, by,
                destroyRoutine: DestroyViewRoutine,
                rebindCartViewHandlers: (v) =>
                {
                    if (v == null) return;
                    // На случай если cart создавался без ViewFactory: гарантируем подписки
                    v.OnSwipe -= input.OnGemSwipe;
                    v.OnSwipe += input.OnGemSwipe;

                    v.OnDoubleTap -= input.OnGemDoubleTap;
                    v.OnDoubleTap += input.OnGemDoubleTap;
                });

            fsm.Set(GameState.Input);
            busy = false;
        }

        private IEnumerator DetonateAtCell(int x, int y)
        {
            // если уже busy — не надо
            if (busy) yield break;

            busy = true;
            fsm.Set(GameState.Resolving);

            // ✅ тратим ход за double tap по бомбе
            bool gameOverAfter = scoreMoves.ConsumeMove();

            // выполняем действие (анимации/взрыв/каскады)
            yield return resolve.DetonateAtCell(x, y, DestroyViewRoutine);

            // ✅ если после этого ходов стало 0 — завершаем игру
            if (gameOverAfter)
            {
                fsm.Set(GameState.GameOver);
                scoreMoves.TriggerGameOver();
                busy = true;
                yield break;
            }

            fsm.Set(GameState.Input);
            busy = false;
        }

        private IEnumerator ActivateCartRandomAt(int x, int y)
        {
            if (busy) yield break;

            busy = true;
            fsm.Set(GameState.Resolving);

            // ✅ тратим ход за double tap по тележке
            bool gameOverAfter = scoreMoves.ConsumeMove();

            // выполняем действие (удаление цвета/падение/каскады)
            yield return resolve.ActivateCartRandomAt(x, y, DestroyViewRoutine);

            // ✅ если ходов стало 0 — завершаем игру
            if (gameOverAfter)
            {
                fsm.Set(GameState.GameOver);
                scoreMoves.TriggerGameOver();
                busy = true;
                yield break;
            }

            fsm.Set(GameState.Input);
            busy = false;
        }

        private IEnumerator DestroyViewRoutine(GemView v)
        {
            if (v == null) yield break;

            yield return v.PlayDestroy();

            if (v != null)
                Destroy(v.gameObject);
        }
    }
}