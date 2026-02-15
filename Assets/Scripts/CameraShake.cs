using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField] float defaultDuration = 0.08f;
    [SerializeField] float defaultMagnitude = 0.06f;
    [SerializeField] float dampingSpeed = 8f;

    Vector3 originalPos;
    Coroutine shakeRoutine;

    void Awake()
    {
        originalPos = transform.localPosition;
    }

    public void Shake(float duration = -1f, float magnitude = -1f)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        if (duration <= 0f) duration = defaultDuration;
        if (magnitude <= 0f) magnitude = defaultMagnitude;

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float time = 0f;

        while (time < duration)
        {
            float t = time / duration;

            // затухание
            float damper = 1f - Mathf.Clamp01(t);

            float offsetX = (Random.value * 2f - 1f) * magnitude * damper;
            float offsetY = (Random.value * 2f - 1f) * magnitude * damper;

            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            time += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    public void ShakeScaled(float intensity)
    {
        float duration = defaultDuration + intensity * 0.05f;
        float magnitude = defaultMagnitude + intensity * 0.05f;

        Shake(duration, magnitude);
    }

}
