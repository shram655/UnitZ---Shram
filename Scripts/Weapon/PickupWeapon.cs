using UnityEngine;
using Photon.Pun;

public class PickupWeapon : MonoBehaviourPun
{
    [Header("Настройки оружия")]
    public string weaponName = "Автомат";
    public int damage = 15;
    public float fireRate = 0.08f;
    public float range = 100f;
    public float spread = 0.015f;
    public int maxAmmo = 30;
    public float reloadTime = 2f;
    public float recoilAmount = 0.8f;
    public float recoilRecovery = 5f;
    
    [Header("Эффекты")]
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
    
    [Header("Визуал")]
    public GameObject weaponModel;
    
    private bool isPickedUp = false;
    
    void Start()
    {
        if (photonView != null && !photonView.IsMine)
        {
            // Можно сделать полупрозрачным
        }
    }
    
    public void PickUp(GameObject player)
    {
        if (isPickedUp) return;
        
        isPickedUp = true;
        
        PlayerWeaponManager weaponManager = player.GetComponent<PlayerWeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.AddWeaponToInventory(
                weaponName,
                damage,
                fireRate,
                range,
                spread,
                maxAmmo,
                reloadTime,
                recoilAmount,
                recoilRecovery,
                muzzleFlash,
                impactEffect,
                shootSound,
                reloadSound,
                emptySound
            );
        }
        
        if (photonView != null && photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public static GameObject DropWeapon(
        Vector3 position,
        Quaternion rotation,
        string weaponName,
        int damage,
        float fireRate,
        float range,
        float spread,
        int maxAmmo,
        float reloadTime,
        float recoilAmount,
        float recoilRecovery,
        ParticleSystem muzzleFlash,
        GameObject impactEffect,
        AudioClip shootSound,
        AudioClip reloadSound,
        AudioClip emptySound,
        GameObject weaponModelPrefab
    )
    {
        GameObject droppedWeapon = new GameObject("PickupWeapon_" + weaponName);
        droppedWeapon.transform.position = position;
        droppedWeapon.transform.rotation = rotation;
        
        Rigidbody rb = droppedWeapon.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 1f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = true;
        
        BoxCollider collider = droppedWeapon.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.5f, 1f, 1.5f);
        
        PhotonView photonView = droppedWeapon.AddComponent<PhotonView>();
        
        PickupWeapon pickup = droppedWeapon.AddComponent<PickupWeapon>();
        
        WeaponDropper dropper = droppedWeapon.AddComponent<WeaponDropper>();
        
        pickup.weaponName = weaponName;
        pickup.damage = damage;
        pickup.fireRate = fireRate;
        pickup.range = range;
        pickup.spread = spread;
        pickup.maxAmmo = maxAmmo;
        pickup.reloadTime = reloadTime;
        pickup.recoilAmount = recoilAmount;
        pickup.recoilRecovery = recoilRecovery;
        pickup.muzzleFlash = muzzleFlash;
        pickup.impactEffect = impactEffect;
        pickup.shootSound = shootSound;
        pickup.reloadSound = reloadSound;
        pickup.emptySound = emptySound;
        
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(droppedWeapon.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = new Vector3(0.5f, 0.5f, 1.5f);
        Destroy(model.GetComponent<Collider>());
        pickup.weaponModel = model;
        
        dropper.StartDrop(rb);
        
        Debug.Log("🗑️ Оружие выброшено на землю: " + weaponName);
        
        return droppedWeapon;
    }
}