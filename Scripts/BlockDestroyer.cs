using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class BlockDestroyer : MonoBehaviourPun
{
    private BlockLootTable lootTable;
    private BlockInfo blockInfo;
    private WorldManager worldManager;
    private bool isDestroyed = false;

    void Start()
    {
        lootTable = GetComponent<BlockLootTable>();
        blockInfo = GetComponent<BlockInfo>();
        worldManager = FindObjectOfType<WorldManager>();
        
        Debug.Log($"✅ BlockDestroyer Start на {gameObject.name}");
        Debug.Log($"  lootTable: {(lootTable != null ? "OK" : "NULL")}");
        
        if (lootTable != null)
        {
            Debug.Log($"  lootTable.lootTable.Count: {lootTable.lootTable.Count}");
        }
    }

    public void RequestDestroy(PlayerInventory playerInventory)
    {
        if (playerInventory == null)
        {
            Debug.LogError("❌ BlockDestroyer: playerInventory = null!");
            return;
        }

        PlayerController pc = playerInventory.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("❌ BlockDestroyer: PlayerController не найден на игроке!");
            return;
        }

        if (isDestroyed)
        {
            Debug.LogWarning("⚠️ Блок уже разрушен!");
            return;
        }
        isDestroyed = true;

        Debug.Log($"🔨 RequestDestroy START. Игрок: {pc.nick.text}");

        // ✅ ШАГ 1: Добавляем лут (в защищённом блоке — НЕ может сломать разрушение)
        try
        {
            if (lootTable != null && lootTable.lootTable != null && lootTable.lootTable.Count > 0)
            {
                Debug.Log($"🎲 Вызываем GenerateAndAddLoot для: {pc.nick.text}");
                lootTable.GenerateAndAddLoot(pc);
                Debug.Log("✅ GenerateAndAddLoot вызван успешно");
            }
            else if (blockInfo != null)
            {
                Debug.Log($"🧱 Добавляем обычный блок ID={blockInfo.blockId}");
                playerInventory.AddToInventory(blockInfo.blockId);
            }
            else
            {
                Debug.LogError($"❌ Блок с лутом, но lootTable пуст!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка при выдаче лута (блок всё равно будет разрушен): {e.Message}");
        }

        // ✅ ШАГ 2: Уведомляем WorldManager (выполняется ВСЕГДА)
        Vector3 blockPosition = transform.position;
        
        if (worldManager != null)
        {
            worldManager.DestroyBlock(blockPosition, pc.nick.text);
        }

        // ✅ ШАГ 3: Отправляем RPC всем (выполняется ВСЕГДА)
        if (pc.view != null && PhotonNetwork.IsConnected)
        {
            int x = Mathf.RoundToInt(blockPosition.x * 100);
            int y = Mathf.RoundToInt(blockPosition.y * 100);
            int z = Mathf.RoundToInt(blockPosition.z * 100);
            
            pc.view.RPC("RPC_BlockDestroyed", RpcTarget.All, x, y, z);
        }

        // ✅ ШАГ 4: Уничтожаем блок (выполняется ВСЕГДА)
        Debug.Log($"💥 Уничтожаем блок локально через Destroy()");
        Destroy(gameObject);
        
        Debug.Log($"🔨 RequestDestroy END");
    }
}