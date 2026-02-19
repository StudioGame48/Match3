using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Match3.UI
{
    public sealed class CartMeterUI : MonoBehaviour
    {
        [SerializeField] private Match3.Game.Match3Controller controller;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (controller == null)
                controller = FindFirstObjectByType<Match3.Game.Match3Controller>();
        }

        private void OnEnable()
        {
            if (controller != null)
                controller.OnCartMeterChanged += HandleChanged;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.OnCartMeterChanged -= HandleChanged;
        }

        private void HandleChanged(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);

            if (fillImage != null)
                fillImage.fillAmount = normalized;

            if (label != null)
                label.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
        }
    }
}
