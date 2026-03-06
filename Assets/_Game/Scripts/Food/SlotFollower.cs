using UnityEngine;

namespace FoodMatch.Food
{
    /// <summary>
    /// Gắn vào FoodItem prefab.
    /// Mỗi frame, ép food về đúng world position của targetSlot,
    /// nhưng KHÔNG làm con của slot → scale/rotation hoàn toàn độc lập.
    ///
    /// Khi targetSlot = null (food đang bay đi order hoặc không có slot),
    /// script tự dừng → DoTween/Animation chạy bình thường.
    /// </summary>
    public class SlotFollower : MonoBehaviour
    {
        [HideInInspector]
        public Transform targetSlot;

        /// <summary>
        /// Gán slot để food bắt đầu bám theo.
        /// </summary>
        public void Follow(Transform slot) => targetSlot = slot;

        /// <summary>
        /// Ngừng bám (chuẩn bị bay đi order hoặc về pool).
        /// </summary>
        public void Unfollow() => targetSlot = null;

        private void LateUpdate()
        {
            if (targetSlot == null) return;

            // Ép world position khớp slot mỗi frame
            transform.position = targetSlot.position;

            // Nếu muốn xoay theo khay, bật dòng dưới:
            // transform.rotation = targetSlot.rotation;
        }
    }
}