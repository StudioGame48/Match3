using Match3.Core;
using Match3.Game.Services;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game.Systems
{
    public sealed class CartSystem
    {
        private readonly BoardModel _model;
        private readonly GemView[,] _views;
        private readonly int _width;
        private readonly int _height;

        private readonly BoardView _boardView;
        private readonly Transform _gridParent;

        private readonly GameObject _cartPrefab;
        private readonly GameObject[] _gemPrefabs;

        private readonly int _cartChargeMax;
        private int _cartCharge;

        private readonly ViewFactory _viewFactory;

        private readonly System.Action<float> _onCartMeterChanged;

        public int CartCharge => _cartCharge;

        public CartSystem(BoardModel model, GemView[,] views, int width, int height,
            BoardView boardView, Transform gridParent,
            GameObject cartPrefab, GameObject[] gemPrefabs,
            int cartChargeMax, int cartChargeStart,
            ViewFactory viewFactory,
            System.Action<float> onCartMeterChanged)
        {
            _model = model;
            _views = views;
            _width = width;
            _height = height;

            _boardView = boardView;
            _gridParent = gridParent;

            _cartPrefab = cartPrefab;
            _gemPrefabs = gemPrefabs;

            _cartChargeMax = cartChargeMax;
            _cartCharge = cartChargeStart;

            _viewFactory = viewFactory;
            _onCartMeterChanged = onCartMeterChanged;
        }

        public void PushMeter()
        {
            _onCartMeterChanged?.Invoke((float)_cartCharge / _cartChargeMax);
        }

        public void AddCartCharge(int amount)
        {
            if (amount <= 0) return;

            _cartCharge += amount;

            while (_cartCharge >= _cartChargeMax)
            {
                _cartCharge -= _cartChargeMax;
                SpawnCartRandom();
            }

            PushMeter();
        }

        public void SpawnCartRandom()
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

            var old = _model.Get(c.x, c.y).Value;
            _model.Set(c.x, c.y, new Piece(old.type, SpecialType.Cart));

            var oldView = _views[c.x, c.y];
            if (oldView != null) Object.Destroy(oldView.gameObject);

            var pos = _boardView.CellToWorld(c.x, c.y);
            var go = Object.Instantiate(_cartPrefab, pos, Quaternion.identity, _gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(c.x, c.y);
            // Подписки делает контроллер через ViewFactory в идеале,
            // но оставляем как есть на проход №1: подписки делаются контроллером в месте создания.
            _views[c.x, c.y] = view;
        }

        // ⚠️ В проход №1 — логику активации тележки оставляем в ResolveSystem (чтобы не разносить зависимости).
        // Здесь можно будет позже перенести ActivateCartAt/RandomAt.
    }
}