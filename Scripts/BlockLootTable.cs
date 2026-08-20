using UnityEngine;
using System.Collections.Generic;

public class BlockLootTable : MonoBehaviour
{
    public List<LootEntry> lootTable = new List<LootEntry>();

    public void GenerateAndAddLoot(PlayerController pc)
    {
        if (pc == null) { Debug.LogError("❌ PlayerController = null!"); return; }
        if (lootTable == null || lootTable.Count == 0) { Debug.LogError("❌ lootTable пуст!"); return; }

        PlayerInventory playerInventory = pc.inventory;
        PlayerWeaponManager weaponManager = pc.weaponManager;

        if (playerInventory == null || weaponManager == null)
        {
            Debug.LogError("❌ PlayerInventory или PlayerWeaponManager не найдены!");
            return;
        }

        LootEntry chosen = PickRandomEntry();
        if (chosen == null) { Debug.LogError("❌ Не удалось выбрать предмет!"); return; }

        int count = Random.Range(chosen.minCount, chosen.maxCount + 1);
        Debug.Log($"✅ Выпал: {(chosen.isWeapon ? chosen.weaponName : chosen.isMelee ? chosen.meleeName : chosen.isAmmo ? "Патроны" : chosen.itemName)} x{count}");

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

            // 🆕 ИСПРАВЛЕНО: передаём chosen.weaponId (раньше было 0 — иконка не находилась)
            ShowLootNotification(pc, chosen.weaponName, 1, true, 0, false, chosen.weaponId, false, 0);
        }
        else if (chosen.isMelee)
        {
            playerInventory.AddMeleeToInventory(chosen.meleeId);
            ShowLootNotification(pc, chosen.meleeName, 1, false, 0, false, 0, true, chosen.meleeId);
        }
        else if (chosen.isAmmo)
        {
            playerInventory.AddAmmo(count);
            ShowLootNotification(pc, "Патроны", count, false, 0, true, 0, false, 0);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                playerInventory.AddToInventory(chosen.blockId);
            }
            ShowLootNotification(pc, chosen.itemName, count, false, chosen.blockId, false, 0, false, 0);
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

    void ShowLootNotification(PlayerController pc, string itemName, int count, bool isWeapon, int blockId,
        bool isAmmo = false, int weaponId = 0, bool isMelee = false, int meleeId = 0)
    {
        try
        {
            Sprite icon = null;
            PlayerInventory inv = pc.inventory;

            if (isWeapon)
            {
                icon = inv != null ? inv.GetWeaponIcon(weaponId) : null;
            }
            else if (isMelee)
            {
                icon = inv != null ? inv.GetMeleeIcon(meleeId) : null;
            }
            else if (isAmmo)
            {
                icon = inv != null ? inv.ammoIcon : null;
            }
            else if (inv != null && inv.blockIcons != null && blockId >= 1 && blockId <= inv.blockIcons.Length)
            {
                icon = inv.blockIcons[blockId - 1];
            }

            LootNotificationUI ui = pc.GetComponentInChildren<LootNotificationUI>(true);
            if (ui == null) ui = LootNotificationUI.Instance;
            if (ui == null) ui = Object.FindObjectOfType<LootNotificationUI>(true);
            if (ui == null)
            {
                GameObject uiObj = new GameObject("LootNotificationUI");
                ui = uiObj.AddComponent<LootNotificationUI>();
            }

            ui.ShowNotification(icon, itemName, count);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Не удалось показать уведомление: {e.Message}");
        }
    }
}