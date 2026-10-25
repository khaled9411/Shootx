using UnityEngine;
using UnityEngine.EventSystems;

namespace Shootx.Store
{
    public class StorePreviewDragRotator : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (StorePreviewManager.Instance != null)
            {
                StorePreviewManager.Instance.RotateCharacter(eventData.delta.x);
            }
        }
    }
}
