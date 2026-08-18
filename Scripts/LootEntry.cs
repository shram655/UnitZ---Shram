using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public string itemName;
    public float dropChance;
    public bool isWeapon;
    public bool isAmmo;
    public int blockId;
    public int minCount = 1;
    public int maxCount = 1;

    // 🆕 УНИКАЛЬНЫЙ ID типа оружия (1, 2, 3...)
    // В инвентаре будет храниться как -(100 + weaponId)
    public int weaponId = 1;

    public string weaponName;
    public int weaponDamage;
    public float weaponFireRate;
    public float weaponRange;
    public float weaponSpread;
    public int weaponMaxAmmo;
    public float weaponReloadTime;
    public float weaponRecoilAmount;
    public float weaponRecoilRecovery;
}