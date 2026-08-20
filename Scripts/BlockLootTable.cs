using UnityEngine;
using System.Collections.Generic;

public class BlockLootTable : MonoBehaviour
{
    public List<LootEntry> lootTable = new List<LootEntry>();

    // Флаг для блокировки уведомлений во время синхронизации
    public static bool IsSyncing = false;

    public void GenerateAndAddLoot(PlayerController pc)
    {
        if (pc == null) { Debug.LogError("❌ BLT: PlayerController = null!"); return; }
        if (lootTable == null || lootTable.Count == 0) { Debug.LogError("❌ BLT: lootTable пуст!"); return; }

        PlayerInventory playerInventory = pc.inventory;
        PlayerWeaponManager weaponManager = pc.weaponManager;

        if (playerInventory == null || weaponManager == null)
        {
            Debug.LogError("❌ BLT: PlayerInventory или PlayerWeaponManager не найдены!");
            return;
        }

        LootEntry chosen = PickRandomEntry();
        if (chosen == null) { Debug.LogError("❌ BLT: не удалось выбрать предмет!"); return; }

        int count = Random.Range(chosen.minCount, chosen.maxCount + 1);

        // ТОЧНОЕ имя того, что выпало
        string dropName = chosen.isWeapon ? chosen.weaponName
            : chosen.isMelee ? chosen.meleeName
            : chosen.isAmmo ? "Патроны"
            : chosen.itemName;

        Debug.Log($"✅ Выпал: {dropName} x{count}");

        if (chosen.isWeapon)
        {
            WeaponData data = new WeaponData
            {
                weaponId = chosen.weaponId,
                weaponName = chosen.weaponName,
                damage = chosen.weaponDamage,
                fireRate = chosen.weaponFireRate,
                range = chosen.weaponRange,
                spread = chosen.weaponSpread,
                maxAmmo = chosen.weaponMaxAmmo,
                reloadTime = chosen.weaponReloadTime,
                recoilAmount = chosen.weaponRecoilAmount,
                recoilRecovery = chosen.weaponRecoilRecovery,
                muzzleFlash = null,
                impactEffect = null,
                shootSound = null,
                reloadSound = null,
                emptySound = null
            };

            weaponManager.AddWeaponToInventory(chosen.weaponId, data);
            Debug.Log($"🎒 ЛУТ ВЫДАН: {dropName} x{count} (игрок {pc.nick.text})");
            ShowLootNotification(pc, dropName, count, true, 0, false, chosen.weaponId, false, 0);
        }
        else if (chosen.isMelee)
        {
            playerInventory.AddMeleeToInventory(chosen.meleeId);
            Debug.Log($"🎒 ЛУТ ВЫДАН: {dropName} x{count} (игрок {pc.nick.text})");
            ShowLootNotification(pc, dropName, count, false, 0, false, 0, true, chosen.meleeId);
        }
        else if (chosen.isAmmo)
        {
            playerInventory.AddAmmo(count);
            Debug.Log($"🎒 ЛУТ ВЫДАН: {dropName} x{count} (игрок {pc.nick.text})");
            ShowLootNotification(pc, dropName, count, false, 0, true, 0, false, 0);
        }
        else
        {
            if (chosen.blockId > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    playerInventory.AddToInventory(chosen.blockId);
                }
                Debug.Log($"🎒 ЛУТ ВЫДАН: {dropName} x{count} (игрок {pc.nick.text})");
            }
            else
            {
                Debug.LogWarning("⚠️ BLT: у записи лута blockId=0 — предмет не добавлен!");
            }
            ShowLootNotification(pc, dropName, count, false, chosen.blockId, false, 0, false, 0);
        }
    }

    LootEntry PickRandomEntry()
    {
        float total = 0f;
        foreach (var e in lootTable) total += Mathf.Max(0f, e.dropChance);
        if (total <= 0f) return lootTable[Random.Range(0, lootTable.Count)];

        float roll = Random.Range(0f, total);
        foreach (var e in lootTable)
        {
            roll -= Mathf.Max(0f, e.dropChance);
            if (roll <= 0f) return e;
        }
        return lootTable[lootTable.Count - 1];
    }

    // Уведомления через НЕЗАВИСИМЫЙ LootNotifier
    void ShowLootNotification(PlayerController pc, string itemName, int count, bool isWeapon, int blockId,
        bool isAmmo = false, int weaponId = 0, bool isMelee = false, int meleeId = 0)
    {
        try
        {
            Sprite icon = null;
            PlayerInventory inv = pc.inventory;

            if (isWeapon) icon = inv != null ? inv.GetWeaponIcon(weaponId) : null;
            else if (isMelee) icon = inv != null ? inv.GetMeleeIcon(meleeId) : null;
            else if (isAmmo) icon = inv != null ? inv.ammoIcon : null;
            else if (inv != null && inv.blockIcons != null && blockId >= 1 && blockId <= inv.blockIcons.Length)
                icon = inv.blockIcons[blockId - 1];

            LootNotifier.Show(icon, itemName, count);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Не удалось показать уведомление: {e.Message}");
        }
    }
}