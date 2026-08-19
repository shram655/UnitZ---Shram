using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInventory : MonoBehaviour
{
    [Header("Настройки слотов")]
    public GameObject[] blockPrefabs;
    public Sprite[] blockIcons;
    public GameObject[] cellPanels;
    public Sprite ammoIcon;

    [Header("Иконки оружий")]
    [Tooltip("Иконки оружий. weaponId=1 -> индекс 0, weaponId=2 -> индекс 1...")]
    public Sprite[] weaponIcons;
    public Sprite defaultWeaponIcon;

    [Header("🆕 Иконки холодного оружия")]
    [Tooltip("Иконки холодного оружия. meleeId=1 -> индекс 0...")]
    public Sprite[] meleeIcons;
    public Sprite defaultMeleeIcon;

    [Header("Данные инвентаря")]
    public int[] inventory = new int[20];
    public int[] inventoryCounts = new int[20];
    public int selectedSlot = 15;

    [Header("Настройки патронов")]
    public int ammoItemId = -2;
    public int maxAmmoStack = 30;

    [Header("UI Ссылки")]
    public InventoryUI inventoryUI;

    private Color[] originalSlotColors = new Color[5];
    private PlayerController playerController;

    public bool IsInventoryOpen
    {
        get
        {
            if (inventoryUI == null) return false;
            if (inventoryUI.inventoryPanel != null)
                return inventoryUI.inventoryPanel.activeSelf;
            return inventoryUI.IsOpen();
        }
    }

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        for (int i = 0; i < 20; i++)
        {
            inventory[i] = 0;
            inventoryCounts[i] = 0;
        }
        selectedSlot = 15;
        for (int i = 0; i < 5; i++)
        {
            if (cellPanels.Length > i && cellPanels[i] != null)
            {
                Image panelImg = cellPanels[i].GetComponent<Image>();
                if (panelImg != null) originalSlotColors[i] = panelImg.color;
            }
        }
    }

    void Start()
    {
        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
        SyncIconArrayWithInventory();
        CopyIconSettingsFromInventory();
        UpdateHotbarUI();
    }

    void Update()
    {
        if (playerController != null && playerController.view != null && !playerController.view.IsMine) return;
        if (playerController != null && playerController.isPlayerDead) return;

        UpdateHotbarUI();

        if (IsInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleSlotSwitching();
    }

    private void HandleSlotSwitching()
    {
        if (IsInventoryOpen) return;

        bool switched = false;

        if (Input.GetAxis("Mouse ScrollWheel") > 0f)
        {
            selectedSlot = (selectedSlot + 1) % 5 + 15;
            switched = true;
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
        {
            selectedSlot = (selectedSlot - 1 + 5) % 5 + 15;
            switched = true;
        }

        for (int i = 1; i <= 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                selectedSlot = i - 1 + 15;
                switched = true;
            }
        }

        if (switched)
        {
            Debug.Log($"🎯 Хотбар: слот переключён на {selectedSlot}");
            UpdateHotbarUI();
            CheckSelectedSlot();
        }
    }

    private void SyncIconArrayWithInventory()
    {
        if (inventoryUI == null) return;
        if (inventoryUI.itemIcons != null && inventoryUI.itemIcons.Length > 0)
        {
            blockIcons = inventoryUI.itemIcons;
        }
    }

    private void CopyIconSettingsFromInventory()
    {
        if (inventoryUI == null) return;
        if (inventoryUI.inventorySlots == null || inventoryUI.inventorySlots.Length == 0) return;

        GameObject referenceSlot = inventoryUI.inventorySlots[0];
        if (referenceSlot == null) return;

        Transform refIconTransform = referenceSlot.transform.Find("Icon");
        if (refIconTransform == null) return;

        Image refIcon = refIconTransform.GetComponent<Image>();
        if (refIcon == null) return;

        RectTransform refRect = refIcon.GetComponent<RectTransform>();

        for (int i = 0; i < cellPanels.Length; i++)
        {
            if (cellPanels[i] == null) continue;

            Transform hotbarIconTransform = cellPanels[i].transform.Find("Icon");
            if (hotbarIconTransform == null) continue;

            Image hotbarIcon = hotbarIconTransform.GetComponent<Image>();
            if (hotbarIcon == null) continue;

            hotbarIcon.color = refIcon.color;
            hotbarIcon.type = refIcon.type;
            hotbarIcon.preserveAspect = refIcon.preserveAspect;
            hotbarIcon.raycastTarget = refIcon.raycastTarget;

            RectTransform hotbarRect = hotbarIcon.GetComponent<RectTransform>();
            if (hotbarRect != null && refRect != null)
            {
                hotbarRect.anchorMin = refRect.anchorMin;
                hotbarRect.anchorMax = refRect.anchorMax;
                hotbarRect.offsetMin = refRect.offsetMin;
                hotbarRect.offsetMax = refRect.offsetMax;
                hotbarRect.pivot = refRect.pivot;
            }
        }
    }

    // ════════════════════════════════════════════════════
    // 🆕 ХОЛОДНОЕ ОРУЖИЕ (Топор)
    // ═════════════════════════════════════════════════════

    // Топор в инвентаре: itemId = -(200 + meleeId)
    // Например, топор с meleeId=1 → itemId = -201

    public const int MELEE_BASE = 200;

    //  Получить иконку топора по ID
    public Sprite GetMeleeIcon(int meleeId)
    {
        int idx = meleeId - 1;
        if (meleeIcons != null && idx >= 0 && idx < meleeIcons.Length && meleeIcons[idx] != null)
        {
            return meleeIcons[idx];
        }
        return defaultMeleeIcon;
    }

    // 🆕 Это холодное оружие?
    public bool IsMelee(int itemId) => itemId < -MELEE_BASE;

    // 🆕 Получить meleeId из itemId
    public int GetMeleeIdFromItemId(int itemId) => -(itemId + MELEE_BASE);

    //  Это огнестрел? (для блокировки строительства)
    public bool IsGun(int itemId) => itemId < -2 && itemId > -MELEE_BASE;

    // 🆕 Добавить топор в инвентарь
    public void AddMeleeToInventory(int meleeId)
    {
        int invId = -(MELEE_BASE + meleeId);

        // Сначала ищем, есть ли уже такой топор — стакаем (хотя топор обычно 1 шт)
        for (int i = 0; i < 20; i++)
        {
            if (inventory[i] == invId)
            {
                inventoryCounts[i]++;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                Debug.Log($"🪓 Топор #{meleeId} добавлен в слот {i} (x{inventoryCounts[i]})");
                return;
            }
        }

        // Ищем свободный слот
        for (int i = 0; i < 20; i++)
        {
            if (inventory[i] == 0)
            {
                inventory[i] = invId;
                inventoryCounts[i] = 1;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                Debug.Log($"🪓 Топор #{meleeId} добавлен в слот {i}");
                return;
            }
        }

        Debug.LogWarning("⚠️ Инвентарь полон, топор не добавлен!");
    }

    // ═════════════════════════════════════════════════════
    // ОРУЖИЕ (огнестрел)
    // ═════════════════════════════════════════════════════

    public Sprite GetWeaponIcon(int weaponId)
    {
        int idx = weaponId - 1;
        if (weaponIcons != null && idx >= 0 && idx < weaponIcons.Length && weaponIcons[idx] != null)
        {
            return weaponIcons[idx];
        }
        return defaultWeaponIcon;
    }

    public bool IsWeapon(int itemId) => itemId < -2 && itemId > -MELEE_BASE;

    public int GetWeaponIdFromItemId(int itemId) => -(itemId + 100);

    public void UpdateHotbarUI()
    {
        for (int i = 0; i < 5; i++)
        {
            int slotIndex = i + 15;
            if (cellPanels.Length > i && cellPanels[i] != null)
            {
                Image panelImg = cellPanels[i].GetComponent<Image>();
                if (panelImg != null)
                {
                    panelImg.color = (slotIndex == selectedSlot)
                        ? new Color(1f, 1f, 1f, 0.5f)
                        : originalSlotColors[i];
                }

                Transform iconTransform = cellPanels[i].transform.Find("Icon");
                if (iconTransform != null)
                {
                    Image iconImg = iconTransform.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        int blockId = inventory[slotIndex];

                        if (blockId > 0)
                        {
                            // Блок
                            int iconIndex = blockId - 1;
                            if (blockIcons != null && iconIndex >= 0 && iconIndex < blockIcons.Length && blockIcons[iconIndex] != null)
                            {
                                iconImg.sprite = blockIcons[iconIndex];
                                iconImg.gameObject.SetActive(true);
                            }
                            else iconImg.gameObject.SetActive(false);
                        }
                        else if (blockId == ammoItemId)
                        {
                            // Патроны
                            if (ammoIcon != null)
                            {
                                iconImg.sprite = ammoIcon;
                                iconImg.gameObject.SetActive(true);
                            }
                            else iconImg.gameObject.SetActive(false);
                        }
                        else if (IsGun(blockId))
                        {
                            // 🆕 Огнестрел — иконка по weaponId
                            int weaponId = GetWeaponIdFromItemId(blockId);
                            Sprite icon = GetWeaponIcon(weaponId);
                            if (icon != null)
                            {
                                iconImg.sprite = icon;
                                iconImg.gameObject.SetActive(true);
                            }
                            else iconImg.gameObject.SetActive(false);
                        }
                        else if (IsMelee(blockId))
                        {
                            // 🆕 Холодное оружие — иконка по meleeId
                            int meleeId = GetMeleeIdFromItemId(blockId);
                            Sprite icon = GetMeleeIcon(meleeId);
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
                    }
                }

                Transform countTransform = cellPanels[i].transform.Find("CountText");
                if (countTransform != null)
                {
                    TextMeshProUGUI tmp = countTransform.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        int count = inventoryCounts[slotIndex];
                        int blockId = inventory[slotIndex];

                        if (blockId == ammoItemId || IsGun(blockId) || IsMelee(blockId))
                        {
                            tmp.text = count > 0 ? count.ToString() : "";
                        }
                        else
                        {
                            tmp.text = count > 1 ? count.ToString() : "";
                        }
                    }
                }
            }
        }
    }

    public void AddAmmo(int amount)
    {
        int remaining = amount;
        for (int i = 0; i < 15; i++)
        {
            if (inventory[i] == ammoItemId && inventoryCounts[i] < maxAmmoStack)
            {
                int canAdd = maxAmmoStack - inventoryCounts[i];
                int toAdd = Mathf.Min(canAdd, remaining);
                inventoryCounts[i] += toAdd;
                remaining -= toAdd;
                if (remaining <= 0) break;
            }
        }

        while (remaining > 0)
        {
            int emptySlot = -1;
            for (int i = 0; i < 15; i++)
            {
                if (inventory[i] == 0) { emptySlot = i; break; }
            }
            if (emptySlot == -1) break;

            int toAdd = Mathf.Min(maxAmmoStack, remaining);
            inventory[emptySlot] = ammoItemId;
            inventoryCounts[emptySlot] = toAdd;
            remaining -= toAdd;
        }

        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();

        if (remaining > 0)
        {
            Debug.LogWarning($"⚠️ Не удалось добавить {remaining} патронов — инвентарь полон!");
        }
    }

    public int GetTotalAmmo()
    {
        int total = 0;
        for (int i = 0; i < 15; i++)
        {
            if (inventory[i] == ammoItemId) total += inventoryCounts[i];
        }
        return total;
    }

    public bool ConsumeAmmo(int amount)
    {
        int totalAmmo = GetTotalAmmo();
        if (totalAmmo < amount) return false;

        int remaining = amount;
        for (int i = 14; i >= 0; i--)
        {
            if (inventory[i] == ammoItemId && remaining > 0)
            {
                int toConsume = Mathf.Min(inventoryCounts[i], remaining);
                inventoryCounts[i] -= toConsume;
                remaining -= toConsume;

                if (inventoryCounts[i] <= 0)
                {
                    inventory[i] = 0;
                    inventoryCounts[i] = 0;
                }
            }
        }

        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
        return true;
    }

    public void AddToInventory(int blockId)
    {
        for (int i = 0; i < 15; i++)
        {
            if (inventory[i] == blockId)
            {
                inventoryCounts[i]++;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                return;
            }
        }

        for (int i = 0; i < 15; i++)
        {
            if (inventory[i] == 0)
            {
                inventory[i] = blockId;
                inventoryCounts[i] = 1;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                return;
            }
        }

        for (int i = 15; i < 20; i++)
        {
            if (inventory[i] == blockId)
            {
                inventoryCounts[i]++;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                return;
            }
        }

        for (int i = 15; i < 20; i++)
        {
            if (inventory[i] == 0)
            {
                inventory[i] = blockId;
                inventoryCounts[i] = 1;
                UpdateHotbarUI();
                if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                return;
            }
        }

        Debug.LogWarning("⚠️ Инвентарь полон!");
    }

    public void ClearInventory()
    {
        if (playerController != null && playerController.weaponManager != null)
        {
            playerController.weaponManager.UnequipCurrentWeapon();
        }

        for (int i = 0; i < 20; i++)
        {
            inventory[i] = 0;
            inventoryCounts[i] = 0;
        }

        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }

    // 🆕 ПЕРЕДЕЛАНО: экипирует оружие/топор по ID слота
    public void CheckSelectedSlot()
    {
        if (playerController == null) return;
        if (playerController.weaponManager == null) return;

        int itemId = inventory[selectedSlot];

        if (IsGun(itemId))
        {
            // Огнестрел — экипируем
            playerController.weaponManager.EquipWeaponFromSlot(selectedSlot);
        }
        else if (IsMelee(itemId))
        {
            // 🆕 Холодное оружие — экипируем топор
            playerController.weaponManager.EquipMeleeFromSlot(selectedSlot);
        }
        else
        {
            // Не оружие — снимаем текущее, если есть
            if (playerController.weaponManager.HasWeaponEquipped)
            {
                playerController.weaponManager.UnequipCurrentWeapon();
            }
        }
    }

    public bool IsFood(int blockId) => blockId == 10;

    public float GetFoodRestoreAmount(int blockId)
    {
        switch (blockId)
        {
            case 10: return 25f;
            default: return 0f;
        }
    }

    public void ConsumeItemFromInventory(int slotIndex)
    {
        int blockId = inventory[slotIndex];
        if (!IsFood(blockId)) return;

        float restoreAmount = GetFoodRestoreAmount(blockId);
        PlayerHunger hunger = GetComponent<PlayerHunger>();
        if (hunger != null) hunger.ConsumeFoodItem(blockId, restoreAmount);

        inventoryCounts[slotIndex]--;
        if (inventoryCounts[slotIndex] <= 0) inventory[slotIndex] = 0;

        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }
}