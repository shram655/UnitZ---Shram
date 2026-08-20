using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Globalization;
using System.Collections;

public class WorldManager : MonoBehaviourPunCallbacks
{
    [Header("Настройки")]
    public GameObject[] blockPrefabs;

    [Header("Ссылки")]
    public Transform worldParent;

    private Dictionary<string, GameObject> activeBlocks = new Dictionary<string, GameObject>();
    private HashSet<string> processedDestroyKeys = new HashSet<string>();
    private bool isWorldSynced = false;

    private const int POSITION_MULTIPLIER = 100;

    public static WorldManager Instance;

    // Очередь респавнов (только у мастера тикает)
    private List<PendingRespawn> pendingRespawns = new List<PendingRespawn>();

    [System.Serializable]
    public class PendingRespawn
    {
        public int blockId;
        public Vector3 position;
        public float timeRemaining;
        public string key;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (worldParent == null)
        {
            GameObject worldObj = new GameObject("WorldBlocks");
            worldParent = worldObj.transform;
        }

        if (photonView == null)
        {
            Debug.LogError("❌ WorldManager не имеет PhotonView!");
            return;
        }

        Debug.Log($"✅ WorldManager Start. IsMasterClient: {PhotonNetwork.IsMasterClient}");

        if (PhotonNetwork.IsMasterClient)
        {
            ScanExistingBlocks();
            DeduplicateActiveBlocks();
            isWorldSynced = true;
            Debug.Log($"✅ Мастер. Активных блоков: {activeBlocks.Count}");
        }
        else
        {
            Debug.Log("🔄 Не мастер, запрашиваем синхронизацию...");
            Invoke(nameof(RequestWorldSync), 2.0f);
        }

        StartCoroutine(UpdateRespawns());
    }

    // ═════════════════════════════════════════════════════
    // 🆕 РЕГИСТРАЦИЯ ЖИВОГО БЛОКА (вызывается каждым клиентом,
    // когда блок появился в мире — респавн, синхронизация и т.д.)
    // ═════════════════════════════════════════════════════
    public void RegisterAliveBlock(GameObject block, Vector3 position)
    {
        if (block == null) return;

        string key = GetBlockKey(position.x, position.y, position.z);

        // 🆕 Блок снова живой — убираем его из "уже обработанных разрушений"
        if (processedDestroyKeys.Remove(key))
        {
            Debug.Log($"♻️ Ключ {key} убран из processedDestroyKeys — блок снова живой");
        }

        if (!activeBlocks.ContainsKey(key))
        {
            activeBlocks.Add(key, block);
        }
    }

