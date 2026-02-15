using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Match3Board : MonoBehaviour
{
    public int width = 6;
    public int height = 6;
    public float cellSize = 1f;
    public GameObject[] gemPrefabs;
    public Transform gridParent;

    private Gem[,] grid;
    private bool busy;

    [SerializeField] float fallSpeed = 4f;
    [SerializeField] AnimationCurve fallCurve;
    [SerializeField] float fallDuration = 0.45f;
    [SerializeField] float bounceStrength = 0.08f;
    [SerializeField] float bounceDuration = 0.06f;
    




    void Start()
    {
        grid = new Gem[width, height];
        Generate();

        StartCoroutine(InitialDrop());
    }


    void Generate()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Spawn(x, y);
    }

    void Spawn(int x, int y)
    {
        int type = GetSafeType(x, y);

        Vector2 spawnPos = new Vector2(x * cellSize, height * cellSize);

        GameObject obj = Instantiate(gemPrefabs[type], spawnPos, Quaternion.identity, gridParent);

        Gem gem = obj.GetComponent<Gem>();
        gem.Init(x, y, type, this);

        grid[x, y] = gem;
    }

    int GetSafeType(int x, int y)
    {
        List<int> available = new List<int>();

        for (int i = 0; i < gemPrefabs.Length; i++)
            available.Add(i);

        // Проверка горизонтали
        if (x >= 2)
        {
            if (grid[x - 1, y] != null &&
                grid[x - 2, y] != null)
            {
                int type1 = grid[x - 1, y].type;
                int type2 = grid[x - 2, y].type;

                if (type1 == type2)
                    available.Remove(type1);
            }
        }

        // Проверка вертикали
        if (y >= 2)
        {
            if (grid[x, y - 1] != null &&
                grid[x, y - 2] != null)
            {
                int type1 = grid[x, y - 1].type;
                int type2 = grid[x, y - 2].type;

                if (type1 == type2)
                    available.Remove(type1);
            }
        }

        // Если всё удалилось (теоретически невозможно при >=4 типах)
        if (available.Count == 0)
            return Random.Range(0, gemPrefabs.Length);

        return available[Random.Range(0, available.Count)];
    }


    IEnumerator InitialDrop()
    {
        busy = true;
        yield return StartCoroutine(AnimateBoard());

        // если при старте случайно есть совпадения — убрать их
        yield return Resolve();
        busy = false;
    }



    public void TrySwap(Gem gem, Vector2Int dir)
    {
        if (busy) return;

        int nx = gem.x + dir.x;
        int ny = gem.y + dir.y;

        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            return;

        StartCoroutine(SwapRoutine(gem, grid[nx, ny]));
    }

    IEnumerator SwapRoutine(Gem a, Gem b)
    {
        busy = true;

        SwapData(a, b);

        if (!HasMatch())
        {
            SwapData(a, b);
            busy = false;
            yield break;
        }

        yield return Resolve();
        busy = false;
    }

    void SwapData(Gem a, Gem b)
    {
        grid[a.x, a.y] = b;
        grid[b.x, b.y] = a;

        (a.x, b.x) = (b.x, a.x);
        (a.y, b.y) = (b.y, a.y);

        Vector3 temp = a.transform.position;
        a.transform.position = b.transform.position;
        b.transform.position = temp;
    }

    IEnumerator Resolve()
    {
        while (true)
        {
            var matches = FindMatches();
            if (matches.Count == 0)
                break;

            DestroyMatches(matches);
            yield return new WaitForSeconds(0.1f);
            yield return ApplyGravity();
            yield return Fill();
        }
    }

    List<Gem> FindMatches()
    {
        HashSet<Gem> result = new HashSet<Gem>();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Gem g = grid[x, y];
                if (g == null) continue;

                // горизонталь
                if (x < width - 2)
                {
                    if (grid[x + 1, y] != null &&
                        grid[x + 2, y] != null &&
                        g.type == grid[x + 1, y].type &&
                        g.type == grid[x + 2, y].type)
                    {
                        result.Add(g);
                        result.Add(grid[x + 1, y]);
                        result.Add(grid[x + 2, y]);
                    }
                }

                // вертикаль
                if (y < height - 2)
                {
                    if (grid[x, y + 1] != null &&
                        grid[x, y + 2] != null &&
                        g.type == grid[x, y + 1].type &&
                        g.type == grid[x, y + 2].type)
                    {
                        result.Add(g);
                        result.Add(grid[x, y + 1]);
                        result.Add(grid[x, y + 2]);
                    }
                }
            }

        return new List<Gem>(result);
    }

    void DestroyMatches(List<Gem> matches)
    {
        foreach (Gem g in matches)
        {
            if (g == null) continue;

            grid[g.x, g.y] = null;
            Destroy(g.gameObject);
        }
    }

    IEnumerator ApplyGravity()
    {
        bool moved;

        do
        {
            moved = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    if (grid[x, y] != null && grid[x, y - 1] == null)
                    {
                        Gem g = grid[x, y];

                        grid[x, y - 1] = g;
                        grid[x, y] = null;

                        g.y--;

                        moved = true;
                    }
                }
            }

        } while (moved);

        // Теперь анимируем ВСЁ сразу
        yield return StartCoroutine(AnimateBoard());
    }


    IEnumerator AnimateBoard()
    {
        Dictionary<Gem, Vector2> startPos = new Dictionary<Gem, Vector2>();
        Dictionary<Gem, Vector2> targetPos = new Dictionary<Gem, Vector2>();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null) continue;

                Gem g = grid[x, y];
                Vector2 target = new Vector2(x * cellSize, y * cellSize);

                if ((Vector2)g.transform.position != target)
                {
                    startPos[g] = g.transform.position;
                    targetPos[g] = target;
                }
            }

        float time = 0f;

        while (time < fallDuration)
        {
            float t = time / fallDuration;

            // EaseOutCubic (очень близко к Homescapes)
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            foreach (var pair in startPos)
            {
                if (pair.Key == null) continue;

                Vector2 start = pair.Value;
                Vector2 target = targetPos[pair.Key];

                pair.Key.transform.position =
                    start + (target - start) * eased;
            }

            time += Time.deltaTime;
            yield return null;
        }

        foreach (var pair in targetPos)
        {
            if (pair.Key != null)
                pair.Key.transform.position = pair.Value;
        }

        yield return StartCoroutine(SmallBounce(targetPos));
    }



    IEnumerator SmallBounce(Dictionary<Gem, Vector2> moved)
    {
        if (bounceStrength <= 0f) yield break;

        float time = 0f;

        while (time < bounceDuration)
        {
            float t = time / bounceDuration;

            // быстрый плотный bounce
            float offset = Mathf.Sin(t * Mathf.PI) * bounceStrength * (1f - t);

            foreach (var pair in moved)
            {
                if (pair.Key == null) continue;

                pair.Key.transform.position =
                    pair.Value + Vector2.up * offset;
            }

            time += Time.deltaTime;
            yield return null;
        }

        foreach (var pair in moved)
        {
            if (pair.Key != null)
                pair.Key.transform.position = pair.Value;
        }
    }



    IEnumerator Fill()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] == null)
                    Spawn(x, y);

        yield return StartCoroutine(AnimateBoard());
    }

    bool HasMatch()
    {
        return FindMatches().Count > 0;
    }
}
