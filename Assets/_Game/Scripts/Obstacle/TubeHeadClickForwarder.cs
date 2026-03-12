using UnityEngine;
using UnityEngine.EventSystems;
using FoodMatch.Obstacle;

namespace FoodMatch.Obstacle
{
    /// <summary>
    /// Gắn vào food GameObject khi nó là head của FoodTube.
    /// Forward tất cả click về FoodTube thay vì tự xử lý.
    /// </summary>
    public class TubeHeadClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        private FoodTube _ownerTube;

        public void SetOwnerTube(FoodTube tube) => _ownerTube = tube;

        public void OnPointerClick(PointerEventData eventData)
        {
            _ownerTube?.OnPointerClick(eventData);
        }
    }
}