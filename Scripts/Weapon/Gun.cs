using UnityEngine;
using Photon.Pun;
using System.Collections;

public class Gun : MonoBehaviourPun
{
    [Header("Основные")]
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
    public PlayerWeaponManager weaponManager;

    [Header("═══ 🎯 FPS: ТОНКАЯ ПОДГОНКА (относительно WeaponSlot) ═══")]
    [Tooltip("Основная позиция задаётся WeaponSlot в префабе игрока! Здесь только мелкая подгонка")]
    public Vector3 fpsOffsetPosition = Vector3.zero;
    [Tooltip("Мелкий доворот конкретного оружия")]
    public Vector3 fpsOffsetRotation = Vector3.zero;

    [Header("═══ 🖐 3-е ЛИЦО: ПОЗИЦИЯ В КИСТИ (WeaponAnchor) ═══")]
    [Tooltip("Позиция относительно WeaponAnchor для других игроков")]
    public Vector3 handPosition = Vector3.zero;
    [Tooltip("Вращение в кисти")]
    public Vector3 handRotation = Vector3.zero;

    [Header("Прицеливание (только зум/точность)")]
    public bool useADS = true;
    [Range(20f, 90f)] public float adsFov = 40f;
    [Range(0.05f, 1f)] public float adsSpreadMultiplier = 0.3f;

    private float nextTimeToFire = 0f;
    private float currentRecoil = 0f;
    private bool isEquipped = false;
    private bool ammoInitialized = false;
    private CubeWorldCharacter ownerCharacter;

    bool IsLocal() => photonView == null || photonView.IsMine;

    void Awake() { if (IsLocal()) gameObject.SetActive(false); }

    void Start()
    {
        if (gunAudio == null) { gunAudio = GetComponent<AudioSource>(); if (gunAudio == null) gunAudio = gameObject.AddComponent<AudioSource>(); }

        if (IsLocal()) StartCoroutine(SelfEquipFallback());
        else StartCoroutine(AttachToOwner());
    }

    IEnumerator SelfEquipFallback()
    {
        float w = 0f;
        while (!isEquipped && w < 2f)
        {
            if (fpsCam != null) { yield return null; if (!isEquipped) Equip(); yield break; }
            w += Time.deltaTime; yield return null;
        }
    }

    // 🖐 Другие игроки: крепим к WeaponAnchor (кисти). МАСШТАБ НЕ ТРОГАЕМ!
    IEnumerator AttachToOwner()
    {
        for (int i = 0; i < 60; i++)
        {
            if (this == null) yield break;
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc.view != null && photonView != null && pc.view.OwnerActorNr == photonView.OwnerActorNr)
                {
                    CubeWorldCharacter cw = pc.GetComponent<CubeWorldCharacter>();
                    if (cw != null && cw.WeaponAnchor != null)
                    {
                        ownerCharacter = cw;
                        if (transform.parent != cw.WeaponAnchor)
                            transform.SetParent(cw.WeaponAnchor);
                        transform.localPosition = handPosition;
                        transform.localRotation = Quaternion.Euler(handRotation);
                        gameObject.SetActive(true);
                    }
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDestroy() { ownerCharacter = null; }

    bool IsSelf(Transform t)
    {
        if (ownerCharacter == null) return false;
        return t == ownerCharacter.transform || t.IsChildOf(ownerCharacter.transform);
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return;
        if (fireRate <= 0f) fireRate = 0.1f;

        currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * recoilRecovery);
        fpsCam.transform.localRotation *= Quaternion.Euler(-currentRecoil, 0, 0);

        if (Input.GetMouseButton(0) && !isReloading) Shoot();
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < maxAmmo && playerInventory != null && playerInventory.GetTotalAmmo() > 0)
            StartCoroutine(Reload());
    }

