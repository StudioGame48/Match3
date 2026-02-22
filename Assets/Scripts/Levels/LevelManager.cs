using Match3.Core;
using Match3.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)] // применяем настройки ДО Start() Match3Controller
public class LevelManager : MonoBehaviour
{
    private const string LEVEL_KEY = "LEVEL_INDEX";

    [Header("Levels")]
    [SerializeField] private LevelConfig[] levels;

    [Header("Links")]
    [SerializeField] private Match3Controller controller;
    [SerializeField] private ObjectiveSystem objectives; // опционально, если уже создали

    [Header("Flow")]
    [SerializeField] private string gameplaySceneName = ""; // если пусто, будет reload текущей сцены

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int levelIndex;
    private bool finished;

    private void Awake()
    {
        // 1) Подхват ссылок
        if (controller == null)
            controller = FindFirstObjectByType<Match3Controller>();

        if(objectives == null)
        {
            objectives = GetComponent<ObjectiveSystem>();
            if (objectives == null)
                objectives = gameObject.AddComponent<ObjectiveSystem>(); 
        }

        // 2) Проверки
        if (controller == null)
        {
            Debug.LogError("LevelManager: Match3Controller not found in scene.");
            enabled = false;
            return;
        }

        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("LevelManager: levels array is empty. Assign LevelConfig assets in Inspector.");
            enabled = false;
            return;
        }

        // 3) Индекс уровня
        levelIndex = Mathf.Clamp(PlayerPrefs.GetInt(LEVEL_KEY, 0), 0, levels.Length - 1);

        if (levels[levelIndex] == null)
        {
            Debug.LogError($"LevelManager: LevelConfig at index {levelIndex} is None (null). Fix Inspector.");
            enabled = false;
            return;
        }

        // 4) Применяем параметры уровня ДО старта контроллера
        ApplyLevel(levels[levelIndex]);

        // 5) Если ObjectiveSystem есть — инициализируем
        if (objectives != null)
        {
            objectives.Init(levels[levelIndex]);
        }

        if (logDebug)
        {
            var cfg = levels[levelIndex];
            Debug.Log($"[Level] Loaded L{levelIndex + 1}/{levels.Length}: {cfg.width}x{cfg.height}, moves={cfg.maxMoves}");
        }
    }

    private void OnEnable()
    {
        if (controller == null) return;
        Debug.Log("[LM] OnEnable subscribe");
        // победа/поражение:
        controller.OnGameOver += OnGameOver;

        // события уничтожения фишек (для целей)
        controller.OnPieceCleared += OnPieceCleared;

        // если objectives есть — победа по выполнению целей
        if (objectives != null)
            objectives.OnCompleted += Win;
    }

    private void OnDisable()
    {
        if (controller == null) return;

        controller.OnGameOver -= OnGameOver;
        controller.OnPieceCleared -= OnPieceCleared;

        if (objectives != null)
            objectives.OnCompleted -= Win;
    }

    private void ApplyLevel(LevelConfig cfg)
    {
        // Эти поля у вас уже public/serialize в Match3Controller — он их прочитает в Start()
        controller.width = cfg.width;
        controller.height = cfg.height;
        controller.maxMoves = cfg.maxMoves;
        controller.levelConfig = cfg;

        // ВАЖНО:
        // pointsPerGem / cartChargeMax / cartChargeStart у вас private [SerializeField] в Match3Controller,
        // их отсюда НЕ поменять без публичного метода.
        // Поэтому пока оставляем как есть. (Позже добавим ApplyLevelTuning)
    }

    private void OnPieceCleared(int type, SpecialType special)
    {
        // Если ObjectiveSystem уже существует — прокидываем туда.
        // Если objectives == null — просто игнор.
        if (finished) return;

        if (objectives != null)
            objectives.HandlePieceCleared(type, special);
    }

    private void OnGameOver()
    {
        if (finished) return;

        // Если у вас есть objectives — проигрыш только если цели не выполнены
        if (objectives != null && objectives.IsComplete())
        {
            Win();
            return;
        }

        Lose();
    }

    private void Win()
    {
        if (finished) return;
        finished = true;

        if (logDebug) Debug.Log($"[Level] WIN L{levelIndex + 1}");

        int next = Mathf.Min(levelIndex + 1, levels.Length - 1);
        PlayerPrefs.SetInt(LEVEL_KEY, next);
        PlayerPrefs.Save();

        ReloadGameplay();
    }

    private void Lose()
    {
        if (finished) return;
        finished = true;

        if (logDebug) Debug.Log($"[Level] LOSE L{levelIndex + 1}");

        // остаёмся на текущем уровне
        ReloadGameplay();
    }

    private void ReloadGameplay()
    {
        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- UI buttons (можно повесить на кнопки) ----------
    public void RestartLevel()
    {
        finished = true;
        ReloadGameplay();
    }

    public void NextLevelCheat()
    {
        int next = Mathf.Min(levelIndex + 1, levels.Length - 1);
        PlayerPrefs.SetInt(LEVEL_KEY, next);
        PlayerPrefs.Save();
        finished = true;
        ReloadGameplay();
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, 0);
        PlayerPrefs.Save();
        finished = true;
        ReloadGameplay();
    }
}