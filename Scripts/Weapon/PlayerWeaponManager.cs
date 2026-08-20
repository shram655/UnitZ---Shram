using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class WeaponData
{
    public int weaponId;
    public string weaponName;
    public int damage;
    public float fireRate;
    public float range;
    public float spread;
    public int maxAmmo;
    public float reloadTime;
    public float recoilAmount;
    public float recoilRecovery;
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
}

public class PlayerWeaponManager : MonoBehaviourPun
{
    [Header("Настройки оружия")]
    public float pickupRange = 3f;
    public GameObject gunPrefab;
    public GameObject meleePrefab; // 🆕 префаб топора

    [Header("Регистрация оружий")]
    public List<WeaponData> weaponRegistry = new List<WeaponData>();

    private Gun currentGun;
    private MeleeWeapon currentMelee;
    private int equippedSlotIndex = -1;
    private int equippedWeaponId = -1;

    public bool HasWeaponEquipped => (currentGun != null && currentGun.gameObject != null && currentGun.gameObject.activeSelf)
                                  || (currentMelee != null && currentMelee.gameObject != null && currentMelee.gameObject.activeSelf);
    public bool HasGunEquipped => currentGun != null && currentGun.gameObject != null && currentGun.gameObject.activeSelf;
    public bool HasMeleeEquipped => currentMelee != null && currentMelee.gameObject != null && currentMelee.gameObject.activeSelf;
    public int EquippedSlotIndex => equippedSlotIndex;

