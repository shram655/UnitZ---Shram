using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

// Перетаскивание предметов между слотами инвентаря и хотбара.
// Работает через InventoryUI.Instance (локальный игрок).
// Слот под курсором ищется ПО ПРЯМОУГОЛЬНИКУ — не зависит от raycast-проблем.
public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private int slotIndex = -1;

    // Реестр всех слотов (инвентарь + хотбар)
    private static readonly List<ItemDragHandler> allSlots = new List<ItemDragHandler>();

    // Состояние перетаскивания
    public static ItemDragHandler DragSource;
    public static int DragItemId;

    private static GameObject dragCanvasObj;
    private static Image dragIconImage;

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        if (!allSlots.Contains(this)) allSlots.Add(this);
    }

    void OnDestroy()
    {
        allSlots.Remove(this);
        if (DragSource == this) DragSource = null;
    }

    InventoryUI UI => InventoryUI.Instance;

    // ═════════════════════════════════════════════════════
    // НАЧАЛО ПЕРЕТАСКИВАНИЯ
    // ═════════════════════════════════════════════════════
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (UI == null || UI.playerInventory == null) return;
        if (ChatManager.IsChatOpen) return;
        if (slotIndex < 0 || slotIndex >= 20) return;

        int id = UI.playerInventory.inventory[slotIndex];
        int count = UI.playerInventory.inventoryCounts[slotIndex];

        // Пустой слот
        if (id == 0) return;

        // Оружие и топоры таскаются даже со счётчиком 0
        bool weaponOrMelee = UI.playerInventory.IsGun(id) || UI.playerInventory.IsMelee(id);
        if (!weaponOrMelee && count <= 0) return;

        DragSource = this;
        DragItemId = id;

        ShowDragIcon(eventData.position, id);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragSource == null) return;
        MoveDragIcon(eventData.position);
    }

    // ═════════════════════════════════════════════════════
    // Окончание: ищем слот под курсором ПО ПРЯМОУГОЛЬНИКУ
    // ═════════════════════════════════════════════════════
    public void OnEndDrag(PointerEventData eventData)
    {
        if (DragSource != null)
        {
            ItemDragHandler target = FindSlotAt(eventData.position);

            if (target != null && UI != null && UI.playerInventory != null)
            {
                UI.MoveItemBetweenSlots(DragSource.slotIndex, target.slotIndex);
            }
        }

        HideDragIcon();
        DragSource = null;
    }

    static ItemDragHandler FindSlotAt(Vector2 screenPos)
    {
        for (int i = allSlots.Count - 1; i >= 0; i--)
        {
            ItemDragHandler s = allSlots[i];
            if (s == null) { allSlots.RemoveAt(i); continue; }
            if (!s.gameObject.activeInHierarchy) continue;

            RectTransform rt = s.transform as RectTransform;
            if (rt == null) continue;

            // cam = null — правильно для Screen Space - Overlay
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                return s;
        }
        return null;
    }

    // ═════════════════════════════════════════════════════
    // ИКОНКА ПЕРЕТАСКИВАНИЯ
    // ═════════════════════════════════════════════════════
    static void EnsureDragCanvas()
    {
        if (dragCanvasObj != null) return;

        dragCanvasObj = new GameObject("DragIconCanvas");
        Canvas canvas = dragCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasObj_AddScaler(dragCanvasObj);
        dragCanvasObj.AddComponent<GraphicRaycaster>();

        GameObject iconObj = new GameObject("DragIcon");
        iconObj.transform.SetParent(dragCanvasObj.transform, false);
        RectTransform rt = iconObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(50, 50);
        dragIconImage = iconObj.AddComponent<Image>();
        dragIconImage.raycastTarget = false;
        iconObj.SetActive(false);
    }

    static void canvasObj_AddScaler(GameObject go)
    {
        go.AddComponent<CanvasScaler>();
    }

    static void ShowDragIcon(Vector2 screenPos, int itemId)
    {
        EnsureDragCanvas();
        if (dragIconImage == null) return;

        Sprite icon = InventoryUI.Instance != null
            ? InventoryUI.Instance.GetIconForItem(itemId)
            : null;

        dragIconImage.sprite = icon;
        dragIconImage.enabled = icon != null;
        dragIconImage.gameObject.SetActive(true);
        MoveDragIcon(screenPos);
    }

    static void MoveDragIcon(Vector2 screenPos)
    {
        if (dragIconImage == null) return;
        dragIconImage.rectTransform.position = screenPos;
    }

    static void HideDragIcon()
    {
        if (dragIconImage != null) dragIconImage.gameObject.SetActive(false);
    }
}