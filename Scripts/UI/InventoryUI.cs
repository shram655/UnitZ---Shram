using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance;

    [Header("Настройки")]
    public GameObject inventoryPanel;
    public GameObject[] inventorySlots;

    [Header("Иконки предметов")]
    public Sprite[] itemIcons;
    public Sprite ammoIcon;
    public int ammoItemId = -2;

    public PlayerInventory playerInventory;

    private bool isOpen = false;
    private bool isRemoteUI = false;

    void Start()
    {
        PlayerController parentPC = GetComponentInParent<PlayerController>();
        if (parentPC != null && parentPC.view != null && !parentPC.view.IsMine)
        {
            isRemoteUI = true;
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            enabled = false;
            return;
        }

        playerInventory = FindLocalPlayerInventory();

        if (playerInventory != null && Instance == null) Instance = this;

        if (playerInventory != null)
        {
            if (ammoIcon == null) ammoIcon = playerInventory.ammoIcon;
            ammoItemId = playerInventory.ammoItemId;
        }

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        SetupDragAndDrop();
    }

    PlayerInventory FindLocalPlayerInventory()
    {
        if (playerInventory != null)
        {
            PlayerController self = playerInventory.GetComponent<PlayerController>();
            if (self != null && self.view != null && self.view.IsMine) return playerInventory;
        }

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
        if (img == null)
        {
            img = slot.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }
        img.raycastTarget = true;

        ItemDragHandler handler = slot.GetComponent<ItemDragHandler>();
        if (handler == null) handler = slot.AddComponent<ItemDragHandler>();
        handler.SetSlotIndex(index);
    }

    void Update()
    {
        if (isRemoteUI) return;
        if (ChatManager.IsChatOpen) return;

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    public void ToggleInventory()
    {
        if (isRemoteUI) return;
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

    public void MoveItemBetweenSlots(int from, int to)
    {
        if (playerInventory == null) return;
        if (from == to) return;
        if (from < 0 || from >= 20 || to < 0 || to >= 20) return;

        int fromId = playerInventory.inventory[from];
        int fromCount = playerInventory.inventoryCounts[from];

        if (fromId == 0) return;

        bool fromIsWeapon = playerInventory.IsGun(fromId) || playerInventory.IsMelee(fromId);
        if (!fromIsWeapon && fromCount <= 0) return;

        int toId = playerInventory.inventory[to];
        int toCount = playerInventory.inventoryCounts[to];

        // ═══ ШАГ 1: ДО перемещения — выгрузить патроны экипированного
        // автомата в его слот (иначе они лежат в currentAmmo и потеряются)
        PlayerWeaponManager pwm = playerInventory.GetComponent<PlayerWeaponManager>();
        if (pwm != null) pwm.FlushEquippedAmmo();

        // ═══ ШАГ 2: Swap (патроны едут вместе со своим слотом)
        bool canStack = false;
        int maxStack = 1;

        if (toId == fromId && fromId > 0)
        {
            if (fromId == ammoItemId)
            {
                canStack = true;
                maxStack = playerInventory.maxAmmoStack;
            }
            else if (!playerInventory.IsGun(fromId) && !playerInventory.IsMelee(fromId))
            {
                canStack = true;
                maxStack = 99;
            }
        }

        if (canStack && toCount < maxStack)
        {
            int move = Mathf.Min(fromCount, maxStack - toCount);
            playerInventory.inventoryCounts[to] += move;
            playerInventory.inventoryCounts[from] -= move;

            if (playerInventory.inventoryCounts[from] <= 0)
            {
                playerInventory.inventory[from] = 0;
                playerInventory.inventoryCounts[from] = 0;
            }
        }
        else
        {
            playerInventory.inventory[to] = fromId;
            playerInventory.inventoryCounts[to] = fromCount;
            playerInventory.inventory[from] = toId;
            playerInventory.inventoryCounts[from] = toCount;
        }

        playerInventory.UpdateHotbarUI();
        UpdateAllSlots();

        // ═══ ШАГ 3: обновить slotIndex у экипированного (БЕЗ загрузки патронов)
        if (pwm != null) pwm.OnSlotMoved(from, to);

        // ═══ ШАГ 4: переэкипировать, если в выбранном слоте теперь другое оружие
        playerInventory.CheckSelectedSlot();
    }

    public Sprite GetIconForItem(int blockId)
    {
        if (blockId > 0)
        {
            int iconIndex = blockId - 1;
            if (itemIcons != null && iconIndex >= 0 && iconIndex < itemIcons.Length)
                return itemIcons[iconIndex];
            return null;
        }

        if (blockId == ammoItemId) return ammoIcon;

        if (playerInventory != null && playerInventory.IsGun(blockId))
            return playerInventory.GetWeaponIcon(playerInventory.GetWeaponIdFromItemId(blockId));

        if (playerInventory != null && playerInventory.IsMelee(blockId))
            return playerInventory.GetMeleeIcon(playerInventory.GetMeleeIdFromItemId(blockId));

        return null;
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

    void UpdateSlot(int slotIndex, int blockId, int count)
    {
        if (slotIndex >= inventorySlots.Length) return;

        GameObject slot = inventorySlots[slotIndex];
        if (slot == null) return;

        Transform iconTransform = slot.transform.Find("Icon");
        if (iconTransform == null) return;

        Image iconImg = iconTransform.GetComponent<Image>();
        if (iconImg == null) return;

        Sprite icon = GetIconForItem(blockId);
        if (icon != null)
        {
            iconImg.sprite = icon;
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
        MoveItemBetweenSlots(slot1, slot2);
    }

    public void OnInventorySlotClick(int slotIndex)
    {
        if (isRemoteUI) return;
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= 15) return;

        int blockId = playerInventory.inventory[slotIndex];
        int count = playerInventory.inventoryCounts[slotIndex];

        if (blockId == 0) return;

        bool isWeapon = playerInventory.IsGun(blockId) || playerInventory.IsMelee(blockId);
        if (!isWeapon && count <= 0) return;

        if (playerInventory.IsFood(blockId))
        {
            playerInventory.ConsumeItemFromInventory(slotIndex);
            UpdateAllSlots();
            return;
        }

        // Переместить в выбранный слот хотбара и сразу экипировать
        MoveItemBetweenSlots(slotIndex, playerInventory.selectedSlot);
    }
}