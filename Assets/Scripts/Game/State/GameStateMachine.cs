using System;

namespace Match3.Game.State
{
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Input;

        public event Action<GameState> OnChanged;

        public void Set(GameState state)
        {
            if (Current == state) return;
            Current = state;
            OnChanged?.Invoke(Current);
        }

        public bool Is(GameState s) => Current == s;
    }
}
