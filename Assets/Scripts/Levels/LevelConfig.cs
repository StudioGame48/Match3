using UnityEngine;

[CreateAssetMenu(menuName = "Match3/Level Config", fileName = "Level_01")]
public class LevelConfig : ScriptableObject
{
    [Header("Board")]
    public int width = 6;
    public int height = 6;

    [Header("Rules")]
    public int maxMoves = 20;
    public int targetScore = 500;

    [Header("Optional tuning")]
    public int pointsPerGem = 10;

    [Header("Cart")]
    public int cartChargeMax = 50;
    public int cartChargeStart = 0;
    public enum GoalType { ClearGem, ClearBomb, FillCart }

    [System.Serializable]
    public class Goal
    {
        public GoalType type;
        public int gemId;      // для ClearGem (0..N) если у вас есть типы
        public int amount;     // сколько нужно
    }

    public Goal[] goals;
}
