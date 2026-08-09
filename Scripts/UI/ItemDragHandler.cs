using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static GameObject dragGhost = null;
    private static int draggedSlotIndex = -1;
    private static InventoryUI draggedFromUI = null;

    private CanvasGroup canvasGroup;
    private int slotIndex = -1;
    private InventoryUI inventoryUI;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        inventoryUI = GetComponentInParent<InventoryUI>();
        if (inventoryUI == null)
            inventoryUI = FindObjectOfType<InventoryUI>();

        if (inventoryUI == null)
            Debug.LogError("❌ ItemDragHandler на " + gameObject.name + ": InventoryUI не найден!");
    }

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    public int GetSlotIndex()
    {
        return slotIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventoryUI == null || inventoryUI.player == null) return;
        if (slotIndex < 0 || slotIndex >= inventoryUI.player.inventory.Length) return;
        if (inventoryUI.player.inventory[slotIndex] == 0) return;

        Debug.Log("[Drag] ✅ Начало перетаскивания из слота " + slotIndex);

        canvasGroup.alpha = 0.3f;
        canvasGroup.blocksRaycasts = false;

        CreateDragGhost();

        draggedSlotIndex = slotIndex;
        draggedFromUI = inventoryUI;
    }

    void CreateDragGhost()
    {
        if (dragGhost != null)
            Destroy(dragGhost);

        Transform iconTransform = transform.Find("Icon");
        if (iconTransform == null) return;

        Image iconImg = iconTransform.GetComponent<Image>();
        if (iconImg == null || iconImg.sprite == null) return;

        RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
        if (iconRect == null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();

        dragGhost = new GameObject("DragGhost");
        dragGhost.transform.SetParent(parentCanvas != null ? parentCanvas.transform : transform.root, false);
        dragGhost.transform.SetAsLastSibling();

        RectTransform rt = dragGhost.AddComponent<RectTransform>();
        rt.sizeDelta = iconRect.sizeDelta;
        rt.anchorMin = iconRect.anchorMin;
        rt.anchorMax = iconRect.anchorMax;
        rt.pivot = iconRect.pivot;
        rt.localScale = iconRect.localScale;

        Image ghostImg = dragGhost.AddComponent<Image>();
        ghostImg.sprite = iconImg.sprite;
        ghostImg.color = new Color(1, 1, 1, 0.9f);
        ghostImg.raycastTarget = false;
        ghostImg.preserveAspect = iconImg.preserveAspect;

        CanvasGroup cg = dragGhost.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }

        if (draggedFromUI == null)
        {
            draggedSlotIndex = -1;
            return;
        }

        ItemDragHandler dropHandler = FindDropTarget(eventData);

        if (dropHandler != null)
        {
            int targetSlot = dropHandler.GetSlotIndex();
            Debug.Log("[Drag] ✅ Цель: слот " + targetSlot);

            if (targetSlot >= 0 && draggedSlotIndex >= 0 && targetSlot != draggedSlotIndex)
            {
                draggedFromUI.SwapItems(draggedSlotIndex, targetSlot);
            }
        }
        else
        {
            Debug.LogWarning("[Drag] ⚠️ Цель не найдена (отпустил вне слота)");
        }

        draggedSlotIndex = -1;
        draggedFromUI = null;
    }

    ItemDragHandler FindDropTarget(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject.name == "DragGhost") continue;
            if (result.gameObject == gameObject) continue;

            ItemDragHandler handler = result.gameObject.GetComponent<ItemDragHandler>();
            if (handler != null && handler.GetSlotIndex() >= 0)
            {
                return handler;
            }
        }

        return null;
    }
}