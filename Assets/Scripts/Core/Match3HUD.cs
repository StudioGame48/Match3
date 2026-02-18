using Match3.Game;
using TMPro;
using UnityEngine;

public class Match3HUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text movesText;

    [Header("Links")]
    [SerializeField] private Match3Controller controller; // можно не задавать, найдём сами

    private void Awake()
    {
        if (controller == null)
            controller = FindFirstObjectByType<Match3Controller>();

        if (controller == null)
        {
            Debug.LogError("Match3HUD: Match3Controller не найден на сцене.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        controller.OnScoreChanged += SetScore;
        controller.OnMovesChanged += SetMoves;

        // ВАЖНО: попросим контроллер отдать текущие значения сразу
        controller.PushUIState();
    }

    private void OnDisable()
    {
        if (controller == null) return;

        controller.OnScoreChanged -= SetScore;
        controller.OnMovesChanged -= SetMoves;
    }

    private void SetScore(int value)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {value}";
    }

    private void SetMoves(int value)
    {
        if (movesText != null)
            movesText.text = $"Moves: {value}";
    }
}
