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

    // ✅ PUBLIC — нужно для ItemDragHandler
    public Move_Player player;
    private bool isOpen = false;

    void Start()
    {
        player = FindObjectOfType<Move_Player>();
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
        if (player == null) return;

        for (int i = 0; i < 15 && i < inventorySlots.Length; i++)
        {
            int blockId = player.inventory[i];
            int count = player.inventoryCounts[i];
            UpdateSlot(i, blockId, count);
        }
    }

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
        else if (blockId < 0 && weaponIcon != null)
        {
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
        if (player == null) return;

        if (slot1 < 0 || slot1 >= player.inventory.Length ||
            slot2 < 0 || slot2 >= player.inventory.Length)
            return;

        if (slot1 == slot2) return;

        Debug.Log("🔄 SwapItems: слот " + slot1 + " ↔ слот " + slot2);

        int tempId = player.inventory[slot1];
        player.inventory[slot1] = player.inventory[slot2];
        player.inventory[slot2] = tempId;

        int tempCount = player.inventoryCounts[slot1];
        player.inventoryCounts[slot1] = player.inventoryCounts[slot2];
        player.inventoryCounts[slot2] = tempCount;

        player.UpdateHotbarUI();
        UpdateAllSlots();

        // ✅ ИСПРАВЛЕНИЕ: проверяем, нужно ли надеть/снять оружие после обмена
        player.CheckSelectedSlot();
    }

    // ✅ КЛИК ПО СЛОТУ ИНВЕНТАРЯ
    public void OnInventorySlotClick(int slotIndex)
    {
        if (player == null) return;
        if (slotIndex < 0 || slotIndex >= 15) return;

        int blockId = player.inventory[slotIndex];
        int count = player.inventoryCounts[slotIndex];

        if (blockId == 0 || count <= 0)
        {
            Debug.Log("⚠️ Слот пуст!");
            return;
        }

        if (player.IsFood(blockId))
        {
            player.ConsumeItemFromInventory(slotIndex);
            Debug.Log("🍎 Предмет съеден!");
            UpdateAllSlots();
            return;
        }

        int hotbarSlot = player.selectedSlot;

        if (player.inventory[hotbarSlot] == 0)
        {
            player.inventory[hotbarSlot] = blockId;
            player.inventoryCounts[hotbarSlot] = count;
            player.inventory[slotIndex] = 0;
            player.inventoryCounts[slotIndex] = 0;
            Debug.Log("📦 Предмет перемещён в хотбар (слот " + hotbarSlot + ")");
        }
        else if (player.inventory[hotbarSlot] == blockId)
        {
            player.inventoryCounts[hotbarSlot] += count;
            player.inventory[slotIndex] = 0;
            player.inventoryCounts[slotIndex] = 0;
            Debug.Log("📦 Предмет добавлен к стопке в хотбаре");
        }
        else
        {
            int tempBlockId = player.inventory[hotbarSlot];
            int tempCount = player.inventoryCounts[hotbarSlot];
            player.inventory[hotbarSlot] = blockId;
            player.inventoryCounts[hotbarSlot] = count;
            player.inventory[slotIndex] = tempBlockId;
            player.inventoryCounts[slotIndex] = tempCount;
            Debug.Log("🔄 Предметы поменялись местами");
        }

        player.UpdateHotbarUI();
        UpdateAllSlots();

        // ✅ ИСПРАВЛЕНИЕ: проверяем, нужно ли надеть/снять оружие после перемещения
        player.CheckSelectedSlot();
    }
}