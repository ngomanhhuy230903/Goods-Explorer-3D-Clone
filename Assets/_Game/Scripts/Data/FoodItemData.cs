using UnityEngine;

/// <summary>
/// ScriptableObject chứa toàn bộ thông tin của 1 loại món ăn.
/// </summary>
[CreateAssetMenu(
    fileName = "FoodItem_New",
    menuName = "FoodMatch/Food Item Data",
    order = 1)]
public class FoodItemData : ScriptableObject
{
    [Header("─── Identification ───────────────────")]
    [Tooltip("ID duy nhất cho loại món ăn này. Dùng để so sánh match.")]
    public int foodID;

    [Tooltip("Tên hiển thị trong editor và debug log.")]
    public string foodName;

    [Header("─── Visuals ─────────────────────────")]
    [Tooltip("Prefab 3D/2D của món ăn đặt trên khay.")]
    public GameObject prefab;

    [Tooltip("Icon 2D hiển thị trên UI Order của khách hàng.")]
    public Sprite iconSprite;

    [Tooltip("Sprite phiên bản màu xám (bị khóa - ở lớp dưới).")]
    public Sprite iconGraySprite;

    [Tooltip("Màu tint khi món ở lớp bị khóa (dùng nếu không có ảnh xám riêng).")]
    public Color lockedTintColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("─── Audio ───────────────────────────")]
    [Tooltip("Âm thanh phát khi người chơi chạm vào món này.")]
    public AudioClip touchSound;

    [Tooltip("Âm thanh phát khi món match thành công với order.")]
    public AudioClip matchSound;

    [Header("─── Debug ───────────────────────────")]
    [Tooltip("Màu gizmo trong Scene view để dễ debug.")]
    public Color debugColor = Color.white;
}
