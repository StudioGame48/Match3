using Match3.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using static LevelConfig;

public class ObjectiveSystem : MonoBehaviour
{
    private LevelConfig cfg;

    // прогресс по целям: key = индекс цели, value = сколько сделано
    private int[] progress;

    public event Action OnObjectivesChanged;
    public event Action OnCompleted;

    public int GetProgress(int index) => progress[index];
    public Goal[] GetGoals() => cfg.goals;
    public void HandlePieceCleared(int type, SpecialType special)
    {
        if (cfg == null || cfg.goals == null) return;

        // Debug.Log($"[OBJ] type={type} special={special}");

        for (int i = 0; i < cfg.goals.Length; i++)
        {
            var g = cfg.goals[i];
            if (progress[i] >= g.amount) continue;

            // 🎯 Уничтожить обычный гем конкретного типа
            if (g.type == GoalType.ClearGem)
            {
                if (special == SpecialType.None && g.gemId == type)
                    progress[i]++;
            }
            // 💣 Уничтожить бомбы (любые спец-камни, кроме Cart)
            else if (g.type == GoalType.ClearBomb)
            {
                if (special != SpecialType.None && special != SpecialType.Cart)
                    progress[i]++;
            }
            // 🛒 Заполнить тележку (пока пример: каждый обычный гем дает +1)
            else if (g.type == GoalType.FillCart)
            {
                if (special == SpecialType.None)
                    progress[i] = Mathf.Min(g.amount, progress[i] + 1);
            }
        }

        OnObjectivesChanged?.Invoke();
        CheckComplete();
    }

    public void Init(LevelConfig level)
    {
        cfg = level;
        progress = new int[cfg.goals.Length];
        OnObjectivesChanged?.Invoke();
    }

    public void HandleGemCleared(int gemId)
    {
        if (cfg == null) return;

        for (int i = 0; i < cfg.goals.Length; i++)
        {
            var g = cfg.goals[i];
            if (g.type == GoalType.ClearGem && g.gemId == gemId && progress[i] < g.amount)
                progress[i]++;
        }
        
        CheckComplete();
        OnObjectivesChanged?.Invoke();
    }

    public void HandleBombCleared()
    {
        if (cfg == null) return;

        for (int i = 0; i < cfg.goals.Length; i++)
        {
            var g = cfg.goals[i];
            if (g.type == GoalType.ClearBomb && progress[i] < g.amount)
                progress[i]++;
        }

        CheckComplete();
        OnObjectivesChanged?.Invoke();
    }

    public void AddCart(int amount)
    {
        if (cfg == null) return;

        for (int i = 0; i < cfg.goals.Length; i++)
        {
            var g = cfg.goals[i];
            if (g.type == GoalType.FillCart && progress[i] < g.amount)
                progress[i] = Mathf.Min(g.amount, progress[i] + amount);
        }

        CheckComplete();
        OnObjectivesChanged?.Invoke();
    }

    public bool IsComplete()
    {
        if (cfg == null) return false;
        for (int i = 0; i < cfg.goals.Length; i++)
            if (progress[i] < cfg.goals[i].amount) return false;
        return true;
    }

    private void CheckComplete()
    {
        if (IsComplete())
            OnCompleted?.Invoke();
    }
}