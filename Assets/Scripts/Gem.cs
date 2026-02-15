using UnityEngine;
using UnityEngine.EventSystems;

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
}
