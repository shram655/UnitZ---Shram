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
    
    private float nextTimeToFire = 0f;
    private float currentRecoil = 0f;
    private bool isEquipped = false;
    
    void Start()
    {
        currentAmmo = maxAmmo;
        
        if (gunAudio == null)
        {
            gunAudio = GetComponent<AudioSource>();
            if (gunAudio == null)
                gunAudio = gameObject.AddComponent<AudioSource>();
        }
        
        isEquipped = false;
        
        Debug.Log("=== GUN START ===");
        Debug.Log("photonView: " + (photonView != null));
        if (photonView != null)
        {
            Debug.Log("IsMine: " + photonView.IsMine);
            Debug.Log("ViewID: " + photonView.ViewID);
        }
        Debug.Log("fpsCam: " + (fpsCam != null));
        Debug.Log("barrelEnd: " + (barrelEnd != null));
        Debug.Log("===================");
        
        // Скрываем оружие для других игроков
        if (photonView != null && !photonView.IsMine)
        {
            gameObject.SetActive(false);
            Debug.Log("️ Оружие скрыто (не моё)");
        }
    }
    
    void Update()
    {
        // Диагностика при нажатии T
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("=== СТАТУС ОРУЖИЯ (T) ===");
            Debug.Log("photonView: " + (photonView != null));
            if (photonView != null) Debug.Log("IsMine: " + photonView.IsMine);
            Debug.Log("isEquipped: " + isEquipped);
            Debug.Log("fpsCam: " + (fpsCam != null));
            Debug.Log("currentAmmo: " + currentAmmo);
            Debug.Log("isReloading: " + isReloading);
            Debug.Log("gameObject.activeSelf: " + gameObject.activeSelf);
            Debug.Log("=========================");
        }
        
        // Проверка: если photonView есть и не наш - выходим
        if (photonView != null && !photonView.IsMine) return;
        
        if (!isEquipped) 
        {
            // Диагностика: если оружие не экипировано, но активно
            if (Input.GetMouseButton(0))
            {
                Debug.LogWarning("⚠️ ЛКМ нажата, но isEquipped = false!");
            }
            return;
        }
        
        if (fpsCam == null)
        {
            Debug.LogWarning("⚠️ fpsCam не назначен!");
            return;
        }
        
        // Восстановление отдачи
        currentRecoil = Mathf.Lerp(currentRecoil, 0f, Time.deltaTime * recoilRecovery);
        fpsCam.transform.localRotation *= Quaternion.Euler(-currentRecoil, 0, 0);
        
        // Стрельба
        if (Input.GetMouseButton(0) && !isReloading)
        {
            Debug.Log("🔫 ЛКМ зажата, вызываем Shoot()");
            Shoot();
        }
        
        // Перезарядка
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }
    
    public void Equip()
    {
        Debug.Log("=== EQUIP() ВЫЗВАН ===");
        Debug.Log("photonView: " + (photonView != null));
        if (photonView != null) Debug.Log("IsMine: " + photonView.IsMine);
        
        if (photonView != null && !photonView.IsMine) 
        {
            Debug.LogWarning("⚠️ Equip отменён: не моё оружие");
            return;
        }
        
        isEquipped = true;
        gameObject.SetActive(true);
        currentAmmo = maxAmmo;
        Debug.Log("✅ isEquipped = " + isEquipped + ", ammo = " + currentAmmo);
        Debug.Log("======================");
    }
    
    public void Unequip()
    {
        if (photonView != null && !photonView.IsMine) return;
        
        isEquipped = false;
        gameObject.SetActive(false);
    }
    
    void Shoot()
    {
        Debug.Log("=== SHOOT() ВЫЗВАН ===");
        Debug.Log("Time.time: " + Time.time + ", nextTimeToFire: " + nextTimeToFire);
        
        if (Time.time < nextTimeToFire) 
        {
            Debug.Log("⏳ Слишком рано, ждём...");
            return;
        }
        
        nextTimeToFire = Time.time + fireRate;
        
        if (currentAmmo <= 0)
        {
            if (gunAudio != null && emptySound != null)
                gunAudio.PlayOneShot(emptySound);
            Debug.Log("⚠️ Магазин пуст!");
            return;
        }
        
        currentAmmo--;
        Debug.Log("🔫 ВЫСТРЕЛ! Патронов: " + currentAmmo);
        
        Vector3 shootDirection = fpsCam.transform.forward;
        shootDirection.x += Random.Range(-spread, spread);
        shootDirection.y += Random.Range(-spread, spread);
        shootDirection.z += Random.Range(-spread, spread);
        shootDirection.Normalize();
        
        RaycastHit hit;
        Vector3 rayOrigin = barrelEnd != null ? barrelEnd.position : fpsCam.transform.position;
        
        Debug.DrawRay(rayOrigin, shootDirection * range, Color.red, 2f);
        
        if (Physics.Raycast(rayOrigin, shootDirection, out hit, range))
        {
            Debug.Log("🎯 Попали в: " + hit.transform.name);
            
            PlayerHealth target = hit.transform.GetComponent<PlayerHealth>();
            if (target == null)
                target = hit.transform.GetComponentInParent<PlayerHealth>();
            
            if (target != null)
            {
                target.photonView.RPC("RPC_TakeDamage", target.photonView.Owner, damage);
                Debug.Log("💥 Урон: " + damage);
            }
            
            if (impactEffect != null)
            {
                GameObject impactGO = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impactGO, 2f);
            }
        }
        else
        {
            Debug.Log("🌬️ Промах (луч ни во что не попал)");
        }
        
        if (muzzleFlash != null) muzzleFlash.Play();
        if (gunAudio != null && shootSound != null) gunAudio.PlayOneShot(shootSound);
        
        currentRecoil += recoilAmount;
        Debug.Log("======================");
    }
    
    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log(" Перезарядка...");
        
        if (gunAudio != null && reloadSound != null)
            gunAudio.PlayOneShot(reloadSound);
        
        yield return new WaitForSeconds(reloadTime);
        
        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log("✅ Перезаряжено! Патронов: " + currentAmmo);
    }
    
    void OnGUI()
    {
        if (photonView != null && !photonView.IsMine) return;
        if (!isEquipped) return;
        
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        
        GUI.Label(new Rect(10, 10, 400, 25), "Оружие: " + weaponName, style);
        GUI.Label(new Rect(10, 35, 400, 25), "Патроны: " + currentAmmo + " / " + maxAmmo, style);
        GUI.Label(new Rect(10, 60, 400, 25), "ЛКМ: " + Input.GetMouseButton(0), style);
        GUI.Label(new Rect(10, 85, 400, 25), "Камера: " + (fpsCam != null ? "OK" : "НЕТ!"), style);
        GUI.Label(new Rect(10, 110, 400, 25), "Экипировано: " + isEquipped, style);
    }
}