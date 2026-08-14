using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject inventoryPanel;
    public GameObject[] inventorySlots; // 15 слотов (0-14)

    [Header("Иконки предметов")]
    public Sprite[] itemIcons;
    public Sprite weaponIcon;
    public Sprite ammoIcon;        // 🆕 иконка патронов
    public int ammoItemId = -2;    // 🆕 ID патронов

    // ✅ PUBLIC — нужно для ItemDragHandler
    public PlayerInventory playerInventory;
    private bool isOpen = false;

    void Start()
    {
        playerInventory = FindObjectOfType<PlayerInventory>();

        // 🆕 Автоматически подтягиваем настройки патронов из PlayerInventory
        if (playerInventory != null)
        {
            if (ammoIcon == null) ammoIcon = playerInventory.ammoIcon;
            ammoItemId = playerInventory.ammoItemId;
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
        SetupDragAndDrop();
        Debug.Log("✅ InventoryUI запущен. Иконок: " + (itemIcons != null ? itemIcons.Length : 0));
    }

    // ✅ Автоматическая настройка перетаскивания
    void SetupDragAndDrop()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("✅ Создан EventSystem");
        }

        foreach (Canvas c in FindObjectsOfType<Canvas>())
        {
            if (c.GetComponent<GraphicRaycaster>() == null)
            {
                c.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("✅ Добавлен GraphicRaycaster на " + c.name);
            }
        }

        if (inventorySlots != null)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null) continue;
                EnsureSlotDraggable(inventorySlots[i], i);
            }
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject cell = GameObject.Find("Cell_" + i);
            if (cell != null) EnsureSlotDraggable(cell, i + 15);
        }

        Debug.Log("✅ Настройка перетаскивания завершена");
    }

    void EnsureSlotDraggable(GameObject slot, int index)
    {
        Image img = slot.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        ItemDragHandler handler = slot.GetComponent<ItemDragHandler>();
        if (handler == null)
            handler = slot.AddComponent<ItemDragHandler>();

        handler.SetSlotIndex(index);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(isOpen);
        }
        if (isOpen)
        {
            UpdateAllSlots();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public void UpdateAllSlots()
    {
        if (playerInventory == null) return;

        for (int i = 0; i < 15 && i < inventorySlots.Length; i++)
        {
            int blockId = playerInventory.inventory[i];
            int count = playerInventory.inventoryCounts[i];
            UpdateSlot(i, blockId, count);
        }
    }

    // ══════════════════════════════════════════════════════
    //  🆕 ОБНОВЛЕНО: теперь понимает патроны
    // ══════════════════════════════════════════════════════
    void UpdateSlot(int slotIndex, int blockId, int count)
    {
        if (slotIndex >= inventorySlots.Length) return;
        GameObject slot = inventorySlots[slotIndex];
        if (slot == null) return;

        Transform iconTransform = slot.transform.Find("Icon");
        if (iconTransform == null) return;

        Image iconImg = iconTransform.GetComponent<Image>();
        if (iconImg == null) return;

        if (blockId > 0)
        {
            // Обычный блок
            int iconIndex = blockId - 1;
            if (itemIcons != null && iconIndex >= 0 && iconIndex < itemIcons.Length && itemIcons[iconIndex] != null)
            {
                iconImg.sprite = itemIcons[iconIndex];
                iconImg.gameObject.SetActive(true);
            }
            else
            {
                iconImg.gameObject.SetActive(false);
            }
        }
        else if (blockId == ammoItemId && ammoIcon != null)
        {
            // 🆕 Патроны
            iconImg.sprite = ammoIcon;
            iconImg.gameObject.SetActive(true);
        }
        else if (blockId < 0 && weaponIcon != null)
        {
            // Оружие
            iconImg.sprite = weaponIcon;
            iconImg.gameObject.SetActive(true);
        }
        else
        {
            iconImg.gameObject.SetActive(false);
        }

        Transform countText = slot.transform.Find("CountText");
        if (countText != null)
        {
            TextMeshProUGUI txt = countText.GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = count > 1 ? count.ToString() : "";
            }
        }
    }

    // ✅ ОБМЕН ПРЕДМЕТОВ (перетаскивание)
    public void SwapItems(int slot1, int slot2)
    {
        if (playerInventory == null) return;

        if (slot1 < 0 || slot1 >= playerInventory.inventory.Length ||
            slot2 < 0 || slot2 >= playerInventory.inventory.Length)
            return;

        if (slot1 == slot2) return;

        Debug.Log("🔄 SwapItems: слот " + slot1 + " ↔ слот " + slot2);

        int tempId = playerInventory.inventory[slot1];
        playerInventory.inventory[slot1] = playerInventory.inventory[slot2];
        playerInventory.inventory[slot2] = tempId;

        int tempCount = playerInventory.inventoryCounts[slot1];
        playerInventory.inventoryCounts[slot1] = playerInventory.inventoryCounts[slot2];
        playerInventory.inventoryCounts[slot2] = tempCount;

        playerInventory.UpdateHotbarUI();
        UpdateAllSlots();

        playerInventory.CheckSelectedSlot();
    }

    // ✅ КЛИК ПО СЛОТУ ИНВЕНТАРЯ
    public void OnInventorySlotClick(int slotIndex)
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= 15) return;

        int blockId = playerInventory.inventory[slotIndex];
        int count = playerInventory.inventoryCounts[slotIndex];

        if (blockId == 0 || count <= 0)
        {
            Debug.Log("⚠️ Слот пуст!");
            return;
        }

        if (playerInventory.IsFood(blockId))
        {
            playerInventory.ConsumeItemFromInventory(slotIndex);
            Debug.Log("🍎 Предмет съеден!");
            UpdateAllSlots();
            return;
        }

        int hotbarSlot = playerInventory.selectedSlot;

        if (playerInventory.inventory[hotbarSlot] == 0)
        {
            playerInventory.inventory[hotbarSlot] = blockId;
            playerInventory.inventoryCounts[hotbarSlot] = count;
            playerInventory.inventory[slotIndex] = 0;
            playerInventory.inventoryCounts[slotIndex] = 0;
            Debug.Log("📦 Предмет перемещён в хотбар (слот " + hotbarSlot + ")");
        }
        else if (playerInventory.inventory[hotbarSlot] == blockId)
        {
            // 🆕 Для патронов — ограничение стака до 30
            if (blockId == ammoItemId)
            {
                int space = playerInventory.maxAmmoStack - playerInventory.inventoryCounts[hotbarSlot];
                int toMove = Mathf.Min(space, count);

                if (toMove <= 0)
                {
                    Debug.Log("⚠️ Стак патронов в хотбаре полон!");
                    return;
                }

                playerInventory.inventoryCounts[hotbarSlot] += toMove;
                playerInventory.inventoryCounts[slotIndex] -= toMove;

                if (playerInventory.inventoryCounts[slotIndex] <= 0)
                {
                    playerInventory.inventory[slotIndex] = 0;
                    playerInventory.inventoryCounts[slotIndex] = 0;
                }

                Debug.Log("🔫 Перемещено патронов: " + toMove);
            }
            else
            {
                playerInventory.inventoryCounts[hotbarSlot] += count;
                playerInventory.inventory[slotIndex] = 0;
                playerInventory.inventoryCounts[slotIndex] = 0;
                Debug.Log("📦 Предмет добавлен к стопке в хотбаре");
            }
        }
        else
        {
            int tempBlockId = playerInventory.inventory[hotbarSlot];
            int tempCount = playerInventory.inventoryCounts[hotbarSlot];
            playerInventory.inventory[hotbarSlot] = blockId;
            playerInventory.inventoryCounts[hotbarSlot] = count;
            playerInventory.inventory[slotIndex] = tempBlockId;
            playerInventory.inventoryCounts[slotIndex] = tempCount;
            Debug.Log("🔄 Предметы поменялись местами");
        }

        playerInventory.UpdateHotbarUI();
        UpdateAllSlots();

        playerInventory.CheckSelectedSlot();
    }
}