using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInventory : MonoBehaviour
{
    [Header("Настройки слотов")]
    public GameObject[] blockPrefabs;
    public Sprite[] blockIcons;
    public GameObject[] cellPanels;
    public Sprite weaponIcon;

    [Header("Данные инвентаря")]
    public int[] inventory = new int[20];
    public int[] inventoryCounts = new int[20];
    public int selectedSlot = 15;

    [Header("UI Ссылки")]
    public InventoryUI inventoryUI;

    private Color[] originalSlotColors = new Color[5];
    private PlayerController playerController;

    // ══════════════════════════════════════════════════════
    //  НАДЁЖНАЯ ПРОВЕРКА: открыт ли инвентарь НА САМОМ ДЕЛЕ
    //  (смотрим на реальную панель, а не на застрявший флаг)
    // ══════════════════════════════════════════════════════
    public bool IsInventoryOpen
    {
        get
        {
            if (inventoryUI == null) return false;

            // Если есть ссылка на панель — проверяем её реальную видимость
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
        // ── Защищённые проверки (ничего не упадёт при null) ──
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

    // ══════════════════════════════════════════════════════
    //  ПЕРЕКЛЮЧЕНИЕ СЛОТОВ (колесо + цифры 1-5)
    // ══════════════════════════════════════════════════════
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

    // ══════════════════════════════════════════════════════
    //  СИНХРОНИЗАЦИЯ ИКОНОК С ИНВЕНТАРЁМ
    // ══════════════════════════════════════════════════════
    private void SyncIconArrayWithInventory()
    {
        if (inventoryUI == null) return;

        if (inventoryUI.itemIcons != null && inventoryUI.itemIcons.Length > 0)
        {
            blockIcons = inventoryUI.itemIcons;
        }

        if (inventoryUI.weaponIcon != null)
        {
            weaponIcon = inventoryUI.weaponIcon;
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

    // ══════════════════════════════════════════════════════
    //  ОБНОВЛЕНИЕ ХОТБАРА
    // ══════════════════════════════════════════════════════
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
                            int iconIndex = blockId - 1;
                            if (blockIcons != null && iconIndex >= 0 && iconIndex < blockIcons.Length && blockIcons[iconIndex] != null)
                            {
                                iconImg.sprite = blockIcons[iconIndex];
                                iconImg.gameObject.SetActive(true);
                            }
                            else iconImg.gameObject.SetActive(false);
                        }
                        else if (blockId < 0 && weaponIcon != null)
                        {
                            iconImg.sprite = weaponIcon;
                            iconImg.gameObject.SetActive(true);
                        }
                        else iconImg.gameObject.SetActive(false);
                    }
                }

                Transform countTransform = cellPanels[i].transform.Find("CountText");
                if (countTransform != null)
                {
                    TextMeshProUGUI tmp = countTransform.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        int count = inventoryCounts[slotIndex];
                        tmp.text = count > 1 ? count.ToString() : "";
                    }
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  ДОБАВЛЕНИЕ / ОЧИСТКА
    // ══════════════════════════════════════════════════════
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
        for (int i = 0; i < 20; i++)
        {
            inventory[i] = 0;
            inventoryCounts[i] = 0;
        }
        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
    }

    // ══════════════════════════════════════════════════════
    //  ПРОВЕРКА СЛОТА (оружие) — ТЕПЕРЬ БЕЗОПАСНАЯ
    // ══════════════════════════════════════════════════════
    public void CheckSelectedSlot()
    {
        if (playerController == null) return;
        if (playerController.weaponManager == null) return;

        if (inventory[selectedSlot] < 0)
        {
            playerController.weaponManager.EquipWeaponFromSlot();
        }
        else if (playerController.weaponManager.HasWeaponEquipped)
        {
            playerController.weaponManager.UnequipCurrentWeapon();
        }
    }

    // ══════════════════════════════════════════════════════
    //  ЕДА
    // ══════════════════════════════════════════════════════
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