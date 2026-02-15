using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Match3.Core;
using Match3.View;

namespace Match3.ViewLayer
{
    public sealed class BoardView : MonoBehaviour
    {
        public float cellSize = 1f;

        [Header("Anim")]
        public float swapDuration = 0.18f;
        public float fallDuration = 0.45f;
        public float bounceStrength = 0.08f;
        public float bounceDuration = 0.06f;

        public Vector2 CellToWorld(int x, int y) => new Vector2(x * cellSize, y * cellSize);

        public IEnumerator AnimateSwap(GemView a, GemView b)
        {
            Vector2 startA = a.transform.position;
            Vector2 startB = b.transform.position;

            float time = 0f;
            while (time < swapDuration)
            {
                float t = time / swapDuration;
                float eased = t * t * (3f - 2f * t); // smoothstep

                if (a != null) a.transform.position = Vector2.Lerp(startA, startB, eased);
                if (b != null) b.transform.position = Vector2.Lerp(startB, startA, eased);

                time += Time.deltaTime;
                yield return null;
            }

            if (a != null) a.transform.position = startB;
            if (b != null) b.transform.position = startA;
        }

        public IEnumerator AnimateMoves(IEnumerable<GemView> movingViews, Dictionary<GemView, Vector2> targetPos)
        {
            // запоминаем стартовые
            var startPos = new Dictionary<GemView, Vector2>();
            foreach (var v in movingViews)
            {
                if (v == null) continue;
                startPos[v] = v.transform.position;
            }

            float time = 0f;
            while (time < fallDuration)
            {
                float t = time / fallDuration;
                float eased = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic

                foreach (var kv in startPos)
                {
                    var v = kv.Key;
                    if (v == null) continue;

                    Vector2 start = kv.Value;
                    Vector2 target = targetPos[v];

                    v.transform.position = start + (target - start) * eased;
                }

                time += Time.deltaTime;
                yield return null;
            }

            foreach (var kv in targetPos)
                if (kv.Key != null)
                    kv.Key.transform.position = kv.Value;

            yield return SmallBounce(targetPos);
        }

        IEnumerator SmallBounce(Dictionary<GemView, Vector2> moved)
        {
            if (bounceStrength <= 0f) yield break;

            float time = 0f;
            while (time < bounceDuration)
            {
                float t = time / bounceDuration;
                float offset = Mathf.Sin(t * Mathf.PI) * bounceStrength * (1f - t);

                foreach (var kv in moved)
                {
                    if (kv.Key == null) continue;
                    kv.Key.transform.position = kv.Value + Vector2.up * offset;
                }

                time += Time.deltaTime;
                yield return null;
            }

            foreach (var kv in moved)
                if (kv.Key != null)
                    kv.Key.transform.position = kv.Value;
        }
    }
}