    // ═════════════════════════════════════════════════════
    // ПЛАНИРОВАНИЕ РЕСПАВНА БЛОКА (сундука)
    // ═════════════════════════════════════════════════════
    public void ScheduleBlockRespawn(int blockId, Vector3 position, float delay)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length)
        {
            Debug.LogWarning($"⚠️ ScheduleBlockRespawn: некорректный blockId={blockId}");
            return;
        }

        string key = GetBlockKey(position.x, position.y, position.z);

        // Проверка дубликатов в очереди
        foreach (var p in pendingRespawns)
        {
            if (p.key == key)
            {
                Debug.Log($"⚠️ Респавн уже запланирован для {key}, пропускаем");
                return;
            }
        }

        if (PhotonNetwork.IsMasterClient)
        {
            pendingRespawns.Add(new PendingRespawn
            {
                blockId = blockId,
                position = position,
                timeRemaining = delay,
                key = key
            });
            Debug.Log($"⏳ Мастер: сундук #{blockId} заспавнится через {delay}с на {position}");
        }
        else
        {
            photonView.RPC("RPC_ScheduleRespawn", RpcTarget.MasterClient, blockId,
                Mathf.RoundToInt(position.x * 100),
                Mathf.RoundToInt(position.y * 100),
                Mathf.RoundToInt(position.z * 100),
                delay);
        }
    }

    [PunRPC]
    void RPC_ScheduleRespawn(int blockId, int x, int y, int z, float delay, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Vector3 pos = new Vector3(x / 100f, y / 100f, z / 100f);
        ScheduleBlockRespawn(blockId, pos, delay);
        Debug.Log($"✅ Мастер получил запрос респавна от {info.Sender.NickName}");
    }

    IEnumerator UpdateRespawns()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            if (!PhotonNetwork.IsMasterClient) continue;

            List<PendingRespawn> toRemove = new List<PendingRespawn>();

            foreach (var r in pendingRespawns)
            {
                r.timeRemaining -= 1f;

                if (r.timeRemaining <= 0f)
                {
                    // Защита: если блок уже появился — пропускаем
                    if (!activeBlocks.ContainsKey(r.key) && !HasBlockNear(r.position))
                    {
                        SpawnBlockNetworked(r.blockId, r.position);
                        Debug.Log($"✅ Респавн: блок #{r.blockId} появился на {r.position}");
                    }
                    else
                    {
                        Debug.Log($"⚠️ Респавн пропущен — блок уже есть на {r.position}");
                    }
                    toRemove.Add(r);
                }
            }

            foreach (var r in toRemove) pendingRespawns.Remove(r);
        }
    }

    void SpawnBlockNetworked(int blockId, Vector3 position)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length) return;

        GameObject prefab = blockPrefabs[blockId - 1];
        if (prefab == null)
        {
            Debug.LogError($"❌ Префаб с blockId={blockId} не найден в blockPrefabs!");
            return;
        }

        GameObject blockObj = PhotonNetwork.Instantiate(prefab.name, position, Quaternion.identity);
        blockObj.transform.SetParent(worldParent);

        // 🆕 Регистрируем живой блок (убирает ключ из processedDestroyKeys)
        RegisterAliveBlock(blockObj, position);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"👑 Смена мастера! Новый мастер: {newMasterClient.NickName}");
        if (PhotonNetwork.IsMasterClient)
        {
            DeduplicateActiveBlocks();
            isWorldSynced = true;
            Debug.Log($"✅ Я теперь мастер. Активных блоков: {activeBlocks.Count}");
        }
    }

    void ScanExistingBlocks()
    {
        activeBlocks.Clear();
        PlacedBlock[] placedBlocks = FindObjectsOfType<PlacedBlock>();
        BlockDestroyer[] lootBlocks = FindObjectsOfType<BlockDestroyer>();

        foreach (PlacedBlock pb in placedBlocks)
        {
            if (pb == null) continue;
            string key = GetBlockKey(pb.transform.position.x, pb.transform.position.y, pb.transform.position.z);
            if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, pb.gameObject);
        }

        foreach (BlockDestroyer bd in lootBlocks)
        {
            if (bd == null) continue;
            string key = GetBlockKey(bd.transform.position.x, bd.transform.position.y, bd.transform.position.z);
            if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, bd.gameObject);
        }

        Debug.Log($"📊 Найдено блоков: {activeBlocks.Count}");
    }

    void DeduplicateActiveBlocks()
    {
        List<string> keys = new List<string>(activeBlocks.Keys);
        List<string> toRemove = new List<string>();

        for (int i = 0; i < keys.Count; i++)
        {
            if (toRemove.Contains(keys[i])) continue;
            if (!activeBlocks.ContainsKey(keys[i]) || activeBlocks[keys[i]] == null) continue;

            Vector3 posI = activeBlocks[keys[i]].transform.position;

            for (int j = i + 1; j < keys.Count; j++)
            {
                if (toRemove.Contains(keys[j])) continue;
                if (!activeBlocks.ContainsKey(keys[j]) || activeBlocks[keys[j]] == null) continue;

                Vector3 posJ = activeBlocks[keys[j]].transform.position;
                if (Vector3.Distance(posI, posJ) < 0.5f)
                {
                    toRemove.Add(keys[j]);
                }
            }
        }

        foreach (string key in toRemove)
        {
            if (activeBlocks.ContainsKey(key))
            {
                if (activeBlocks[key] != null) Destroy(activeBlocks[key]);
                activeBlocks.Remove(key);
            }
        }

        if (toRemove.Count > 0)
            Debug.Log($"🧹 Удалено дубликатов блоков: {toRemove.Count}");
    }

    bool HasBlockNear(Vector3 position)
    {
        foreach (var kvp in activeBlocks)
        {
            if (kvp.Value == null) continue;
            if (Vector3.Distance(kvp.Value.transform.position, position) < 0.5f) return true;
        }
        return false;
    }

    void RequestWorldSync()
    {
        if (PhotonNetwork.IsMasterClient || isWorldSynced) return;
        if (photonView == null) return;
        photonView.RPC("RequestFullWorldSync", PhotonNetwork.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        Invoke(nameof(ForceSync), 5.0f);
    }

    void ForceSync()
    {
        if (!isWorldSynced) isWorldSynced = true;
    }

    [PunRPC]
    void RequestFullWorldSync(int requesterActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Player targetPlayer = PhotonNetwork.CurrentRoom.GetPlayer(requesterActorNumber);
        if (targetPlayer == null) return;
        SendWorldSync(targetPlayer);
    }

    void SendWorldSync(Player targetPlayer)
    {
        List<int> posX = new List<int>();
        List<int> posY = new List<int>();
        List<int> posZ = new List<int>();
        List<int> blockIds = new List<int>();
        List<bool> isLoots = new List<bool>();
        List<int> lootCounts = new List<int>();
        List<string> lootNames = new List<string>();
        List<float> lootChances = new List<float>();
        List<bool> lootWeapons = new List<bool>();
        List<int> lootBlockIds = new List<int>();
        List<int> lootMins = new List<int>();
        List<int> lootMaxs = new List<int>();

        foreach (var kvp in activeBlocks)
        {
            if (kvp.Value == null) continue;
            Vector3 pos = kvp.Value.transform.position;
            int blockId = 1;
            PlacedBlock pb = kvp.Value.GetComponent<PlacedBlock>();
            if (pb != null) blockId = pb.blockId;

            bool isLoot = kvp.Value.GetComponent<BlockDestroyer>() != null;

            posX.Add(Mathf.RoundToInt(pos.x * POSITION_MULTIPLIER));
            posY.Add(Mathf.RoundToInt(pos.y * POSITION_MULTIPLIER));
            posZ.Add(Mathf.RoundToInt(pos.z * POSITION_MULTIPLIER));
            blockIds.Add(blockId);
            isLoots.Add(isLoot);

            if (isLoot)
            {
                BlockLootTable lt = kvp.Value.GetComponent<BlockLootTable>();
                if (lt != null && lt.lootTable != null && lt.lootTable.Count > 0)
                {
                    lootCounts.Add(lt.lootTable.Count);
                    foreach (var e in lt.lootTable)
                    {
                        lootNames.Add(e.itemName ?? "");
                        lootChances.Add(e.dropChance);
                        lootWeapons.Add(e.isWeapon);
                        lootBlockIds.Add(e.blockId);
                        lootMins.Add(e.minCount);
                        lootMaxs.Add(e.maxCount);
                    }
                }
                else lootCounts.Add(0);
            }
            else lootCounts.Add(0);
        }

        Debug.Log($"📤 Отправляю {blockIds.Count} живых блоков игроку {targetPlayer.NickName}");

        photonView.RPC("RPC_FullWorldSync", targetPlayer,
            posX.ToArray(), posY.ToArray(), posZ.ToArray(),
            blockIds.ToArray(), isLoots.ToArray(),
            lootCounts.ToArray(), lootNames.ToArray(), lootChances.ToArray(),
            lootWeapons.ToArray(), lootBlockIds.ToArray(), lootMins.ToArray(), lootMaxs.ToArray());
    }

    [PunRPC]
    void RPC_FullWorldSync(int[] posX, int[] posY, int[] posZ, int[] blockIds, bool[] isLoots,
        int[] lootCounts, string[] lootNames, float[] lootChances, bool[] lootWeapons,
        int[] lootBlockIds, int[] lootMins, int[] lootMaxs)
    {
        Debug.Log($"🌍 Получена синхронизация: {blockIds.Length} блоков");

        ClearAllBlocks();

        int lootIndex = 0;
        for (int i = 0; i < blockIds.Length; i++)
        {
            Vector3 position = new Vector3(
                posX[i] / (float)POSITION_MULTIPLIER,
                posY[i] / (float)POSITION_MULTIPLIER,
                posZ[i] / (float)POSITION_MULTIPLIER);

            string key = GetBlockKey(position.x, position.y, position.z);
            if (processedDestroyKeys.Contains(key)) continue;

            CreateBlockFromSync(blockIds[i], position, isLoots[i], lootCounts[i], ref lootIndex,
                lootNames, lootChances, lootWeapons, lootBlockIds, lootMins, lootMaxs);
        }

        isWorldSynced = true;
        Debug.Log($"✅ Мир синхронизирован. Активных: {activeBlocks.Count}");
    }

    void ClearAllBlocks()
    {
        PlacedBlock[] allPlaced = FindObjectsOfType<PlacedBlock>();
        foreach (PlacedBlock pb in allPlaced)
            if (pb != null && pb.gameObject != null) Destroy(pb.gameObject);

        BlockDestroyer[] allLoot = FindObjectsOfType<BlockDestroyer>();
        foreach (BlockDestroyer bd in allLoot)
            if (bd != null && bd.gameObject != null) Destroy(bd.gameObject);

        activeBlocks.Clear();
    }

    void CreateBlockFromSync(int blockId, Vector3 position, bool isLoot, int lootCount, ref int lootIndex,
        string[] lootNames, float[] lootChances, bool[] lootWeapons, int[] lootBlockIds, int[] lootMins, int[] lootMaxs)
    {
        if (blockId < 1 || blockId > blockPrefabs.Length) return;
        if (HasBlockNear(position)) return;

        GameObject blockObj = Instantiate(blockPrefabs[blockId - 1], position, Quaternion.identity, worldParent);
        blockObj.name = "Block_" + position.x + "_" + position.y + "_" + position.z;

        string key = GetBlockKey(position.x, position.y, position.z);

        PlacedBlock pb = blockObj.GetComponent<PlacedBlock>();
        if (pb == null) pb = blockObj.AddComponent<PlacedBlock>();
        pb.blockId = blockId;

        if (isLoot)
        {
            BlockLootTable lootTable = blockObj.GetComponent<BlockLootTable>();
            if (lootTable == null) lootTable = blockObj.AddComponent<BlockLootTable>();
            lootTable.lootTable = new List<LootEntry>();

            for (int j = 0; j < lootCount; j++)
            {
                lootTable.lootTable.Add(new LootEntry
                {
                    itemName = lootNames[lootIndex],
                    dropChance = lootChances[lootIndex],
                    isWeapon = lootWeapons[lootIndex],
                    blockId = lootBlockIds[lootIndex],
                    minCount = lootMins[lootIndex],
                    maxCount = lootMaxs[lootIndex]
                });
                lootIndex++;
            }

            BlockInfo bi = blockObj.GetComponent<BlockInfo>();
            if (bi == null) bi = blockObj.AddComponent<BlockInfo>();
            bi.blockId = blockId;

            if (blockObj.GetComponent<BlockDestroyer>() == null)
                blockObj.AddComponent<BlockDestroyer>();
        }

        PhotonView pv = blockObj.GetComponent<PhotonView>();
        if (pv != null && !isLoot) Destroy(pv);

        if (!activeBlocks.ContainsKey(key)) activeBlocks.Add(key, blockObj);
    }

    // ═════════════════════════════════════════════════════
    // РАЗРУШЕНИЕ БЛОКА — 🆕 ИСПРАВЛЕНО
    // ═════════════════════════════════════════════════════
    public void HandleBlockDestroyed(Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (processedDestroyKeys.Contains(key))
        {
            Debug.Log($"⚠️ HandleBlockDestroyed: ключ {key} уже обработан, пропускаем");
            return;
        }
        processedDestroyKeys.Add(key);

        Debug.Log($"🔍 HandleBlockDestroyed: позиция={position}, ключ={key}");

        GameObject blockToRemove = null;
        int blockId = -1;
        float respawnTime = 0f;
        bool enableRespawn = false;

        if (activeBlocks.ContainsKey(key))
        {
            blockToRemove = activeBlocks[key];

            Debug.Log($"📦 Найден блок по ключу: {(blockToRemove != null ? blockToRemove.name : "NULL")}");

            if (blockToRemove != null)
            {
                LootChestRespawner respawner = blockToRemove.GetComponent<LootChestRespawner>();
                BlockInfo bi = blockToRemove.GetComponent<BlockInfo>();

                if (respawner != null && respawner.enableRespawn && bi != null)
                {
                    blockId = bi.blockId;
                    respawnTime = respawner.respawnTime;
                    enableRespawn = true;
                    Debug.Log($"🎯 Сундук будет зареспавнен через {respawnTime}с (blockId={blockId})");
                }
                else
                {
                    Debug.Log($"ℹ️ Респавн не нужен: respawner={(respawner != null)}, bi={(bi != null)}");
                }
            }

            activeBlocks.Remove(key);
            Debug.Log($"✅ Блок удалён из activeBlocks: {key}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Блок не найден в activeBlocks по ключу {key}, ищу по дистанции...");

            foreach (var kvp in activeBlocks)
            {
                if (kvp.Value == null) continue;
                if (Vector3.Distance(kvp.Value.transform.position, position) < 0.1f)
                {
                    blockToRemove = kvp.Value;
                    Debug.Log($"📦 Найден блок по дистанции: {blockToRemove.name}");

                    LootChestRespawner respawner = blockToRemove.GetComponent<LootChestRespawner>();
                    BlockInfo bi = blockToRemove.GetComponent<BlockInfo>();

                    if (respawner != null && respawner.enableRespawn && bi != null)
                    {
                        blockId = bi.blockId;
                        respawnTime = respawner.respawnTime;
                        enableRespawn = true;
                        Debug.Log($"🎯 Сундук будет зареспавнен через {respawnTime}с (blockId={blockId})");
                    }

                    activeBlocks.Remove(kvp.Key);
                    Debug.Log($"✅ Блок удалён из activeBlocks: {kvp.Key}");
                    break;
                }
            }
        }

        if (blockToRemove != null)
        {
            Destroy(blockToRemove);
            Debug.Log($"💥 Блок уничтожен: {blockToRemove.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ blockToRemove = null, нечего уничтожать!");
        }

        if (enableRespawn && blockId > 0)
        {
            Debug.Log($"📡 Вызываю ScheduleBlockRespawn для blockId={blockId}, position={position}, delay={respawnTime}");
            ScheduleBlockRespawn(blockId, position, respawnTime);
        }
        else
        {
            Debug.Log($"ℹ️ Респавн НЕ запланирован: enableRespawn={enableRespawn}, blockId={blockId}");
        }
    }

    public void HandleBlockPlaced(int blockId, Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (activeBlocks.ContainsKey(key)) return;

        if (HasBlockNear(position))
        {
            Debug.Log($"⚠️ ДЮП ЗАБЛОКИРОВАН: рядом уже есть блок, позиция {position}");
            return;
        }

        if (blockId < 1 || blockId > blockPrefabs.Length) return;

        // 🆕 Блок снова живой — убираем ключ из "обработанных разрушений"
        processedDestroyKeys.Remove(key);

        GameObject blockObj = Instantiate(blockPrefabs[blockId - 1], position, Quaternion.identity, worldParent);
        blockObj.name = "Block_" + position.x + "_" + position.y + "_" + position.z;

        PlacedBlock pb = blockObj.AddComponent<PlacedBlock>();
        pb.blockId = blockId;

        PhotonView pv = blockObj.GetComponent<PhotonView>();
        if (pv != null) Destroy(pv);

        activeBlocks.Add(key, blockObj);
        Debug.Log($"✅ Блок установлен: {key}");
    }

    public void PlaceBlock(int blockId, Vector3 position)
    {
        string key = GetBlockKey(position.x, position.y, position.z);
        if (activeBlocks.ContainsKey(key) || HasBlockNear(position))
        {
            Debug.Log($"⚠️ Блок уже существует рядом с {position}");
            return;
        }
        HandleBlockPlaced(blockId, position);
    }

    public void DestroyBlock(Vector3 position, string playerNickname = "")
    {
        HandleBlockDestroyed(position);
    }

    string GetBlockKey(float x, float y, float z)
    {
        return x.ToString("F2", CultureInfo.InvariantCulture) + "|" +
               y.ToString("F2", CultureInfo.InvariantCulture) + "|" +
               z.ToString("F2", CultureInfo.InvariantCulture);
    }

    void OnGUI()
    {
        GUILayout.Label("Активных блоков: " + activeBlocks.Count);
        GUILayout.Label("Мир синхронизирован: " + isWorldSynced);
    }
}