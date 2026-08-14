using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private int slotIndex;
    private InventoryUI inventoryUI;
    private CanvasGroup canvasGroup;

    // Визуальная копия иконки, которая следует за курсором
    private GameObject dragGhost;

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    void Awake()
    {
        inventoryUI = FindObjectOfType<InventoryUI>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // ═══════════════════════════════════════════
    //  НАЧАЛО ПЕРЕТАСКИВАНИЯ
    // ═══════════════════════════════════════════
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventoryUI == null || inventoryUI.playerInventory == null) return;

        // Пустой слот — не тащим
        if (slotIndex >= 0 && slotIndex < 20)
        {
            int blockId = inventoryUI.playerInventory.inventory[slotIndex];
            if (blockId == 0) return;
        }

        // Ищем иконку внутри ячейки
        Transform iconTransform = transform.Find("Icon");
        if (iconTransform == null) return;

        Image iconImg = iconTransform.GetComponent<Image>();
        if (iconImg == null || iconImg.sprite == null) return;

        // ── Создаём "призрак" — визуальную копию иконки ──
        dragGhost = new GameObject("DragGhost");
        dragGhost.transform.SetParent(GetRootCanvas().transform, false);
        dragGhost.transform.SetAsLastSibling(); // Поверх всех элементов

        RectTransform ghostRect = dragGhost.AddComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(50f, 50f);

        Image ghostImg = dragGhost.AddComponent<Image>();
        ghostImg.sprite = iconImg.sprite;
        ghostImg.raycastTarget = false; // Не мешает кликам

        // Позиция = текущая позиция мыши
        SetGhostPosition(eventData.position);

        // ── Скрываем оригинальную иконку на время перетаскивания ──
        iconImg.enabled = false;

        // ── Отключаем raycast ячейки, чтобы курсор "видел" другие слоты ──
        canvasGroup.blocksRaycasts = false;

        Debug.Log($"🖐️ Начало перетаскивания из слота {slotIndex}");
    }

    // ═══════════════════════════════════════════
    //  ПРОЦЕСС ПЕРЕТАСКИВАНИЯ
    // ═══════════════════════════════════════════
    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            SetGhostPosition(eventData.position);
        }
    }

    // ═══════════════════════════════════════════
    //  КОНЕЦ ПЕРЕТАСКИВАНИЯ
    // ═══════════════════════════════════════════
    public void OnEndDrag(PointerEventData eventData)
    {
        // ── Возвращаем видимость оригинальной иконки ──
        Transform iconTransform = transform.Find("Icon");
        if (iconTransform != null)
        {
            Image iconImg = iconTransform.GetComponent<Image>();
            if (iconImg != null) iconImg.enabled = true;
        }

        // ── Включаем raycast обратно ──
        canvasGroup.blocksRaycasts = true;

        // ── Удаляем "призрак" ──
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }

        if (inventoryUI == null || inventoryUI.playerInventory == null) return;

        // ── Определяем, куда отпустили предмет ──
        GameObject targetObject = eventData.pointerEnter;

        // Если pointerEnter пустой (из-за raycast), ищем вручную
        if (targetObject == null)
        {
            targetObject = FindObjectAtPosition(eventData.position);
        }

        if (targetObject != null)
        {
            ItemDragHandler targetHandler = targetObject.GetComponent<ItemDragHandler>();
            if (targetHandler != null && targetHandler.slotIndex != slotIndex)
            {
                Debug.Log($"🔄 Обмен: слот {slotIndex} → слот {targetHandler.slotIndex}");
                inventoryUI.SwapItems(slotIndex, targetHandler.slotIndex);
            }
        }

        Debug.Log($"🖐️ Конец перетаскивания");
    }

    // ═══════════════════════════════════════════
    //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ═══════════════════════════════════════════

    /// <summary>
    /// Позиционирует "призрак" по координатам мыши с учётом Canvas Scaler
    /// </summary>
    private void SetGhostPosition(Vector2 screenPosition)
    {
        if (dragGhost == null) return;

        Canvas rootCanvas = GetRootCanvas();
        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();

        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Для Overlay просто конвертируем экранные координаты
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, rootCanvas.worldCamera, out localPoint))
            {
                dragGhost.transform.localPosition = localPoint;
            }
        }
        else
        {
            dragGhost.transform.position = screenPosition;
        }
    }

    /// <summary>
    /// Находит корневой Canvas
    /// </summary>
    private Canvas GetRootCanvas()
    {
        Transform t = transform;
        while (t != null)
        {
            Canvas c = t.GetComponent<Canvas>();
            if (c != null && c.isRootCanvas) return c;
            t = t.parent;
        }
        return FindObjectOfType<Canvas>();
    }

    /// <summary>
    /// Запасной поиск объекта под курсором через Raycast
    /// </summary>
    private GameObject FindObjectAtPosition(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return null;

        PointerEventData pe = new PointerEventData(eventSystem);
        pe.position = screenPosition;

        var results = new System.Collections.Generic.List<RaycastResult>();
        eventSystem.RaycastAll(pe, results);

        foreach (var result in results)
        {
            ItemDragHandler handler = result.gameObject.GetComponent<ItemDragHandler>();
            if (handler != null)
            {
                return result.gameObject;
            }
        }

        return null;
    }
}