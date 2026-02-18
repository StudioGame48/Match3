using UnityEngine;

namespace Match3.View
{
    public enum SpecialKind { None, Bomb4, Bomb5, Bomb6, Bomb7 }

    public sealed class GemIdView : MonoBehaviour
    {
        public int ColorType;             // какой “цвет” (тип) у гема
        public SpecialKind Special = SpecialKind.None;
    }
}
