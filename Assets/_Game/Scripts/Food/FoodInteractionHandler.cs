using UnityEngine;
using FoodMatch.Tray;

namespace FoodMatch.Food
{
    /// <summary>
    /// Xử lý touch/click cho FoodItem 3D.
    /// </summary>
    [RequireComponent(typeof(FoodItem))]
    public class FoodInteractionHandler : MonoBehaviour
    {
        private FoodItem _foodItem;
        private FoodTray _tray;

        private void Awake()
        {
            _foodItem = GetComponent<FoodItem>();
        }

        public void SetTray(FoodTray tray)
        {
            _tray = tray;
        }

        // Unity tự gọi khi click/touch vào Collider 3D
        private void OnMouseDown()
        {
            if (_tray == null) return;

            FoodItem selected = _tray.TrySelectItem(_foodItem);

            if (selected != null)
            {
                Debug.Log($"[Interaction] Selected: {selected.Data?.foodName}");
                // Day 4: EventBus.OnItemSelected?.Invoke(selected);
            }
        }
    }
}