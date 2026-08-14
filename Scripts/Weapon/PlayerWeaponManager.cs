using UnityEngine;
using Photon.Pun;
using System.Collections;

public class PlayerWeaponManager : MonoBehaviourPun
{
    [Header("Настройки оружия")]
    public Transform weaponSlot;
    public Transform thirdPersonWeaponSlot;
    public float pickupRange = 3f;
    public GameObject gunPrefab;
    public Sprite weaponIcon;

    [Header("Параметры текущего оружия")]
    public string weaponName; public int weaponDamage; public float weaponFireRate; public float weaponRange; public float weaponSpread;
    public int weaponMaxAmmo; public float weaponReloadTime; public float weaponRecoilAmount; public float weaponRecoilRecovery;
    public ParticleSystem weaponMuzzleFlash; public GameObject weaponImpactEffect;
    public AudioClip weaponShootSound; public AudioClip weaponReloadSound; public AudioClip weaponEmptySound;

    private Gun currentGun; private Gun thirdPersonGun; private bool hasWeaponEquipped = false;
    public bool HasWeaponEquipped => hasWeaponEquipped;

    private PlayerController playerController; private PlayerInventory playerInventory;

    void Awake() { playerController = GetComponent<PlayerController>(); playerInventory = GetComponent<PlayerInventory>(); }

    void Update()
    {
        if (!photonView.IsMine || playerController.isPlayerDead || playerInventory.IsInventoryOpen) return;
        if (Input.GetKeyDown(KeyCode.E)) PickupWeaponFromGround();
        if (Input.GetKeyDown(KeyCode.G)) DropWeapon();
    }

    public void EquipWeaponFromSlot() { if (hasWeaponEquipped) return; CreateAndEquipWeapon(); }

    // ══════════════════════════════════════════════════════
    //  ИСПРАВЛЕНО: оружие НЕ уничтожается при снятии —
    //  оно прячется и хранит остаток магазина
    // ══════════════════════════════════════════════════════
    public void UnequipCurrentWeapon()
    {
        if (!hasWeaponEquipped) return;
        if (currentGun != null) currentGun.Unequip();
        if (thirdPersonGun != null) thirdPersonGun.Hide();
        hasWeaponEquipped = false;
    }

    // Полное уничтожение оружия (при выбросе или получении нового)
    private void DestroyGuns()
    {
        if (currentGun != null) { PhotonNetwork.Destroy(currentGun.gameObject); currentGun = null; }
        if (thirdPersonGun != null) { PhotonNetwork.Destroy(thirdPersonGun.gameObject); thirdPersonGun = null; }
    }

    void CreateAndEquipWeapon()
    {
        if (weaponSlot == null || gunPrefab == null) return;

        // Повторная экипировка: автомат возвращается с тем же магазином
        if (currentGun != null)
        {
            currentGun.Equip();
            if (thirdPersonGun != null) thirdPersonGun.Show();
            hasWeaponEquipped = true;
            return;
        }

        GameObject gunObj = PhotonNetwork.Instantiate(gunPrefab.name, weaponSlot.position, weaponSlot.rotation);
        gunObj.transform.SetParent(weaponSlot);
        gunObj.transform.localPosition = new Vector3(0.3f, -0.3f, 0.5f);
        gunObj.transform.localRotation = Quaternion.identity;
        gunObj.transform.localScale = Vector3.one;
        
        currentGun = gunObj.GetComponent<Gun>();
        if (currentGun == null) { PhotonNetwork.Destroy(gunObj); return; }
        
        ApplyWeaponStats(currentGun);
        currentGun.fpsCam = playerController.playerCamera;
        currentGun.playerInventory = playerInventory;
        
        Transform barrelEnd = gunObj.transform.Find("BarrelEnd");
        if (barrelEnd == null) { GameObject barrelObj = new GameObject("BarrelEnd"); barrelObj.transform.SetParent(gunObj.transform); barrelObj.transform.localPosition = new Vector3(0, 0, 0.5f); barrelEnd = barrelObj.transform; }
        currentGun.barrelEnd = barrelEnd;

        if (thirdPersonWeaponSlot != null)
        {
            GameObject thirdPersonGunObj = PhotonNetwork.Instantiate(gunPrefab.name, thirdPersonWeaponSlot.position, thirdPersonWeaponSlot.rotation);
            thirdPersonGunObj.transform.SetParent(thirdPersonWeaponSlot);
            thirdPersonGunObj.transform.localPosition = Vector3.zero;
            thirdPersonGunObj.transform.localRotation = Quaternion.identity;
            thirdPersonGunObj.transform.localScale = Vector3.one;
            
            thirdPersonGun = thirdPersonGunObj.GetComponent<Gun>();
            if (thirdPersonGun != null) { ApplyWeaponStats(thirdPersonGun); thirdPersonGun.fpsCam = null; thirdPersonGun.playerInventory = playerInventory; }
        }
        StartCoroutine(DelayedEquip(currentGun));
    }

