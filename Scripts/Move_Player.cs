using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class Move_Player : MonoBehaviourPunCallbacks
{
    public PhotonView view;
    public TextMeshPro nick;

    [Header("Управление камерой")]
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;
    public Camera cam;

    [Header("Прыжок")]
    public float jumpSpeed = 8.0F;
    public float gravity = 20.0F;
    private Vector3 moveDirection = Vector3.zero;
    public Vector3 playerScale;

    [Header("Скорость перемещения персонажа")]
    public float speed = 7f;

    [Header("Спавн и дестрой объектов")]
    public GameObject aim;
    Ray spawnRay;
    RaycastHit hit;

    [Header("Инвентарь (20 слотов: 0-14 основной, 15-19 хотбар)")]
    public GameObject[] blockPrefabs;
    public Sprite[] blockIcons;
    public GameObject[] cellPanels;
    public int[] inventory = new int[20];
    public int[] inventoryCounts = new int[20];
    public int selectedSlot = 15;
    public GameObject Torch;
    public GameObject Canvas;
    public InventoryUI inventoryUI;

    // ? Цвета слотов хотбара из инспектора
    private Color[] originalSlotColors = new Color[5];

    [Header("Оружие")]
    public Transform weaponSlot;
    public Transform thirdPersonWeaponSlot;
    public float pickupRange = 3f;
    public GameObject gunPrefab;
    public Sprite weaponIcon;

    public string weaponName;
    public int weaponDamage;
    public float weaponFireRate;
    public float weaponRange;
    public float weaponSpread;
    public int weaponMaxAmmo;
    public float weaponReloadTime;
    public float weaponRecoilAmount;
    public float weaponRecoilRecovery;
    public ParticleSystem weaponMuzzleFlash;
    public GameObject weaponImpactEffect;
    public AudioClip weaponShootSound;
    public AudioClip weaponReloadSound;
    public AudioClip weaponEmptySound;

    private Gun currentGun;
    private Gun thirdPersonGun;
    private bool hasWeaponEquipped = false;

    [Header("Звуки")]
    public AudioClip[] sound;
    public AudioSource music;

    private bool isPlayerDead = false;
    private WorldManager worldManager;

    void Start()
    {
        view = GetComponent<PhotonView>();
        worldManager = FindObjectOfType<WorldManager>();
        if (worldManager == null)
        {
            Debug.LogError("? WorldManager не найден в сцене!");
        }

        string nickname = PhotonNetwork.NickName;
        if (string.IsNullOrEmpty(nickname))
        {
            nickname = "Игрок";
        }
        if (nick != null)
        {
            nick.text = nickname;
        }

        for (int i = 0; i < 20; i++)
        {
            inventory[i] = 0;
            inventoryCounts[i] = 0;
        }
        selectedSlot = 15;

        // ? Запоминаем цвета слотов хотбара из инспектора
        for (int i = 0; i < 5; i++)
        {
            if (cellPanels.Length > i && cellPanels[i] != null)
            {
                Image panelImg = cellPanels[i].GetComponent<Image>();
                if (panelImg != null)
                {
                    originalSlotColors[i] = panelImg.color;
                }
            }
        }

        UpdateHotbarUI();

        if (inventoryUI == null)
        {
            Debug.LogWarning("?? inventoryUI не назначен! Ищем автоматически...");
            inventoryUI = FindObjectOfType<InventoryUI>();
        }
        if (inventoryUI != null)
        {
            Debug.Log("? inventoryUI найден: " + inventoryUI.gameObject.name);
        }
        else
        {
            Debug.LogError("? inventoryUI НЕ НАЙДЕН!");
        }

        if (!view.IsMine)
        {
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
                AudioListener audioListener = cam.GetComponent<AudioListener>();
                if (audioListener != null) audioListener.enabled = false;
            }
            if (Canvas != null) Canvas.SetActive(false);
            if (Torch != null) Torch.SetActive(false);
            enabled = false;
            Debug.Log("?? Это чужой игрок: " + nickname + ". Управление отключено.");
            return;
        }

        Debug.Log("? Мой игрок инициализирован. Ник: " + nickname);
        if (weaponSlot == null)
            Debug.LogError("[ОШИБКА] WeaponSlot не назначен!");
        if (gunPrefab == null)
            Debug.LogError("[ОШИБКА] Gun Prefab не назначен!");
    }

    void Update()
    {
        if (!view.IsMine) return;
        if (isPlayerDead) return;
        if (selectedSlot < 15 || selectedSlot > 19)
        {
            selectedSlot = 15;
        }
        cam.gameObject.SetActive(true);
        Canvas.gameObject.SetActive(true);

        // ? Хотбар обновляется ВСЕГДА (даже при открытом инвентаре)
        UpdateHotbarUI();

        if (inventoryUI != null && inventoryUI.IsOpen())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair != null) crosshair.SetCrosshairActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        CrosshairController crosshairShow = FindObjectOfType<CrosshairController>();
        if (crosshairShow != null) crosshairShow.SetCrosshairActive(true);

        Move();
        Shift();
        SpawnObject();
        DestroyObject();
        Inventory();
        TorchControl();
        CheckWeaponInput();
        esc_menu();
    }

    public void AddBlockToInventory(int blockId)
    {
        AddToInventory(blockId);
    }

    public void SetDead(bool dead)
    {
        isPlayerDead = dead;
        Debug.Log("?? Статус смерти изменён: " + dead);
        if (dead && hasWeaponEquipped)
        {
            UnequipCurrentWeapon();
            Debug.Log("?? Оружие снято при смерти");
        }
    }

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
                    // ? Выбранный — полупрозрачный белый, остальные — ТВОИ цвета из инспектора
                    panelImg.color = (slotIndex == selectedSlot) ? new Color(1f, 1f, 1f, 0.5f) : originalSlotColors[i];
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
                    }
                }
            }
        }
    }

    public void SpawnObject()
    {
        if (inventory[selectedSlot] < 0) return;
        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            int currentBlockId = inventory[selectedSlot];
            if (currentBlockId == 0 || currentBlockId > blockPrefabs.Length) return;
            spawnRay = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(spawnRay, out hit, 10.0f))
            {
                Vector3 spawnPosition = hit.point;
                bool placed = false;
                if (hit.transform.tag == "floor")
                { spawnPosition = hit.point; placed = true; }
                else if (hit.transform.gameObject.name == "Trigger Y")
                { spawnPosition = hit.transform.position + new Vector3(0, 0.459f, 0); placed = true; }
                else if (hit.transform.gameObject.name == "Trigger -Y ")
                { spawnPosition = hit.transform.position + new Vector3(0, -0.459f, 0); placed = true; }
                else if (hit.transform.gameObject.name == "Trigger X")
                { spawnPosition = hit.transform.position + new Vector3(0.459f, 0, 0); placed = true; }
                else if (hit.transform.gameObject.name == "Trigger -X")
                { spawnPosition = hit.transform.position + new Vector3(-0.459f, 0, 0); placed = true; }
                else if (hit.transform.gameObject.name == "Trigger Z")
                { spawnPosition = hit.transform.position + new Vector3(0, 0, 0.459f); placed = true; }
                else if (hit.transform.gameObject.name == "Trigger -Z")
                { spawnPosition = hit.transform.position + new Vector3(0, 0, -0.459f); placed = true; }
                if (placed && worldManager != null)
                {
                    worldManager.PlaceBlock(currentBlockId, spawnPosition);
                    if (view != null && PhotonNetwork.IsConnected)
                    {
                        view.RPC("RPC_BlockPlaced", RpcTarget.All, currentBlockId,
                            Mathf.RoundToInt(spawnPosition.x * 100),
                            Mathf.RoundToInt(spawnPosition.y * 100),
                            Mathf.RoundToInt(spawnPosition.z * 100));
                    }
                    music.clip = sound[0];
                    music.Play();
                    inventoryCounts[selectedSlot]--;
                    if (inventoryCounts[selectedSlot] <= 0) inventory[selectedSlot] = 0;
                    UpdateHotbarUI();
                    if (inventoryUI != null) inventoryUI.UpdateAllSlots();
                }
            }
        }
    }

    public void DestroyObject()
    {
        if (Input.GetKeyUp(KeyCode.Mouse1))
        {
            spawnRay = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(spawnRay, out hit, 10.0f))
            {
                Debug.Log($"?? Raycast попал в: {hit.transform.name} (тег: {hit.transform.tag})");
                GameObject blockRoot = hit.transform.gameObject;
                while (blockRoot.transform.parent != null &&
                       blockRoot.transform.parent.name != "WorldBlocks")
                {
                    blockRoot = blockRoot.transform.parent.gameObject;
                }
                Debug.Log($"?? Корневой объект: {blockRoot.name} (позиция: {blockRoot.transform.position})");
                BlockDestroyer blockDestroyer = blockRoot.GetComponent<BlockDestroyer>();
                if (blockDestroyer != null)
                {
                    Debug.Log($"?? Это блок с лутом ? вызываем BlockDestroyer.RequestDestroy()");
                    blockDestroyer.RequestDestroy(this);
                    music.clip = sound[1];
                    music.Play();
                    return;
                }
                PlacedBlock placedBlock = blockRoot.GetComponent<PlacedBlock>();
                if (placedBlock != null)
                {
                    Debug.Log($"?? Это обычный блок ? добавляем в инвентарь и уничтожаем");
                    AddToInventory(placedBlock.blockId);
                    if (worldManager != null)
                    {
                        worldManager.DestroyBlock(blockRoot.transform.position, PhotonNetwork.NickName);
                    }
                    if (view != null && PhotonNetwork.IsConnected)
                    {
                        int x = Mathf.RoundToInt(blockRoot.transform.position.x * 100);
                        int y = Mathf.RoundToInt(blockRoot.transform.position.y * 100);
                        int z = Mathf.RoundToInt(blockRoot.transform.position.z * 100);
                        Debug.Log($"?? Отправляем RPC_BlockDestroyed всем через свой PhotonView");
                        view.RPC("RPC_BlockDestroyed", RpcTarget.All, x, y, z);
                    }
                    Destroy(blockRoot);
                    music.clip = sound[1];
                    music.Play();
                    return;
                }
                Debug.Log($"?? Неизвестный тип блока: {blockRoot.name}");
            }
            else
            {
                Debug.Log("? Raycast ни во что не попал");
            }
        }
    }

    [PunRPC]
    void RPC_BlockDestroyed(int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        Debug.Log($"?? RPC_BlockDestroyed получен! Позиция: {position}");
        if (worldManager != null)
        {
            worldManager.HandleBlockDestroyed(position);
        }
    }

    [PunRPC]
    void RPC_BlockPlaced(int blockId, int x, int y, int z)
    {
        Vector3 position = new Vector3(x / 100f, y / 100f, z / 100f);
        Debug.Log($"?? RPC_BlockPlaced получен! ID={blockId}, Позиция: {position}");
        if (worldManager != null)
        {
            worldManager.HandleBlockPlaced(blockId, position);
        }
    }

    void AddToInventory(int blockId)
    {
        Debug.Log($"?? [ЛОКАЛЬНО] AddToInventory вызван для блока ID={blockId} (игрок: {nick.text})");
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
        Debug.Log("?? Инвентарь полон!");
    }

    public void Inventory()
    {
        if (Input.GetAxis("Mouse ScrollWheel") > 0f)
        {
            selectedSlot = (selectedSlot + 1) % 5 + 15;
            UpdateHotbarUI();
            CheckSelectedSlot();
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
        {
            selectedSlot = (selectedSlot - 1 + 5) % 5 + 15;
            UpdateHotbarUI();
            CheckSelectedSlot();
        }
        for (int i = 1; i <= 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                selectedSlot = i - 1 + 15;
                UpdateHotbarUI();
                CheckSelectedSlot();
            }
        }
    }

    // ? СДЕЛАНО PUBLIC — теперь инвентарь может вызывать эту проверку
    public void CheckSelectedSlot()
    {
        if (inventory[selectedSlot] < 0)
        {
            EquipWeaponFromSlot();
        }
        else if (hasWeaponEquipped)
        {
            UnequipCurrentWeapon();
        }
    }

    void TorchControl()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Torch.SetActive(!Torch.activeSelf);
        }
    }

    void CheckWeaponInput()
    {
        if (Input.GetKeyDown(KeyCode.E)) PickupWeaponFromGround();
        if (Input.GetKeyDown(KeyCode.G)) DropWeapon();
    }

    public void AddWeaponToInventory(
        string name, int damage, float fireRate, float range, float spread, int maxAmmo, float reloadTime,
        float recoilAmount, float recoilRecovery, ParticleSystem muzzleFlash, GameObject impactEffect,
        AudioClip shootSound, AudioClip reloadSound, AudioClip emptySound)
    {
        Debug.Log("? Оружие добавлено: " + name);
        weaponName = name; weaponDamage = damage; weaponFireRate = fireRate; weaponRange = range; weaponSpread = spread;
        weaponMaxAmmo = maxAmmo; weaponReloadTime = reloadTime; weaponRecoilAmount = recoilAmount; weaponRecoilRecovery = recoilRecovery;
        weaponMuzzleFlash = muzzleFlash; weaponImpactEffect = impactEffect; weaponShootSound = shootSound; weaponReloadSound = reloadSound; weaponEmptySound = emptySound;
        int freeSlot = -1;
        for (int i = 0; i < 15; i++) { if (inventory[i] == 0) { freeSlot = i; break; } }
        if (freeSlot == -1) { for (int i = 15; i < 20; i++) { if (inventory[i] == 0) { freeSlot = i; break; } } }
        if (freeSlot == -1)
        {
            Debug.LogWarning("?? Инвентарь полон!");
            return;
        }
        inventory[freeSlot] = -1;
        inventoryCounts[freeSlot] = 1;
        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
        Debug.Log("? Оружие добавлено в слот " + freeSlot);
    }

    void EquipWeaponFromSlot() { if (hasWeaponEquipped) return; CreateAndEquipWeapon(); }

    void UnequipCurrentWeapon()
    {
        if (!hasWeaponEquipped) return;
        if (currentGun != null)
        {
            currentGun.Unequip();
            PhotonNetwork.Destroy(currentGun.gameObject);
            currentGun = null;
        }
        if (thirdPersonGun != null)
        {
            PhotonNetwork.Destroy(thirdPersonGun.gameObject);
            thirdPersonGun = null;
        }
        hasWeaponEquipped = false;
        Debug.Log("?? Оружие снято и уничтожено");
    }

    void CreateAndEquipWeapon()
    {
        if (weaponSlot == null || gunPrefab == null) return;
        if (currentGun != null) { currentGun.Equip(); hasWeaponEquipped = true; return; }
        GameObject gunObj = PhotonNetwork.Instantiate(gunPrefab.name, weaponSlot.position, weaponSlot.rotation);
        gunObj.transform.SetParent(weaponSlot);
        gunObj.transform.localPosition = new Vector3(0.3f, -0.3f, 0.5f);
        gunObj.transform.localRotation = Quaternion.identity;
        gunObj.transform.localScale = Vector3.one;
        currentGun = gunObj.GetComponent<Gun>();
        if (currentGun == null) { PhotonNetwork.Destroy(gunObj); return; }
        currentGun.weaponName = weaponName; currentGun.damage = weaponDamage; currentGun.fireRate = weaponFireRate; currentGun.range = weaponRange; currentGun.spread = weaponSpread;
        currentGun.maxAmmo = weaponMaxAmmo; currentGun.currentAmmo = weaponMaxAmmo; currentGun.reloadTime = weaponReloadTime; currentGun.recoilAmount = weaponRecoilAmount; currentGun.recoilRecovery = weaponRecoilRecovery;
        currentGun.muzzleFlash = weaponMuzzleFlash; currentGun.impactEffect = weaponImpactEffect; currentGun.shootSound = weaponShootSound; currentGun.reloadSound = weaponReloadSound; currentGun.emptySound = weaponEmptySound;
        currentGun.fpsCam = cam;
        Transform barrelEnd = gunObj.transform.Find("BarrelEnd");
        if (barrelEnd == null)
        {
            GameObject barrelObj = new GameObject("BarrelEnd");
            barrelObj.transform.SetParent(gunObj.transform);
            barrelObj.transform.localPosition = new Vector3(0, 0, 0.5f);
            barrelEnd = barrelObj.transform;
        }
        currentGun.barrelEnd = barrelEnd;
        if (thirdPersonWeaponSlot != null)
        {
            GameObject thirdPersonGunObj = PhotonNetwork.Instantiate(gunPrefab.name, thirdPersonWeaponSlot.position, thirdPersonWeaponSlot.rotation);
            thirdPersonGunObj.transform.SetParent(thirdPersonWeaponSlot);
            thirdPersonGunObj.transform.localPosition = Vector3.zero;
            thirdPersonGunObj.transform.localRotation = Quaternion.identity;
            thirdPersonGunObj.transform.localScale = Vector3.one;
            thirdPersonGun = thirdPersonGunObj.GetComponent<Gun>();
            if (thirdPersonGun != null)
            {
                thirdPersonGun.weaponName = weaponName;
                thirdPersonGun.damage = weaponDamage;
                thirdPersonGun.fireRate = weaponFireRate;
                thirdPersonGun.range = weaponRange;
                thirdPersonGun.spread = weaponSpread;
                thirdPersonGun.maxAmmo = weaponMaxAmmo;
                thirdPersonGun.currentAmmo = weaponMaxAmmo;
                thirdPersonGun.reloadTime = weaponReloadTime;
                thirdPersonGun.recoilAmount = weaponRecoilAmount;
                thirdPersonGun.recoilRecovery = weaponRecoilRecovery;
                thirdPersonGun.muzzleFlash = weaponMuzzleFlash;
                thirdPersonGun.impactEffect = weaponImpactEffect;
                thirdPersonGun.shootSound = weaponShootSound;
                thirdPersonGun.reloadSound = weaponReloadSound;
                thirdPersonGun.emptySound = weaponEmptySound;
                if (thirdPersonGun.fpsCam != null) thirdPersonGun.fpsCam = null;
            }
            Debug.Log("?? Оружие третьего лица создано");
        }
        StartCoroutine(DelayedEquip(currentGun));
    }

    IEnumerator DelayedEquip(Gun gun)
    {
        yield return new WaitForSeconds(0.2f);
        gun.Equip();
        hasWeaponEquipped = true;
    }

    void DropWeapon()
    {
        if (inventory[selectedSlot] >= 0)
        {
            Debug.Log("?? В текущем слоте нет оружия!");
            return;
        }
        if (currentGun != null) { currentGun.Unequip(); PhotonNetwork.Destroy(currentGun.gameObject); currentGun = null; }
        if (thirdPersonGun != null) { PhotonNetwork.Destroy(thirdPersonGun.gameObject); thirdPersonGun = null; }
        hasWeaponEquipped = false;
        string name = weaponName;
        int damage = weaponDamage;
        float fireRate = weaponFireRate;
        float range = weaponRange;
        float spread = weaponSpread;
        int maxAmmo = weaponMaxAmmo;
        float reloadTime = weaponReloadTime;
        float recoilAmount = weaponRecoilAmount;
        float recoilRecovery = weaponRecoilRecovery;
        ParticleSystem muzzleFlash = weaponMuzzleFlash;
        GameObject impactEffect = weaponImpactEffect;
        AudioClip shootSound = weaponShootSound;
        AudioClip reloadSound = weaponReloadSound;
        AudioClip emptySound = weaponEmptySound;
        inventory[selectedSlot] = 0;
        inventoryCounts[selectedSlot] = 0;
        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
        Vector3 dropPosition = transform.position + transform.forward * 3f;
        dropPosition.y = transform.position.y + 2f;
        PickupWeapon.DropWeapon(
            dropPosition, Quaternion.identity, name, damage, fireRate, range, spread,
            maxAmmo, reloadTime, recoilAmount, recoilRecovery, muzzleFlash, impactEffect,
            shootSound, reloadSound, emptySound, null
        );
    }

    void PickupWeaponFromGround()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            PickupWeapon pickup = col.GetComponent<PickupWeapon>();
            if (pickup != null) { pickup.PickUp(gameObject); return; }
        }
    }

    public void esc_menu()
    {
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.Confined;
            PhotonNetwork.Disconnect();
            PhotonNetwork.LoadLevel("LobbyScene");
        }
    }

    private void Move()
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller.isGrounded)
        {
            moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            moveDirection = transform.TransformDirection(moveDirection);
            moveDirection *= speed;
            if (Input.GetKey(KeyCode.W)) transform.localPosition += transform.forward * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.S)) transform.localPosition += -transform.forward * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.A)) transform.localPosition += -transform.right * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.D)) transform.localPosition += transform.right * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.Space)) moveDirection.y = jumpSpeed;
        }
        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }

    private void Shift()
    {
        speed = Input.GetKey(KeyCode.LeftShift) ? 15f : 7f;
    }

    public bool IsFood(int blockId)
    {
        return blockId == 10;
    }

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
        if (!IsFood(blockId))
        {
            Debug.Log("?? Этот предмет нельзя съесть!");
            return;
        }
        float restoreAmount = GetFoodRestoreAmount(blockId);
        PlayerHunger hunger = GetComponent<PlayerHunger>();
        if (hunger != null)
        {
            hunger.ConsumeFoodItem(blockId, restoreAmount);
        }
        inventoryCounts[slotIndex]--;
        if (inventoryCounts[slotIndex] <= 0)
        {
            inventory[slotIndex] = 0;
        }
        UpdateHotbarUI();
        if (inventoryUI != null) inventoryUI.UpdateAllSlots();
        Debug.Log("?? Съеден предмет из слота " + slotIndex);
    }
}