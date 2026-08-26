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

[System.Serializable]
public class WeaponSlotPose
{
    public int weaponId;
    public string weaponName = "Оружие";
    
    [Header("🎯 FPS (вид от первого лица, у камеры)")]
    public Vector3 fpsPosition = new Vector3(0.25f, -0.25f, 0.5f);
    public Vector3 fpsRotation = Vector3.zero;
    
    [Header("🖐 3-е лицо (в кисти, для других игроков)")]
    public Vector3 handPosition = Vector3.zero;
    public Vector3 handRotation = Vector3.zero;
    public Vector3 handScale = Vector3.one;
}

public class PlayerWeaponManager : MonoBehaviourPun
{
    [Header("Настройки")]
    public float pickupRange = 3f;
    public GameObject gunPrefab;
    public GameObject meleePrefab;

    [Header("Регистрация")]
    public List<WeaponData> weaponRegistry = new List<WeaponData>();

    [Header("═══ 🎯 ПОЗИЦИИ ОРУЖИЯ (настраивай ЗДЕСЬ!) ═══")]
    [Tooltip("У каждого оружия СВОЯ позиция. Добавь запись для каждого weaponId")]
    public List<WeaponSlotPose> weaponPoses = new List<WeaponSlotPose>();

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
        if (meleePrefab == null) Debug.LogWarning("⚠️ meleePrefab не назначен");
        if (gunPrefab == null) Debug.LogWarning("⚠️ gunPrefab не назначен");
    }

    public WeaponSlotPose GetPose(int weaponId)
    {
        foreach (var p in weaponPoses)
            if (p.weaponId == weaponId) return p;
        return null;
    }

    public void ApplyFpsPose(GameObject weapon, int weaponId)
    {
        WeaponSlotPose pose = GetPose(weaponId);
        if (pose == null) return;
        weapon.transform.localPosition = pose.fpsPosition;
        weapon.transform.localRotation = Quaternion.Euler(pose.fpsRotation);
        weapon.transform.localScale = Vector3.one;
    }

    public void ApplyHandPose(GameObject weapon, int weaponId)
    {
        WeaponSlotPose pose = GetPose(weaponId);
        if (pose == null) return;
        weapon.transform.localPosition = pose.handPosition;
        weapon.transform.localRotation = Quaternion.Euler(pose.handRotation);
        weapon.transform.localScale = pose.handScale;
    }

    void Update()
    {
        if (!photonView.IsMine || playerController.isPlayerDead || (playerInventory != null && playerInventory.IsInventoryOpen)) return;
        if (Input.GetKeyDown(KeyCode.E)) PickupWeaponFromGround();
        if (Input.GetKeyDown(KeyCode.G)) DropWeapon();
    }

    public WeaponData GetWeaponData(int weaponId)
    {
        foreach (var wd in weaponRegistry) if (wd.weaponId == weaponId) return wd;
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
        if (!playerInventory.IsGun(itemId)) { if (currentGun != null) UnequipCurrentGun(); return; }

        int weaponId = -(itemId + 100);
        WeaponData data = GetWeaponData(weaponId);

        if (equippedSlotIndex == slotIndex && currentGun != null) { currentGun.Equip(); return; }

        UnequipCurrentWeapon();
        CreateAndEquipGun(slotIndex, weaponId, data);
    }

    public void EquipMeleeFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        int itemId = playerInventory.inventory[slotIndex];
        if (!playerInventory.IsMelee(itemId)) { if (currentMelee != null) UnequipCurrentMelee(); return; }

        int meleeId = playerInventory.GetMeleeIdFromItemId(itemId);
        if (equippedSlotIndex == slotIndex && currentMelee != null) { currentMelee.Equip(); return; }

        UnequipCurrentWeapon();
        CreateAndEquipMelee(slotIndex, meleeId);
    }

    public void UnequipCurrentWeapon()
    {
        if (currentGun != null) UnequipCurrentGun();
        if (currentMelee != null) UnequipCurrentMelee();
        CubeWorldCharacter cw = GetComponent<CubeWorldCharacter>();
        if (cw != null) cw.SetHasWeapon(false);
        equippedSlotIndex = -1; equippedWeaponId = -1;
    }

    void UnequipCurrentGun()
    {
        if (currentGun == null) return;
        Gun old = currentGun; currentGun = null;
        old.Unequip();
        if (old != null && old.gameObject != null) 
        { 
            if (PhotonNetwork.IsConnected) 
                PhotonNetwork.Destroy(old.gameObject); 
            else 
                Destroy(old.gameObject); 
        }
    }

    void UnequipCurrentMelee()
    {
        if (currentMelee == null) return;
        MeleeWeapon old = currentMelee; currentMelee = null;
        old.Unequip();
        if (old != null && old.gameObject != null) 
        { 
            if (PhotonNetwork.IsConnected) 
                PhotonNetwork.Destroy(old.gameObject); 
            else 
                Destroy(old.gameObject); 
        }
    }

    void CreateAndEquipGun(int slotIndex, int weaponId, WeaponData data)
    {
        if (gunPrefab == null) { Debug.LogError("❌ gunPrefab не назначен!"); return; }

        CubeWorldCharacter cw = GetComponent<CubeWorldCharacter>();
        if (cw == null || cw.WeaponAnchor == null) { Debug.LogError("❌ CubeWorldCharacter не найден"); return; }

        // 🆕 PhotonNetwork.Instantiate для сетевой синхронизации
        GameObject gunObj = PhotonNetwork.Instantiate(gunPrefab.name, cw.WeaponAnchor.position, cw.WeaponAnchor.rotation);
        gunObj.transform.SetParent(cw.WeaponAnchor);
        gunObj.transform.localPosition = Vector3.zero;
        gunObj.transform.localRotation = Quaternion.identity;

        Gun gun = gunObj.GetComponent<Gun>();
        if (gun == null) { Debug.LogError("❌ Gun компонент не найден!"); PhotonNetwork.Destroy(gunObj); return; }

        if (data != null) ApplyWeaponData(gun, data);

        gun.fpsCam = playerController.playerCamera;
        gun.playerInventory = playerInventory;
        gun.weaponId = weaponId;
        gun.slotIndex = slotIndex;
        gun.weaponManager = this;

        Transform barrelEnd = gunObj.transform.Find("BarrelEnd");
        if (barrelEnd == null) 
        { 
            GameObject b = new GameObject("BarrelEnd"); 
            b.transform.SetParent(gunObj.transform); 
            b.transform.localPosition = new Vector3(0, 0, 0.5f); 
            barrelEnd = b.transform; 
        }
        gun.barrelEnd = barrelEnd;

        cw.SetHasWeapon(true);
        equippedSlotIndex = slotIndex;
        equippedWeaponId = weaponId;
        currentGun = gun;

        StartCoroutine(DelayedEquip(gun));
    }

    void CreateAndEquipMelee(int slotIndex, int meleeId)
    {
        if (meleePrefab == null) { Debug.LogError("❌ meleePrefab не назначен!"); return; }

        CubeWorldCharacter cw = GetComponent<CubeWorldCharacter>();
        if (cw == null || cw.WeaponAnchor == null) { Debug.LogError("❌ не найден"); return; }

        // 🆕 PhotonNetwork.Instantiate для сетевой синхронизации
        GameObject meleeObj = PhotonNetwork.Instantiate(meleePrefab.name, cw.WeaponAnchor.position, cw.WeaponAnchor.rotation);
        meleeObj.transform.SetParent(cw.WeaponAnchor);
        meleeObj.transform.localPosition = Vector3.zero;
        meleeObj.transform.localRotation = Quaternion.identity;

        MeleeWeapon melee = meleeObj.GetComponent<MeleeWeapon>();
        if (melee == null) { Debug.LogError("❌ MeleeWeapon не найден!"); PhotonNetwork.Destroy(meleeObj); return; }

        melee.fpsCam = playerController.playerCamera;
        melee.playerInventory = playerInventory;
        melee.meleeId = meleeId;
        melee.slotIndex = slotIndex;
        melee.weaponManager = this;

        cw.SetHasWeapon(true);
        equippedSlotIndex = slotIndex;
        equippedWeaponId = meleeId;
        currentMelee = melee;

        StartCoroutine(DelayedEquipMelee(melee));
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

    IEnumerator DelayedEquip(Gun gun) { yield return new WaitForSeconds(0.2f); if (gun != null) gun.Equip(); }
    IEnumerator DelayedEquipMelee(MeleeWeapon melee) { yield return new WaitForSeconds(0.2f); if (melee != null) melee.Equip(); }

    public void AddWeaponToInventory(int weaponId, WeaponData data)
    {
        if (data == null) return;
        RegisterWeaponData(data);
        int free = -1;
        for (int i = 0; i < 15; i++) { if (playerInventory.inventory[i] == 0) { free = i; break; } }
        if (free == -1) for (int i = 15; i < 20; i++) { if (playerInventory.inventory[i] == 0) { free = i; break; } }
        if (free == -1) return;
        playerInventory.inventory[free] = -(100 + weaponId);
        playerInventory.inventoryCounts[free] = data.maxAmmo;
        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();
    }

    void PickupWeaponFromGround()
    {
        foreach (Collider col in Physics.OverlapSphere(transform.position, pickupRange))
        {
            PickupWeapon p = col.GetComponent<PickupWeapon>();
            if (p != null) { p.PickUp(gameObject); return; }
        }
    }

    void DropWeapon()
    {
        if (equippedSlotIndex < 0) return;
        int slot = equippedSlotIndex;
        int itemId = playerInventory.inventory[slot];
        if (playerInventory.IsGun(itemId))
        {
            int wid = -(itemId + 100);
            WeaponData data = GetWeaponData(wid);
            int mag = currentGun != null ? currentGun.currentAmmo : playerInventory.inventoryCounts[slot];
            if (currentGun != null) 
            { 
                if (PhotonNetwork.IsConnected) 
                    PhotonNetwork.Destroy(currentGun.gameObject); 
                else 
                    Destroy(currentGun.gameObject); 
                currentGun = null; 
            }
            PickupWeapon.DropWeapon(transform.position + transform.forward * 3f + Vector3.up * 2f, Quaternion.identity,
                data != null ? data.weaponName : "Оружие", data != null ? data.damage : 25, data != null ? data.fireRate : 0.1f,
                data != null ? data.range : 100f, data != null ? data.spread : 0.02f, data != null ? data.maxAmmo : 30,
                data != null ? data.reloadTime : 2f, data != null ? data.recoilAmount : 0.5f, data != null ? data.recoilRecovery : 5f,
                data != null ? data.muzzleFlash : null, data != null ? data.impactEffect : null, data != null ? data.shootSound : null,
                data != null ? data.reloadSound : null, data != null ? data.emptySound : null, null, wid, mag);
            playerInventory.inventory[slot] = 0; playerInventory.inventoryCounts[slot] = 0;
        }
        else if (playerInventory.IsMelee(itemId))
        {
            if (currentMelee != null) 
            { 
                if (PhotonNetwork.IsConnected) 
                    PhotonNetwork.Destroy(currentMelee.gameObject); 
                else 
                    Destroy(currentMelee.gameObject); 
                currentMelee = null; 
            }
        }
        CubeWorldCharacter cw = GetComponent<CubeWorldCharacter>();
        if (cw != null) cw.SetHasWeapon(false);
        equippedSlotIndex = -1; equippedWeaponId = -1;
        playerInventory.UpdateHotbarUI();
    }

    public void FlushEquippedAmmo() { if (currentGun != null) currentGun.SaveAmmoToInventory(); }

    public void OnSlotMoved(int from, int to)
    {
        if (from == to) return;
        if (currentGun != null)
        {
            if (currentGun.slotIndex == from) { currentGun.slotIndex = to; equippedSlotIndex = to; currentGun.LoadAmmoFromInventory(); }
            else if (currentGun.slotIndex == to) { currentGun.slotIndex = from; equippedSlotIndex = from; currentGun.LoadAmmoFromInventory(); }
            else { int cid = playerInventory.inventory[equippedSlotIndex]; if (cid != -(100 + currentGun.weaponId)) UnequipCurrentWeapon(); }
        }
        else if (currentMelee != null)
        {
            if (currentMelee.slotIndex == from) { currentMelee.slotIndex = to; equippedSlotIndex = to; }
            else if (currentMelee.slotIndex == to) { currentMelee.slotIndex = from; equippedSlotIndex = from; }
            else { int cid = playerInventory.inventory[equippedSlotIndex]; if (cid != -(PlayerInventory.MELEE_BASE + currentMelee.meleeId)) UnequipCurrentWeapon(); }
        }
    }
}