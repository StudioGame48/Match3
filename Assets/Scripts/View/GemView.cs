using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;

namespace Match3.View
{
    public sealed class GemView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public int X { get; private set; }
        public int Y { get; private set; }

        public event Action<GemView, Vector2Int> OnSwipe;

        Vector2 startTouch;

        public void Bind(int x, int y)
        {
            X = x; Y = y;
        }

        public void SetCoords(int x, int y)
        {
            X = x; Y = y;
        }

        public void OnPointerDown(PointerEventData e) => startTouch = e.position;

        public void OnPointerUp(PointerEventData e)
        {
            Vector2 delta = e.position - startTouch;

            Vector2Int dir;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                dir = delta.x > 40 ? Vector2Int.right : (delta.x < -40 ? Vector2Int.left : Vector2Int.zero);
            else
                dir = delta.y > 40 ? Vector2Int.up : (delta.y < -40 ? Vector2Int.down : Vector2Int.zero);

            if (dir != Vector2Int.zero)
                OnSwipe?.Invoke(this, dir);
        }

        public IEnumerator PlayDestroy(float duration = 0.15f, float scaleMul = 1.2f)
        {
            var sr = GetComponent<SpriteRenderer>();
            var startScale = transform.localScale;
            var endScale = startScale * scaleMul;

            var startColor = sr != null ? sr.color : Color.white;
            var endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            float t = 0f;
            while (t < duration)
            {
                float k = t / duration;
                k = 1f - Mathf.Pow(1f - k, 3f);

                transform.localScale = Vector3.Lerp(startScale, endScale, k);
                if (sr != null) sr.color = Color.Lerp(startColor, endColor, k);

                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