    private PlayerController playerController;
    private PlayerInventory playerInventory;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();
    }

    void Start()
    {
        if (meleePrefab == null)
        {
            Debug.LogWarning("⚠️ meleePrefab НЕ назначен в PlayerWeaponManager! Топор не будет работать.");
        }
        if (gunPrefab == null)
        {
            Debug.LogWarning("⚠️ gunPrefab НЕ назначен в PlayerWeaponManager! Огнестрел не будет работать.");
        }
    }

    void Update()
    {
        if (!photonView.IsMine || playerController.isPlayerDead || (playerInventory != null && playerInventory.IsInventoryOpen)) return;

        if (Input.GetKeyDown(KeyCode.E)) PickupWeaponFromGround();
        if (Input.GetKeyDown(KeyCode.G)) DropWeapon();
    }

    public WeaponData GetWeaponData(int weaponId)
    {
        foreach (var wd in weaponRegistry)
        {
            if (wd.weaponId == weaponId) return wd;
        }
        return null;
    }

    public void RegisterWeaponData(WeaponData data)
    {
        if (GetWeaponData(data.weaponId) != null) return;
        weaponRegistry.Add(data);
    }

    public void EquipWeaponFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;

        int itemId = playerInventory.inventory[slotIndex];

        if (!playerInventory.IsGun(itemId))
        {
            if (currentGun != null) UnequipCurrentGun();
            return;
        }

        int weaponId = -(itemId + 100);
        WeaponData data = GetWeaponData(weaponId);

        if (equippedSlotIndex == slotIndex && currentGun != null)
        {
            currentGun.Equip();
            return;
        }

        UnequipCurrentWeapon();
        CreateAndEquipGun(slotIndex, weaponId, data);
    }

    public void EquipMeleeFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;

        int itemId = playerInventory.inventory[slotIndex];

        if (!playerInventory.IsMelee(itemId))
        {
            if (currentMelee != null) UnequipCurrentMelee();
            return;
        }

        int meleeId = playerInventory.GetMeleeIdFromItemId(itemId);

        if (equippedSlotIndex == slotIndex && currentMelee != null)
        {
            currentMelee.Equip();
            return;
        }

        UnequipCurrentWeapon();
        CreateAndEquipMelee(slotIndex, meleeId);
    }

    public void UnequipCurrentWeapon()
    {
        if (currentGun != null) UnequipCurrentGun();
        if (currentMelee != null) UnequipCurrentMelee();

        CubeWorldCharacter cwChar = GetComponent<CubeWorldCharacter>();
        if (cwChar != null) cwChar.SetHasWeapon(false);

        equippedSlotIndex = -1;
        equippedWeaponId = -1;
    }

    void UnequipCurrentGun()
    {
        if (currentGun == null) return;

        Gun oldGun = currentGun;
        currentGun = null;

        oldGun.Unequip();

        if (oldGun != null && oldGun.gameObject != null)
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Destroy(oldGun.gameObject);
            else Destroy(oldGun.gameObject);
        }
    }

    void UnequipCurrentMelee()
    {
        if (currentMelee == null) return;

        MeleeWeapon oldMelee = currentMelee;
        currentMelee = null;

        oldMelee.Unequip();

        if (oldMelee != null && oldMelee.gameObject != null)
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Destroy(oldMelee.gameObject);
            else Destroy(oldMelee.gameObject);
        }
    }

    void CreateAndEquipGun(int slotIndex, int weaponId, WeaponData data)
    {
        if (gunPrefab == null)
        {
            Debug.LogError("❌ gunPrefab не назначен!");
            return;
        }

        CubeWorldCharacter cwChar = GetComponent<CubeWorldCharacter>();
        if (cwChar == null || cwChar.WeaponAnchor == null)
        {
            Debug.LogError("❌ CubeWorldCharacter или WeaponAnchor не найден");
            return;
        }

        GameObject gunObj = PhotonNetwork.Instantiate(gunPrefab.name, cwChar.WeaponAnchor.position, cwChar.WeaponAnchor.rotation);
        gunObj.transform.SetParent(cwChar.WeaponAnchor);
        gunObj.transform.localPosition = Vector3.zero;
        gunObj.transform.localRotation = Quaternion.identity;
        // 🆕 НЕ трогаем scale — оставляем размер из префаба
        // gunObj.transform.localScale = Vector3.one;

        currentGun = gunObj.GetComponent<Gun>();
        if (currentGun == null)
        {
            Debug.LogError("❌ Gun компонент не найден на префабе!");
            PhotonNetwork.Destroy(gunObj);
            return;
        }

        if (data != null) ApplyWeaponData(currentGun, data);

        currentGun.fpsCam = playerController.playerCamera;
        currentGun.playerInventory = playerInventory;
        currentGun.weaponId = weaponId;
        currentGun.slotIndex = slotIndex;

        Transform barrelEnd = gunObj.transform.Find("BarrelEnd");
        if (barrelEnd == null)
        {
            GameObject barrelObj = new GameObject("BarrelEnd");
            barrelObj.transform.SetParent(gunObj.transform);
            barrelObj.transform.localPosition = new Vector3(0, 0, 0.5f);
            barrelEnd = barrelObj.transform;
        }
        currentGun.barrelEnd = barrelEnd;

        cwChar.SetHasWeapon(true);

        equippedSlotIndex = slotIndex;
        equippedWeaponId = weaponId;

        StartCoroutine(DelayedEquipGun(currentGun));
    }

    void CreateAndEquipMelee(int slotIndex, int meleeId)
    {
        if (meleePrefab == null)
        {
            Debug.LogError(" meleePrefab НЕ назначен в PlayerWeaponManager! Перетащи префаб топора в Inspector.");
            return;
        }

        CubeWorldCharacter cwChar = GetComponent<CubeWorldCharacter>();
        if (cwChar == null)
        {
            Debug.LogError("❌ CubeWorldCharacter не найден на игроке!");
            return;
        }
        if (cwChar.WeaponAnchor == null)
        {
            Debug.LogError("❌ WeaponAnchor не найден в CubeWorldCharacter!");
            return;
        }

        Debug.Log($"🪓 Создаём топор: meleeId={meleeId}, slotIndex={slotIndex}");

        GameObject meleeObj = PhotonNetwork.Instantiate(meleePrefab.name, cwChar.WeaponAnchor.position, cwChar.WeaponAnchor.rotation);
        meleeObj.transform.SetParent(cwChar.WeaponAnchor);
        meleeObj.transform.localPosition = Vector3.zero;
        meleeObj.transform.localRotation = Quaternion.identity;
        //  НЕ трогаем scale — топор сохраняет свой размер из префаба!
        // meleeObj.transform.localScale = Vector3.one;

        currentMelee = meleeObj.GetComponent<MeleeWeapon>();
        if (currentMelee == null)
        {
            Debug.LogError(" MeleeWeapon компонент не найден на префабе топора!");
            PhotonNetwork.Destroy(meleeObj);
            return;
        }

        Debug.Log($"✅ Топор создан: {currentMelee.meleeName}");

        currentMelee.fpsCam = playerController.playerCamera;
        currentMelee.playerInventory = playerInventory;
        currentMelee.meleeId = meleeId;
        currentMelee.slotIndex = slotIndex;

        cwChar.SetHasWeapon(true);

        equippedSlotIndex = slotIndex;
        equippedWeaponId = meleeId;

        StartCoroutine(DelayedEquipMelee(currentMelee));
    }

    private void ApplyWeaponData(Gun gun, WeaponData data)
    {
        gun.weaponName = string.IsNullOrEmpty(data.weaponName) ? "Оружие" : data.weaponName;
        gun.damage = data.damage > 0 ? data.damage : 25f;
        gun.fireRate = data.fireRate > 0.01f ? data.fireRate : 0.1f;
        gun.range = data.range > 0 ? data.range : 100f;
        gun.spread = data.spread > 0 ? data.spread : 0.02f;
        gun.maxAmmo = data.maxAmmo > 0 ? data.maxAmmo : 30;
        gun.reloadTime = data.reloadTime > 0 ? data.reloadTime : 2f;
        gun.recoilAmount = data.recoilAmount > 0 ? data.recoilAmount : 0.5f;
        gun.recoilRecovery = data.recoilRecovery > 0 ? data.recoilRecovery : 5f;

        if (data.muzzleFlash != null) gun.muzzleFlash = data.muzzleFlash;
        if (data.impactEffect != null) gun.impactEffect = data.impactEffect;
        if (data.shootSound != null) gun.shootSound = data.shootSound;
        if (data.reloadSound != null) gun.reloadSound = data.reloadSound;
        if (data.emptySound != null) gun.emptySound = data.emptySound;
    }

    IEnumerator DelayedEquipGun(Gun gun)
    {
        yield return new WaitForSeconds(0.2f);
        if (gun != null) gun.Equip();
    }

    IEnumerator DelayedEquipMelee(MeleeWeapon melee)
    {
        yield return new WaitForSeconds(0.2f);
        if (melee != null) melee.Equip();
    }

    public void AddWeaponToInventory(int weaponId, WeaponData data)
    {
        if (data == null) return;

        RegisterWeaponData(data);

        int freeSlot = -1;
        for (int i = 0; i < 15; i++) { if (playerInventory.inventory[i] == 0) { freeSlot = i; break; } }
        if (freeSlot == -1) { for (int i = 15; i < 20; i++) { if (playerInventory.inventory[i] == 0) { freeSlot = i; break; } } }
        if (freeSlot == -1) { Debug.LogWarning("⚠️ Нет места для оружия!"); return; }

        int inventoryId = -(100 + weaponId);
        playerInventory.inventory[freeSlot] = inventoryId;
        playerInventory.inventoryCounts[freeSlot] = data.maxAmmo;

        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();

        Debug.Log($"🔫 Добавлено оружие #{weaponId} ({data.weaponName}) в слот {freeSlot}");
    }

    void PickupWeaponFromGround()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            PickupWeapon pickup = col.GetComponent<PickupWeapon>();
            if (pickup != null)
            {
                pickup.PickUp(gameObject);
                return;
            }
        }
    }

    void DropWeapon()
    {
        if (equippedSlotIndex < 0)
        {
            Debug.Log("⚠️ DropWeapon: нет экипированного оружия");
            return;
        }

        if (equippedSlotIndex >= playerInventory.inventory.Length)
        {
            Debug.LogError("❌ DropWeapon: некорректный equippedSlotIndex");
            return;
        }

        int slotToClear = equippedSlotIndex;
        int itemId = playerInventory.inventory[slotToClear];

        if (playerInventory.IsGun(itemId))
        {
            int weaponId = -(itemId + 100);
            WeaponData data = GetWeaponData(weaponId);
            int magAmmo = playerInventory.inventoryCounts[slotToClear];

            if (currentGun != null)
            {
                if (PhotonNetwork.IsConnected) PhotonNetwork.Destroy(currentGun.gameObject);
                else Destroy(currentGun.gameObject);
                currentGun = null;
            }

            Vector3 dropPosition = transform.position + transform.forward * 3f;
            dropPosition.y = transform.position.y + 2f;

            PickupWeapon.DropWeapon(dropPosition, Quaternion.identity,
                data != null ? data.weaponName : "Оружие",
                data != null ? data.damage : 25,
                data != null ? data.fireRate : 0.1f,
                data != null ? data.range : 100f,
                data != null ? data.spread : 0.02f,
                data != null ? data.maxAmmo : 30,
                data != null ? data.reloadTime : 2f,
                data != null ? data.recoilAmount : 0.5f,
                data != null ? data.recoilRecovery : 5f,
                data != null ? data.muzzleFlash : null,
                data != null ? data.impactEffect : null,
                data != null ? data.shootSound : null,
                data != null ? data.reloadSound : null,
                data != null ? data.emptySound : null,
                null,
                weaponId,
                magAmmo);

            playerInventory.inventory[slotToClear] = 0;
            playerInventory.inventoryCounts[slotToClear] = 0;
        }
        else if (playerInventory.IsMelee(itemId))
        {
            if (currentMelee != null)
            {
                if (PhotonNetwork.IsConnected) PhotonNetwork.Destroy(currentMelee.gameObject);
                else Destroy(currentMelee.gameObject);
                currentMelee = null;
            }
            Debug.Log(" Топор снят");
        }

        CubeWorldCharacter cwChar = GetComponent<CubeWorldCharacter>();
        if (cwChar != null) cwChar.SetHasWeapon(false);

        equippedSlotIndex = -1;
        equippedWeaponId = -1;

        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();

        Debug.Log($"✅ Оружие выброшено, слот {slotToClear} очищен");
    }
}