    private void ApplyWeaponStats(Gun gun)
    {
        gun.weaponName = string.IsNullOrEmpty(weaponName) ? "Автомат" : weaponName;
        gun.damage = weaponDamage > 0 ? weaponDamage : 25f;
        gun.fireRate = weaponFireRate > 0.01f ? weaponFireRate : 0.1f;
        gun.range = weaponRange > 0 ? weaponRange : 100f;
        gun.spread = weaponSpread > 0 ? weaponSpread : 0.02f;
        gun.maxAmmo = weaponMaxAmmo > 0 ? weaponMaxAmmo : 30;
        gun.reloadTime = weaponReloadTime > 0 ? weaponReloadTime : 2f;
        gun.recoilAmount = weaponRecoilAmount > 0 ? weaponRecoilAmount : 0.5f;
        gun.recoilRecovery = weaponRecoilRecovery > 0 ? weaponRecoilRecovery : 5f;

        if (weaponMuzzleFlash != null) gun.muzzleFlash = weaponMuzzleFlash;
        if (weaponImpactEffect != null) gun.impactEffect = weaponImpactEffect;
        if (weaponShootSound != null) gun.shootSound = weaponShootSound;
        if (weaponReloadSound != null) gun.reloadSound = weaponReloadSound;
        if (weaponEmptySound != null) gun.emptySound = weaponEmptySound;
    }

    IEnumerator DelayedEquip(Gun gun) { yield return new WaitForSeconds(0.2f); gun.Equip(); hasWeaponEquipped = true; }

    public void AddWeaponToInventory(string name, int damage, float fireRate, float range, float spread, int maxAmmo, float reloadTime, float recoilAmount, float recoilRecovery, ParticleSystem muzzleFlash, GameObject impactEffect, AudioClip shootSound, AudioClip reloadSound, AudioClip emptySound)
    {
        weaponName = name; weaponDamage = damage; weaponFireRate = fireRate; weaponRange = range; weaponSpread = spread;
        weaponMaxAmmo = maxAmmo; weaponReloadTime = reloadTime; weaponRecoilAmount = recoilAmount; weaponRecoilRecovery = recoilRecovery;
        weaponMuzzleFlash = muzzleFlash; weaponImpactEffect = impactEffect; weaponShootSound = shootSound; weaponReloadSound = reloadSound; weaponEmptySound = emptySound;

        // Новое оружие с лута = новый автомат (старый уничтожаем)
        DestroyGuns();

        int freeSlot = -1;
        for (int i = 0; i < 15; i++) { if (playerInventory.inventory[i] == 0) { freeSlot = i; break; } }
        if (freeSlot == -1) { for (int i = 15; i < 20; i++) { if (playerInventory.inventory[i] == 0) { freeSlot = i; break; } } }
        if (freeSlot == -1) { Debug.LogWarning("⚠️ Нет места для оружия!"); return; }

        playerInventory.inventory[freeSlot] = -1;
        playerInventory.inventoryCounts[freeSlot] = 1;
        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();
    }

    void PickupWeaponFromGround()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders) { PickupWeapon pickup = col.GetComponent<PickupWeapon>(); if (pickup != null) { pickup.PickUp(gameObject); return; } }
    }

    void DropWeapon()
    {
        if (playerInventory.inventory[playerInventory.selectedSlot] != -1) return;

        UnequipCurrentWeapon();
        Vector3 dropPosition = transform.position + transform.forward * 3f;
        dropPosition.y = transform.position.y + 2f;
        PickupWeapon.DropWeapon(dropPosition, Quaternion.identity, weaponName, weaponDamage, weaponFireRate, weaponRange, weaponSpread, weaponMaxAmmo, weaponReloadTime, weaponRecoilAmount, weaponRecoilRecovery, weaponMuzzleFlash, weaponImpactEffect, weaponShootSound, weaponReloadSound, weaponEmptySound, null);

        // Выброшенное оружие уничтожается вместе с магазином
        DestroyGuns();

        playerInventory.inventory[playerInventory.selectedSlot] = 0;
        playerInventory.inventoryCounts[playerInventory.selectedSlot] = 0;
        playerInventory.UpdateHotbarUI();
        if (playerInventory.inventoryUI != null) playerInventory.inventoryUI.UpdateAllSlots();
    }
}