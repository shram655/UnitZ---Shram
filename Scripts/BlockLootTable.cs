using UnityEngine;
using System.Collections.Generic;

public class BlockLootTable : MonoBehaviour
{
    public List<LootEntry> lootTable = new List<LootEntry>();

    public void GenerateAndAddLoot(Move_Player player)
    {
        Debug.Log($"🎲 BlockLootTable.GenerateAndAddLoot START");

        if (player == null)
        {
            Debug.LogError("❌ player = null!");
            return;
        }

        if (lootTable == null || lootTable.Count == 0)
        {
            Debug.LogError("❌ lootTable пуст!");
            return;
        }

        // ✅ Выбираем только ОДИН случайный предмет
        LootEntry chosen = PickRandomEntry();
        if (chosen == null)
        {
            Debug.LogError("❌ Не удалось выбрать предмет!");
            return;
        }

        int count = Random.Range(chosen.minCount, chosen.maxCount + 1);
        Debug.Log($"✅ Выпал ОДИН предмет: {(chosen.isWeapon ? chosen.weaponName : chosen.itemName)} x{count}");

        if (chosen.isWeapon)
        {
            player.AddWeaponToInventory(
                chosen.weaponName, chosen.weaponDamage, chosen.weaponFireRate,
                chosen.weaponRange, chosen.weaponSpread, chosen.weaponMaxAmmo,
                chosen.weaponReloadTime, chosen.weaponRecoilAmount,
                chosen.weaponRecoilRecovery, null, null, null, null, null
            );

            ShowLootNotification(player, chosen.weaponName, 1, true, 0);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                player.AddBlockToInventory(chosen.blockId);
            }

            ShowLootNotification(player, chosen.itemName, count, false, chosen.blockId);
        }

        Debug.Log($"✅ GenerateAndAddLoot END");
    }

    LootEntry PickRandomEntry()
    {
        float total = 0f;
        foreach (var e in lootTable)
        {
            total += Mathf.Max(0f, e.dropChance);
        }

        if (total <= 0f)
        {
            return lootTable[Random.Range(0, lootTable.Count)];
        }

        float roll = Random.Range(0f, total);

        foreach (var e in lootTable)
        {
            roll -= Mathf.Max(0f, e.dropChance);
            if (roll <= 0f) return e;
        }

        return lootTable[lootTable.Count - 1];
    }

    void ShowLootNotification(Move_Player player, string itemName, int count, bool isWeapon, int blockId)
    {
        try
        {
            Sprite icon = null;

            if (isWeapon)
            {
                icon = player.weaponIcon;
            }
            else if (player.blockIcons != null && blockId >= 1 && blockId <= player.blockIcons.Length)
            {
                icon = player.blockIcons[blockId - 1];
            }

            // ✅ ИЩЕМ UI ПРЯМО В ИЕРАРХИИ ЛОКАЛЬНОГО ИГРОКА (гарантия для 2-го, 3-го и т.д.)
            LootNotificationUI ui = player.GetComponentInChildren<LootNotificationUI>(true);

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
            Debug.LogWarning($"⚠️ Не удалось показать уведомление (не критично): {e.Message}");
        }
    }
}