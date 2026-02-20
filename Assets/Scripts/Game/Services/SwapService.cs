using Match3.Core;
using Match3.View;
using Match3.ViewLayer;
using System.Collections;
using UnityEngine;

namespace Match3.Game.Services
{
    public sealed class SwapService
    {
        private readonly BoardView _boardView;

        public SwapService(BoardView boardView)
        {
            _boardView = boardView;
        }

        public IEnumerator AnimateAndApplySwap(BoardModel model, GemView[,] views,
            int ax, int ay, int bx, int by)
        {
            var aView = views[ax, ay];
            var bView = views[bx, by];

            if (_boardView != null && aView != null && bView != null)
                yield return _boardView.AnimateSwap(aView, bView);

            model.Swap(ax, ay, bx, by);

            (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
            if (views[ax, ay] != null) views[ax, ay].SetCoords(ax, ay);
            if (views[bx, by] != null) views[bx, by].SetCoords(bx, by);
        }

        public IEnumerator AnimateAndRollbackSwap(BoardModel model, GemView[,] views,
            int ax, int ay, int bx, int by)
        {
            var aView = views[ax, ay];
            var bView = views[bx, by];

            if (_boardView != null && aView != null && bView != null)
                yield return _boardView.AnimateSwap(bView, aView);

            model.Swap(ax, ay, bx, by);

            (views[ax, ay], views[bx, by]) = (views[bx, by], views[ax, ay]);
            if (views[ax, ay] != null) views[ax, ay].SetCoords(ax, ay);
            if (views[bx, by] != null) views[bx, by].SetCoords(bx, by);
        }
    }
}