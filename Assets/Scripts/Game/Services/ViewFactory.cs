using Match3.View;
using Match3.ViewLayer;
using UnityEngine;

namespace Match3.Game.Services
{
    public sealed class ViewFactory
    {
        private readonly BoardView _boardView;
        private readonly Transform _gridParent;

        private readonly System.Action<GemView, Vector2Int> _onSwipe;
        private readonly System.Action<GemView> _onDoubleTap;

        public ViewFactory(BoardView boardView, Transform gridParent,
            System.Action<GemView, Vector2Int> onSwipe,
            System.Action<GemView> onDoubleTap)
        {
            _boardView = boardView;
            _gridParent = gridParent;
            _onSwipe = onSwipe;
            _onDoubleTap = onDoubleTap;
        }

        public GemView CreateGem(GameObject prefab, int x, int y, int height, bool spawnFromAbove, float cellSize)
        {
            Vector2 pos = _boardView.CellToWorld(x, y);
            if (spawnFromAbove)
                pos = _boardView.CellToWorld(x, height) + Vector2.up * cellSize;

            var go = Object.Instantiate(prefab, pos, Quaternion.identity, _gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(x, y);
            view.OnSwipe += _onSwipe;
            view.OnDoubleTap += _onDoubleTap;

            var id = go.GetComponent<GemIdView>();
            if (id != null)
            {
                // тип/спешл выставляет контроллер (или ResolveSystem)
                id.Special = SpecialKind.None;
            }

            return view;
        }

        public GemView ReplaceCellWithPrefab(GemView oldView, GameObject prefab, int x, int y)
        {
            if (oldView != null) Object.Destroy(oldView.gameObject);

            var pos = _boardView.CellToWorld(x, y);
            var go = Object.Instantiate(prefab, pos, Quaternion.identity, _gridParent);

            var view = go.GetComponent<GemView>();
            view.Bind(x, y);
            view.OnSwipe += _onSwipe;
            view.OnDoubleTap += _onDoubleTap;

            return view;
        }
    }
}