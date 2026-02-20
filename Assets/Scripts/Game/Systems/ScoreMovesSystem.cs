using System;

namespace Match3.Game.Systems
{
    public sealed class ScoreMovesSystem
    {
        private int _movesLeft;
        private int _score;

        private readonly Action<int> _onMovesChanged;
        private readonly Action<int> _onScoreChanged;
        private readonly Action _onGameOver;

        public int MovesLeft => _movesLeft;
        public int Score => _score;

        public ScoreMovesSystem(Action<int> onMovesChanged, Action<int> onScoreChanged, Action onGameOver)
        {
            _onMovesChanged = onMovesChanged;
            _onScoreChanged = onScoreChanged;
            _onGameOver = onGameOver;
        }

        public void Init(int maxMoves)
        {
            _movesLeft = maxMoves;
            _score = 0;
            PushUIState();
        }

        public void PushUIState()
        {
            _onScoreChanged?.Invoke(_score);
            _onMovesChanged?.Invoke(_movesLeft);
        }

        // ⚠️ Проход №1: поведение как у тебя сейчас — списываем сразу
        public bool ConsumeMoveOrGameOver()
        {
            _movesLeft--;
            _onMovesChanged?.Invoke(_movesLeft);

            if (_movesLeft <= 0)
            {
                _onGameOver?.Invoke();
                return true;
            }
            return false;
        }

        public void AddDestroyed(int destroyed, int pointsPerGem)
        {
            if (destroyed <= 0) return;
            _score += destroyed * pointsPerGem;
            _onScoreChanged?.Invoke(_score);
        }
    }
}