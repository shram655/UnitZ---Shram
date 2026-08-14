using UnityEngine;
using Photon.Pun;
using System.Collections;

public class Gun : MonoBehaviourPun
{
    [Header("Основные настройки")]
    public string weaponName = "Автомат";
    public float damage = 25f;
    public float fireRate = 0.1f;
    public float range = 100f;
    public float spread = 0.02f;

    [Header("Патроны")]
    public int maxAmmo = 30;
    public int currentAmmo;
    public float reloadTime = 2f;
    private bool isReloading = false;
    private bool ammoInitialized = false;

    [Header("Отдача")]
    public float recoilAmount = 0.5f;
    public float recoilRecovery = 5f;

    [Header("Анимация бега")]
    public float sprintLerpSpeed = 12f;                                  // плавность входа в бег
    public float snapSpeed = 30f;                                        // резкий возврат при стрельбе
    public Vector3 sprintPosition = new Vector3(0.25f, -0.40f, 0.45f);  // позиция в беге
    public Vector3 sprintRotation = new Vector3(0f, -60f, 0f);          // 🆕 просто поворот ВЛЕВО, без крена
    public float bobStrength = 0.006f;
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

    private Vector3 normalPosition;
    private Quaternion normalRotation;
    private float normalFov = 60f;
    private bool poseCaptured = false;
    private float bobTimer = 0f;

    void Start()
    {
        if (gunAudio == null)
        {
            gunAudio = GetComponent<AudioSource>();
            if (gunAudio == null)
                gunAudio = gameObject.AddComponent<AudioSource>();
        }

        isEquipped = false;

        if (photonView != null && !photonView.IsMine)
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;
        if (fpsCam == null) return;

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

    // ══════════════════════════════════════════════════════
    //  АНИМАЦИЯ: ровный поворот вбок при беге,
    //  резкий возврат в боевое положение при стрельбе
    // ══════════════════════════════════════════════════════
    void LateUpdate()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped || fpsCam == null) return;

        if (!poseCaptured)
        {
            normalPosition = transform.localPosition;
            normalRotation = transform.localRotation;
            normalFov = fpsCam.fieldOfView;
            poseCaptured = true;
        }

        bool firing = Input.GetMouseButton(0);
        bool sprinting = IsSprinting() && !firing;

        float moveAmount = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
        bool moving = moveAmount > 0.1f;

        // Покачивание (отключается при стрельбе)
        if (moving && !firing) bobTimer += Time.deltaTime * (sprinting ? 14f : 9f);
        float bob = (moving && !firing) ? Mathf.Sin(bobTimer) * bobStrength * (sprinting ? 1.6f : 1f) : 0f;

        Vector3 targetPos = sprinting ? sprintPosition : normalPosition;
        targetPos.y += bob;
        Quaternion targetRot = sprinting ? Quaternion.Euler(sprintRotation) : normalRotation;

        float t = Time.deltaTime * (firing ? snapSpeed : sprintLerpSpeed);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, t);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, t);

        if (useFovKick)
        {
            float targetFov = normalFov + (sprinting ? sprintFovAdd : 0f);
            fpsCam.fieldOfView = Mathf.Lerp(fpsCam.fieldOfView, targetFov, t);
        }
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

        if (fpsCam != null && useFovKick) fpsCam.fieldOfView = normalFov;
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    void LoadFromInventory()
    {
        if (playerInventory == null) return;

        int take = Mathf.Min(maxAmmo, playerInventory.GetTotalAmmo());
        if (take > 0)
        {
            playerInventory.ConsumeAmmo(take);
            currentAmmo = take;
        }
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

        RaycastHit hit;
        Vector3 rayOrigin = barrelEnd != null ? barrelEnd.position : fpsCam.transform.position;

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

        Debug.Log("🔄 Перезарядка...");
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
                Debug.Log($"✅ Перезаряжено: +{take}, в магазине {currentAmmo}/{maxAmmo}");
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