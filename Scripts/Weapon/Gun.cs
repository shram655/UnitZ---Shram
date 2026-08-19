using UnityEngine;
using Photon.Pun;
using System.Collections;

public class Gun : MonoBehaviourPun
{
    [Header("Основные настройки")]
    public string weaponName = "Оружие";
    public float damage = 25f;
    public float fireRate = 0.1f;
    public float range = 100f;
    public float spread = 0.02f;

    [Header("Патроны")]
    public int maxAmmo = 30;
    public int currentAmmo;
    public float reloadTime = 2f;
    private bool isReloading = false;

    public int weaponId = -1;
    public int slotIndex = -1;

    [Header("Отдача")]
    public float recoilAmount = 0.5f;
    public float recoilRecovery = 5f;

    [Header("FOV-кик при беге")]
    public float sprintLerpSpeed = 12f;
    public float snapSpeed = 30f;
    public bool useFovKick = true;
    public float sprintFovAdd = 8f;

    [Header("Эффекты")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioSource gunAudio;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Ссылки")]
    public Camera fpsCam;
    public Transform barrelEnd;
    public PlayerInventory playerInventory;

    private float nextTimeToFire = 0f;
    private float currentRecoil = 0f;
    private bool isEquipped = false;
    private bool ammoInitialized = false;

    private float normalFov = 60f;
    private bool fovCaptured = false;

    // 🆕 CubeWorldCharacter вместо VoxelCharacter
    private CubeWorldCharacter ownerCharacter;

    void Start()
    {
        if (gunAudio == null)
        {
            gunAudio = GetComponent<AudioSource>();
            if (gunAudio == null) gunAudio = gameObject.AddComponent<AudioSource>();
        }

        isEquipped = false;

        // ЧУЖОЙ автомат: крепим к телу владельца
        if (photonView != null && !photonView.IsMine)
        {
            StartCoroutine(AttachToOwner());
        }
    }

    // 🆕 Ищем тело владельца (CubeWorldCharacter)
    IEnumerator AttachToOwner()
    {
        for (int i = 0; i < 60; i++)
        {
            if (this == null) yield break;

            PlayerController[] pcs = FindObjectsOfType<PlayerController>();
            foreach (var pc in pcs)
            {
                if (pc.view != null && photonView != null && pc.view.OwnerActorNr == photonView.OwnerActorNr)
                {
                    // 🆕 CubeWorldCharacter вместо VoxelCharacter
                    CubeWorldCharacter cwChar = pc.GetComponent<CubeWorldCharacter>();
                    if (cwChar != null && cwChar.WeaponAnchor != null)
                    {
                        ownerCharacter = cwChar;
                        transform.SetParent(cwChar.WeaponAnchor);
                        transform.localPosition = Vector3.zero;
                        transform.localRotation = Quaternion.identity;
                        transform.localScale = Vector3.one;
                    }
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // 🆕 При уничтожении автомата
    void OnDestroy()
    {
        if (ownerCharacter != null)
        {
            ownerCharacter.SetHasWeapon(false);
            ownerCharacter = null;
        }
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;
        if (fpsCam == null) return;

        if (playerInventory != null && playerInventory.IsInventoryOpen) return;

        if (fireRate <= 0f) fireRate = 0.1f;

        currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * recoilRecovery);
        fpsCam.transform.localRotation *= Quaternion.Euler(-currentRecoil, 0, 0);

        if (Input.GetMouseButton(0) && !isReloading) Shoot();

        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < maxAmmo
            && playerInventory != null && playerInventory.GetTotalAmmo() > 0)
        {
            StartCoroutine(Reload());
        }
    }

    void LateUpdate()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (!useFovKick) return;

        if (!fovCaptured)
        {
            normalFov = fpsCam.fieldOfView;
            fovCaptured = true;
        }

        bool inventoryOpen = playerInventory != null && playerInventory.IsInventoryOpen;
        bool firing = Input.GetMouseButton(0) && !inventoryOpen;
        bool sprinting = IsSprinting() && !firing;

        float t = Time.deltaTime * (firing ? snapSpeed : sprintLerpSpeed);
        float targetFov = normalFov + (sprinting ? sprintFovAdd : 0f);
        fpsCam.fieldOfView = Mathf.Lerp(fpsCam.fieldOfView, targetFov, t);
    }

    bool IsSprinting()
    {
        if (isReloading) return false;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return false;

        float moveAmount = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
        return Input.GetKey(KeyCode.LeftShift) && moveAmount > 0.1f;
    }

    public void Equip()
    {
        if (photonView != null && !photonView.IsMine) return;

        isEquipped = true;
        gameObject.SetActive(true);

        if (!ammoInitialized)
        {
            ammoInitialized = true;
            LoadFromInventory();
        }
    }

    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;

        isEquipped = false;
        isReloading = false;
        gameObject.SetActive(false);

        SaveToInventory();

        if (fpsCam != null && useFovKick) fpsCam.fieldOfView = normalFov;
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    void LoadFromInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;

        currentAmmo = playerInventory.inventoryCounts[slotIndex];
        playerInventory.inventoryCounts[slotIndex] = 0;
        playerInventory.UpdateHotbarUI();
    }

    void SaveToInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;

        playerInventory.inventoryCounts[slotIndex] = currentAmmo;
        currentAmmo = 0;
        playerInventory.UpdateHotbarUI();
    }

