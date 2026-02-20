using Match3.Core;
using Match3.Game.State;
using Match3.View;
using UnityEngine;

namespace Match3.Game.Systems
{
    public sealed class InputRouter
    {
        private readonly GameStateMachine _fsm;
        private readonly BoardModel _model;

        private readonly System.Func<bool> _isBusy;
        private readonly System.Action<int, int, int, int> _requestSwap;
        private readonly System.Action<int, int> _requestDetonate;
        private readonly System.Action<int, int> _requestCartRandom;

        public InputRouter(
            GameStateMachine fsm,
            BoardModel model,
            System.Func<bool> isBusy,
            System.Action<int, int, int, int> requestSwap,
            System.Action<int, int> requestDetonate,
            System.Action<int, int> requestCartRandom)
        {
            _fsm = fsm;
            _model = model;
            _isBusy = isBusy;
            _requestSwap = requestSwap;
            _requestDetonate = requestDetonate;
            _requestCartRandom = requestCartRandom;
        }

        public void OnGemSwipe(GemView view, Vector2Int dir)
        {
            if (_isBusy()) return;
            if (!_fsm.Is(GameState.Input)) return;

            int ax = view.X;
            int ay = view.Y;
            int bx = ax + dir.x;
            int by = ay + dir.y;

            if (!_model.InBounds(bx, by)) return;

            _requestSwap(ax, ay, bx, by);
        }

        public void OnGemDoubleTap(GemView v)
        {
            if (_isBusy()) return;
            if (!_fsm.Is(GameState.Input)) return;

            int x = v.X;
            int y = v.Y;

            var piece = _model.Get(x, y);
            if (!piece.HasValue) return;

            if (piece.Value.special == SpecialType.Cart)
            {
                _requestCartRandom(x, y);
                return;
            }

            if (piece.Value.special == SpecialType.None)
                return;

            _requestDetonate(x, y);
        }
    }
}