    public void Equip()
    {
        if (photonView != null && !photonView.IsMine) return;
        bool first = !isEquipped;
        isEquipped = true;
        gameObject.SetActive(true);

        if (first && !ammoInitialized) { ammoInitialized = true; LoadFromInventory(); }

        if (ownerCharacter == null)
        {
            ownerCharacter = GetComponentInParent<CubeWorldCharacter>();
            if (ownerCharacter == null) { PlayerController pc = GetComponentInParent<PlayerController>(); if (pc != null) ownerCharacter = pc.GetComponent<CubeWorldCharacter>(); }
        }
        if (ownerCharacter != null) ownerCharacter.SetHasWeapon(true);

        // 🎯 ЛОКАЛЬНО: крепим К WEAPON SLOT. МАСШТАБ НЕ ТРОГАЕМ — берётся из префаба!
        if (fpsCam != null)
        {
            Transform slot = fpsCam.transform.Find("WeaponSlot");
            if (slot == null) slot = fpsCam.transform;

            if (transform.parent != slot)
                transform.SetParent(slot);

            transform.localPosition = fpsOffsetPosition;
            transform.localRotation = Quaternion.Euler(fpsOffsetRotation);
        }

        PhotonTransformView ptv = GetComponent<PhotonTransformView>();
        if (ptv != null) ptv.enabled = false;
    }

    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;
        isEquipped = false; isReloading = false;
        gameObject.SetActive(false);
        SaveToInventory();
    }

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

    public void SaveAmmoToInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        playerInventory.inventoryCounts[slotIndex] = currentAmmo;
        playerInventory.UpdateHotbarUI();
    }

    public void LoadAmmoFromInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        currentAmmo = playerInventory.inventoryCounts[slotIndex];
        playerInventory.inventoryCounts[slotIndex] = 0;
        playerInventory.UpdateHotbarUI();
    }

    void SyncAmmoToInventory()
    {
        if (playerInventory == null) return;
        if (slotIndex < 0 || slotIndex >= playerInventory.inventory.Length) return;
        playerInventory.inventoryCounts[slotIndex] = currentAmmo;
    }

    void Shoot()
    {
        if (Time.time < nextTimeToFire) return;
        nextTimeToFire = Time.time + fireRate;
        if (currentAmmo <= 0) { if (gunAudio != null && emptySound != null) gunAudio.PlayOneShot(emptySound); return; }
        currentAmmo--; SyncAmmoToInventory();
        Vector3 dir = fpsCam.transform.forward;
        dir.x += Random.Range(-spread, spread); dir.y += Random.Range(-spread, spread); dir.z += Random.Range(-spread, spread);
        dir.Normalize();
        RaycastHit[] hits = Physics.RaycastAll(fpsCam.transform.position, dir, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (IsSelf(hit.transform)) continue;
            PlayerHealth t = hit.transform.GetComponent<PlayerHealth>();
            if (t == null) t = hit.transform.GetComponentInParent<PlayerHealth>();
            if (t != null) { if (photonView == null || photonView.IsMine) if (t.photonView != null && t.photonView.IsMine) continue; t.photonView.RPC("RPC_TakeDamage", t.photonView.Owner, damage); }
            if (impactEffect != null) { GameObject g = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal)); Destroy(g, 2f); }
            break;
        }
        if (muzzleFlash != null) muzzleFlash.Play();
        if (gunAudio != null && shootSound != null) gunAudio.PlayOneShot(shootSound);
        currentRecoil += recoilAmount;
    }

    IEnumerator Reload()
    {
        if (isReloading) yield break;
        isReloading = true;
        if (gunAudio != null && reloadSound != null) gunAudio.PlayOneShot(reloadSound);
        yield return new WaitForSeconds(reloadTime);
        if (playerInventory != null) { int n = maxAmmo - currentAmmo; int t = Mathf.Min(n, playerInventory.GetTotalAmmo()); if (t > 0) { playerInventory.ConsumeAmmo(t); currentAmmo += t; SyncAmmoToInventory(); } }
        isReloading = false;
    }

    void OnGUI()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;
        GUIStyle s = new GUIStyle(); s.fontSize = 16; s.normal.textColor = Color.white;
        int r = playerInventory != null ? playerInventory.GetTotalAmmo() : 0;
        GUI.Label(new Rect(10, 10, 400, 25), "Оружие: " + weaponName, s);
        GUI.Label(new Rect(10, 35, 400, 25), $"Магазин: {currentAmmo}/{maxAmmo}  (запас: {r})", s);
        if (isReloading) GUI.Label(new Rect(10, 60, 400, 25), "ПЕРЕЗАРЯДКА...", s);
    }
}