    void Shoot()
    {
        if (Time.time < nextTimeToFire) return;
        nextTimeToFire = Time.time + fireRate;

        if (currentAmmo <= 0)
        {
            if (gunAudio != null && emptySound != null)
                gunAudio.PlayOneShot(emptySound);
            return;
        }

        currentAmmo--;

        Vector3 shootDirection = fpsCam.transform.forward;
        shootDirection.x += Random.Range(-spread, spread);
        shootDirection.y += Random.Range(-spread, spread);
        shootDirection.z += Random.Range(-spread, spread);
        shootDirection.Normalize();

        Vector3 rayOrigin = fpsCam.transform.position;

        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, shootDirection, out hit, range))
        {
            PlayerHealth target = hit.transform.GetComponent<PlayerHealth>();
            if (target == null)
                target = hit.transform.GetComponentInParent<PlayerHealth>();

            if (target != null)
            {
                target.photonView.RPC("RPC_TakeDamage", target.photonView.Owner, damage);
            }

            if (impactEffect != null)
            {
                GameObject impactGO = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impactGO, 2f);
            }
        }

        if (muzzleFlash != null) muzzleFlash.Play();
        if (gunAudio != null && shootSound != null) gunAudio.PlayOneShot(shootSound);

        currentRecoil += recoilAmount;
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break;
        isReloading = true;

        if (gunAudio != null && reloadSound != null)
            gunAudio.PlayOneShot(reloadSound);

        yield return new WaitForSeconds(reloadTime);

        if (playerInventory != null)
        {
            int needed = maxAmmo - currentAmmo;
            int take = Mathf.Min(needed, playerInventory.GetTotalAmmo());

            if (take > 0)
            {
                playerInventory.ConsumeAmmo(take);
                currentAmmo += take;
            }
        }

        isReloading = false;
    }

    void OnGUI()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        int reserve = playerInventory != null ? playerInventory.GetTotalAmmo() : 0;

        GUI.Label(new Rect(10, 10, 400, 25), "Оружие: " + weaponName, style);
        GUI.Label(new Rect(10, 35, 400, 25), $"Магазин: {currentAmmo}/{maxAmmo}  (запас: {reserve})", style);
        if (isReloading)
        {
            GUI.Label(new Rect(10, 60, 400, 25), "ПЕРЕЗАРЯДКА...", style);
        }
    }
}