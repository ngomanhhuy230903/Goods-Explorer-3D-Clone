using UnityEngine;

/// <summary>
/// Gắn tạm vào Main Camera để debug xem raycast có hit food không.
/// Sau khi xác nhận tap hoạt động thì xóa script này.
/// </summary>
public class ClickDebugger : MonoBehaviour
{
    private void Update()
    {
        // Nhận cả click lẫn touch
        bool inputDown = Input.GetMouseButtonDown(0);

#if UNITY_IOS || UNITY_ANDROID
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            inputDown = true;
#endif

        if (!inputDown) return;

        Vector3 screenPos = Input.mousePosition;
#if UNITY_IOS || UNITY_ANDROID
        if (Input.touchCount > 0)
            screenPos = Input.GetTouch(0).position;
#endif

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Debug.Log($"[ClickDebugger] Ray từ {screenPos}");

        // Raycast ALL — không lọc layer
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        if (hits.Length == 0)
        {
            Debug.LogWarning("[ClickDebugger] Raycast KHÔNG hit gì cả! " +
                             "Kiểm tra Collider trên food.");
            return;
        }

        foreach (var hit in hits)
        {
            Debug.Log($"[ClickDebugger] Hit: {hit.collider.gameObject.name} " +
                      $"| Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                      $"| Has FoodInteractionHandler: " +
                      $"{hit.collider.GetComponentInParent<FoodMatch.Food.FoodInteractionHandler>() != null}");
        }
    }
}