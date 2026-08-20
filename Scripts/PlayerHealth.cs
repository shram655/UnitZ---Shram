using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Настройки здоровья")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Точки респавна")]
    public bool useSpawnPointsForRespawn = true;
    public string spawnPointTag = "SpawnPoint";
    private Transform[] respawnPoints;

    [Header("UI элементы")]
    public Slider healthBar;
    public TextMeshProUGUI healthText;
    public GameObject healthPanel;
    public Image damageFlash;
    public float flashDuration = 0.3f;

    [Header("UI смерти")]
    public GameObject deathPanel;
    public TextMeshProUGUI deathText;
    public TextMeshProUGUI respawnTimerText;

    [Header("Анимация")]
    public float smoothSpeed = 5f;
    private float displayHealth;

    [Header("Звуки")]
    public AudioClip damageSound;
    public AudioClip deathSound;
    private AudioSource audioSource;

    [Header("Настройки респавна")]
    public float respawnDelay = 3f;
    private bool isDead = false;
    private float respawnCountdown = 0f;
    private bool isRespawning = false;

    private List<GameObject> hiddenObjects = new List<GameObject>();

    void Start()
    {
        currentHealth = maxHealth;
        displayHealth = maxHealth;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = maxHealth;
        }
        UpdateHealthUI();

        if (deathPanel != null) deathPanel.SetActive(false);

        if (!photonView.IsMine)
        {
            if (healthPanel != null) healthPanel.SetActive(false);
            if (deathPanel != null) deathPanel.SetActive(false);
            return;
        }

        FindRespawnPoints();
        Debug.Log("❤️ PlayerHealth Start() | Max HP: " + maxHealth);
    }

    void FindRespawnPoints()
    {
        if (useSpawnPointsForRespawn)
        {
            GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag(spawnPointTag);
            if (spawnObjects.Length > 0)
            {
                respawnPoints = new Transform[spawnObjects.Length];
                for (int i = 0; i < spawnObjects.Length; i++)
                    respawnPoints[i] = spawnObjects[i].transform;
                Debug.Log("✅ Найдено точек спавна: " + respawnPoints.Length);
            }
            else
            {
                Debug.LogError("⚠️ Не найдено объектов с тегом '" + spawnPointTag + "'!");
                respawnPoints = new Transform[0];
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // 🆕 БЛОКИРОВКА ВВОДА ВО ВРЕМЯ ЧАТА (U = мгновенная смерть)
        if (!ChatManager.IsChatOpen && Input.GetKeyDown(KeyCode.U) && !isDead)
        {
            Debug.Log("⚠️ Нажата клавиша U — мгновенная смерть!");
            Die();
            return;
        }

        // Плавная анимация полоски (работает всегда)
        if (healthBar != null && displayHealth != currentHealth)
        {
            displayHealth = Mathf.Lerp(displayHealth, currentHealth, Time.deltaTime * smoothSpeed);
            healthBar.value = displayHealth;
        }

        // Обратный отсчёт респавна (работает всегда)
        if (isRespawning)
        {
            respawnCountdown -= Time.deltaTime;
            if (respawnTimerText != null)
                respawnTimerText.text = "Респавн через: " + Mathf.Ceil(respawnCountdown).ToString();

            if (respawnCountdown <= 0f) Respawn();
        }
    }

    [PunRPC]
    public void RPC_TakeDamage(float damage) { TakeDamage(damage); }

    public void TakeDamage(float damage)
    {
        if (!photonView.IsMine) return;
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        Debug.Log("💥 Урон: " + damage + ", HP: " + currentHealth + "/" + maxHealth);

        if (damageSound != null && audioSource != null) audioSource.PlayOneShot(damageSound);
        if (damageFlash != null) StartCoroutine(ShowDamageFlash());

        UpdateHealthUI();
        if (currentHealth <= 0) Die();
    }

    IEnumerator ShowDamageFlash()
    {
        if (damageFlash != null)
        {
            damageFlash.gameObject.SetActive(true);
            Color flashColor = damageFlash.color;
            flashColor.a = 0.5f;
            damageFlash.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            flashColor.a = 0f;
            damageFlash.color = flashColor;
            damageFlash.gameObject.SetActive(false);
        }
    }

    void UpdateHealthUI()
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth;
            if (healthBar.fillRect != null && healthBar.fillRect.GetComponent<Image>() != null)
            {
                if (currentHealth > maxHealth * 0.6f)
                    healthBar.fillRect.GetComponent<Image>().color = Color.green;
                else if (currentHealth > maxHealth * 0.3f)
                    healthBar.fillRect.GetComponent<Image>().color = Color.yellow;
                else
                    healthBar.fillRect.GetComponent<Image>().color = Color.red;
            }
        }

        if (healthText != null)
            healthText.text = Mathf.Ceil(currentHealth).ToString() + " / " + Mathf.Ceil(maxHealth).ToString();
    }

    bool ContainsUIPanel(GameObject obj)
    {
        if (obj == deathPanel || obj == healthPanel) return true;
        foreach (Transform child in obj.transform)
            if (child.gameObject == deathPanel || child.gameObject == healthPanel) return true;
        return false;
    }

    void HidePlayer()
    {
        Debug.Log("👻 Скрываем всего игрока...");
        hiddenObjects.Clear();

        foreach (Transform child in transform)
        {
            if (ContainsUIPanel(child.gameObject)) continue;
            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                hiddenObjects.Add(child.gameObject);
            }
        }

        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
            if (mr != null) mr.enabled = false;
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            if (smr != null) smr.enabled = false;
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Debug.Log("✅ Игрок полностью скрыт (включая Mesh Renderer)!");
    }

    void ShowPlayer()
    {
        Debug.Log("✅ Показываем игрока...");
        foreach (GameObject obj in hiddenObjects)
            if (obj != null) obj.SetActive(true);
        hiddenObjects.Clear();

        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
            if (mr != null) mr.enabled = true;
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            if (smr != null) smr.enabled = true;
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = true;

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = true;

        Debug.Log("✅ Игрок восстановлен (включая Mesh Renderer)!");
    }

    void ClearInventory()
    {
        Debug.Log("🗑️ Очищаем инвентарь...");
        PlayerInventory playerInv = GetComponent<PlayerInventory>();
        if (playerInv == null) return;

        for (int i = 0; i < 20; i++)
        {
            playerInv.inventory[i] = 0;
            playerInv.inventoryCounts[i] = 0;
        }
        playerInv.UpdateHotbarUI();
        if (playerInv.inventoryUI != null) playerInv.inventoryUI.UpdateAllSlots();
        Debug.Log("✅ Инвентарь очищен!");
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isRespawning = true;
        respawnCountdown = respawnDelay;

        Debug.Log("💀 Игрок погиб! Респавн через " + respawnDelay + " сек");
        if (deathSound != null && audioSource != null) audioSource.PlayOneShot(deathSound);

        ClearInventory();
        HidePlayer();

        if (deathPanel != null && photonView.IsMine)
        {
            deathPanel.SetActive(true);
            Debug.Log("✅ DeathPanel показан!");
        }

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.SetDead(true);
    }

    void Respawn()
    {
        if (!photonView.IsMine) return;

        Debug.Log("========================================");
        Debug.Log("✅ Респавн игрока...");
        Debug.Log("Текущая позиция: " + transform.position);

        Transform respawnPoint = GetRandomRespawnPoint();
        if (respawnPoint != null)
        {
            Vector3 newPosition = respawnPoint.position;
            Quaternion newRotation = respawnPoint.rotation;
            Debug.Log("🎯 Телепортация на точку: " + respawnPoint.name);
            transform.position = newPosition;
            transform.rotation = newRotation;
        }
        else
        {
            Debug.LogError("❌ Точки респавна не найдены! Используем стандартную позицию");
            transform.position = new Vector3(0, 5, 0);
        }

        ShowPlayer();
        currentHealth = maxHealth;
        displayHealth = maxHealth;

        PlayerHunger hunger = GetComponent<PlayerHunger>();
        if (hunger != null) hunger.FullRestore();

        isDead = false;
        isRespawning = false;
        UpdateHealthUI();

        if (deathPanel != null) deathPanel.SetActive(false);

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.SetDead(false);

        Debug.Log("✅ Игрок возродился! HP: " + currentHealth + ", Голод: " + (hunger != null ? hunger.GetCurrentHunger() : 0));
        Debug.Log("========================================");
    }

    Transform GetRandomRespawnPoint()
    {
        if (respawnPoints == null || respawnPoints.Length == 0)
        {
            Debug.LogError("⚠️ Массив respawnPoints пуст или NULL!");
            return null;
        }
        for (int i = 0; i < respawnPoints.Length; i++)
            if (respawnPoints[i] != null) return respawnPoints[i];

        Debug.LogError("❌ Все точки респавна = NULL!");
        return null;
    }

    public void Heal(float amount)
    {
        if (!photonView.IsMine) return;
        if (isDead) return;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    public void FullHeal()
    {
        if (!photonView.IsMine) return;
        currentHealth = maxHealth;
        displayHealth = maxHealth;
        isDead = false;
        isRespawning = false;
        UpdateHealthUI();
        if (deathPanel != null) deathPanel.SetActive(false);
    }

    public float GetCurrentHealth() { return currentHealth; }
    public float GetMaxHealth() { return maxHealth; }
    public bool IsDead() { return isDead; }
}