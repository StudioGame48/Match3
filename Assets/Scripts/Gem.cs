using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public enum SpecialType
{
    None,
    LineHorizontal,
    LineVertical,
    Bomb
}

public class Gem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int x;
    public int y;
    public int type;
    public SpecialType special;

    private Match3Board board;
    private Vector2 startTouch;

    [SerializeField] float squashAmount = 0.12f;
    [SerializeField] float squashDuration = 0.08f;

    private Coroutine squashRoutine;

    [SerializeField] float destroyDuration = 0.15f;
    [SerializeField] float destroyScale = 1.2f;

    private bool isDestroying = false;



    public void Init(int px, int py, int pType, Match3Board pBoard)
    {
        x = px;
        y = py;
        type = pType;
        board = pBoard;
        special = SpecialType.None;
    }

    public void OnPointerDown(PointerEventData e)
    {
        startTouch = e.position;

        if (squashRoutine != null)
            StopCoroutine(squashRoutine);

        squashRoutine = StartCoroutine(Squash());
    }

    IEnumerator Squash()
    {
        Vector3 original = Vector3.one;

        Vector3 squashed = new Vector3(
            1f + squashAmount,
            1f - squashAmount,
            1f);

        float time = 0f;

        // фаза сжатия
        while (time < squashDuration)
        {
            float t = time / squashDuration;
            float eased = t * t * (3f - 2f * t); // smoothstep

            transform.localScale = Vector3.Lerp(original, squashed, eased);

            time += Time.deltaTime;
            yield return null;
        }

        time = 0f;

        // возврат
        while (time < squashDuration)
        {
            float t = time / squashDuration;
            float eased = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(squashed, original, eased);

            time += Time.deltaTime;
            yield return null;
        }

        transform.localScale = original;
    }


    public void OnPointerUp(PointerEventData e)
    {
        Vector2 delta = e.position - startTouch;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            if (delta.x > 40)
                board.TrySwap(this, Vector2Int.right);
            else if (delta.x < -40)
                board.TrySwap(this, Vector2Int.left);
        }
        else
        {
            if (delta.y > 40)
                board.TrySwap(this, Vector2Int.up);
            else if (delta.y < -40)
                board.TrySwap(this, Vector2Int.down);
        }
    }

    public IEnumerator PlayDestroy()
    {
        if (isDestroying) yield break;
        isDestroying = true;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * destroyScale;

        Color startColor = sr.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float time = 0f;

        while (time < destroyDuration)
        {
            float t = time / destroyDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            transform.localScale = Vector3.Lerp(startScale, endScale, eased);

            if (sr != null)
                sr.color = Color.Lerp(startColor, endColor, eased);

            time += Time.deltaTime;
            yield return null;
        }

        transform.localScale = endScale;

        if (sr != null)
            sr.color = endColor;
    }

}
