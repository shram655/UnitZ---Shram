using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject inventoryPanel;
    public GameObject[] inventorySlots;

    [Header("Иконки предметов")]
    public Sprite[] itemIcons;
    public Sprite ammoIcon;
    public int ammoItemId = -2;

    public PlayerInventory playerInventory;

    private bool isOpen = false;

    void Start()
    {
        // 🆕 ИСПРАВЛЕНО: привязываемся ТОЛЬКО к ЛОКАЛЬНОМУ инвентарю!
        // (раньше FindObjectOfType при 2 игроках возвращал ЧУЖОЙ пустой инвентарь)
        playerInventory = FindLocalPlayerInventory();

        if (playerInventory != null)
        {
            if (ammoIcon == null) ammoIcon = playerInventory.ammoIcon;
            ammoItemId = playerInventory.ammoItemId;
        }

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        SetupDragAndDrop();

        Debug.Log($"✅ InventoryUI: привязан к инвентарю {(playerInventory != null ? "ЛОКАЛЬНОГО игрока" : "NULL!")}");
    }

    // 🆕 Поиск инвентаря ЛОКАЛЬНОГО игрока
    PlayerInventory FindLocalPlayerInventory()
    {
        // Если ссылка уже назначена в Inspector и это локальный игрок — оставляем
        if (playerInventory != null)
        {
            PlayerController self = playerInventory.GetComponent<PlayerController>();
            if (self != null && self.view != null && self.view.IsMine) return playerInventory;
        }

        // Ищем инвентарь локального игрока в сцене
        foreach (var inv in FindObjectsOfType<PlayerInventory>())
        {
            if (inv == null) continue;
            PlayerController pc = inv.GetComponent<PlayerController>();
            if (pc != null && pc.view != null && pc.view.IsMine) return inv;
        }

        return FindObjectOfType<PlayerInventory>();
    }

    void SetupDragAndDrop()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        foreach (Canvas c in FindObjectsOfType<Canvas>())
        {
            if (c.GetComponent<GraphicRaycaster>() == null)
                c.gameObject.AddComponent<GraphicRaycaster>();
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
    }

    void EnsureSlotDraggable(GameObject slot, int index)
    {
        Image img = slot.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        ItemDragHandler handler = slot.GetComponent<ItemDragHandler>();
        if (handler == null) handler = slot.AddComponent<ItemDragHandler>();
        handler.SetSlotIndex(index);
    }

    void Update()
    {
        // БЛОКИРОВКА ОТКРЫТИЯ ИНВЕНТАРЯ ВО ВРЕМЯ ЧАТА
        if (ChatManager.IsChatOpen) return;

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;

        if (inventoryPanel != null) inventoryPanel.SetActive(isOpen);

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

    public bool IsOpen() { return isOpen; }

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
            else iconImg.gameObject.SetActive(false);
        }
        else if (blockId == ammoItemId)
        {
            if (ammoIcon != null)
            {
                iconImg.sprite = ammoIcon;
                iconImg.gameObject.SetActive(true);
            }
            else iconImg.gameObject.SetActive(false);
        }
        else if (playerInventory != null && playerInventory.IsGun(blockId))
        {
            int weaponId = playerInventory.GetWeaponIdFromItemId(blockId);
            Sprite icon = playerInventory.GetWeaponIcon(weaponId);
            if (icon != null)
            {
                iconImg.sprite = icon;
                iconImg.gameObject.SetActive(true);
            }
            else iconImg.gameObject.SetActive(false);
        }
        else if (playerInventory != null && playerInventory.IsMelee(blockId))
        {
            int meleeId = playerInventory.GetMeleeIdFromItemId(blockId);
            Sprite icon = playerInventory.GetMeleeIcon(meleeId);
            if (icon != null)
            {
                iconImg.sprite = icon;
                iconImg.gameObject.SetActive(true);
            }
            else iconImg.gameObject.SetActive(false);
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
                if (blockId == ammoItemId ||
                    (playerInventory != null && playerInventory.IsGun(blockId)) ||
                    (playerInventory != null && playerInventory.IsMelee(blockId)))
                {
                    txt.text = count > 0 ? count.ToString() : "";
                }
                else
                {
                    txt.text = count > 1 ? count.ToString() : "";
                }
            }
        }
    }

    public void SwapItems(int slot1, int slot2)
    {
        if (playerInventory == null) return;

        if (slot1 < 0 || slot1 >= playerInventory.inventory.Length ||
            slot2 < 0 || slot2 >= playerInventory.inventory.Length) return;

        if (slot1 == slot2) return;

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

    public void OnInventorySlotClick(int slotIndex)
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= 15) return;

        int blockId = playerInventory.inventory[slotIndex];
        int count = playerInventory.inventoryCounts[slotIndex];

        if (blockId == 0 || count <= 0) return;

        if (playerInventory.IsFood(blockId))
        {
            playerInventory.ConsumeItemFromInventory(slotIndex);
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
        }
        else if (playerInventory.inventory[hotbarSlot] == blockId && blockId == ammoItemId)
        {
            int space = playerInventory.maxAmmoStack - playerInventory.inventoryCounts[hotbarSlot];
            int toMove = Mathf.Min(space, count);
            if (toMove <= 0) return;

            playerInventory.inventoryCounts[hotbarSlot] += toMove;
            playerInventory.inventoryCounts[slotIndex] -= toMove;

            if (playerInventory.inventoryCounts[slotIndex] <= 0)
            {
                playerInventory.inventory[slotIndex] = 0;
                playerInventory.inventoryCounts[slotIndex] = 0;
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
        }

        playerInventory.UpdateHotbarUI();
        UpdateAllSlots();
        playerInventory.CheckSelectedSlot();
    }
}