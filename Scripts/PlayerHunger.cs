using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class PlayerHunger : MonoBehaviourPun
{
    [Header("Настройки голода")]
    public float maxHunger = 100f;
    private float currentHunger;
    private float displayHunger;

    [Header("Убывание и урон")]
    public float hungerDecayRate = 1f;
    public float starvationDamage = 5f;
    public float starvationTickRate = 2f;

    [Header("UI")]
    public Slider hungerBar;
    public Image hungerBarFill;
    public TextMeshProUGUI hungerText;

    [Header("Ссылки")]
    public PlayerHealth playerHealth;

    private float starvationTimer = 0f;

    void Start()
    {
        currentHunger = maxHunger;
        displayHunger = maxHunger;

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (hungerBar != null)
        {
            hungerBar.maxValue = maxHunger;
            hungerBar.value = maxHunger;
        }

        if (hungerText != null)
        {
            hungerText.text = Mathf.Ceil(currentHunger).ToString();
        }

        UpdateHungerUI();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // 1. Убывание голода
        if (currentHunger > 0)
        {
            currentHunger -= hungerDecayRate * Time.deltaTime;
            currentHunger = Mathf.Max(currentHunger, 0f);
            starvationTimer = 0f;
        }
        else
        {
            // 2. Голод = 0, получаем урон от голодания
            starvationTimer += Time.deltaTime;
            if (starvationTimer >= starvationTickRate)
            {
                starvationTimer = 0f;
                if (playerHealth != null && !playerHealth.IsDead())
                {
                    playerHealth.TakeDamage(starvationDamage);
                    Debug.Log("💀 Вы получаете урон от голода! (-" + starvationDamage + " HP)");
                }
            }
        }

        // 3. Плавная анимация полоски
        if (hungerBar != null && displayHunger != currentHunger)
        {
            displayHunger = Mathf.Lerp(displayHunger, currentHunger, Time.deltaTime * 5f);
            hungerBar.value = displayHunger;
        }

        // 4. Обновляем текст с цифрами
        if (hungerText != null)
        {
            hungerText.text = Mathf.Ceil(currentHunger).ToString();
        }

        // 5. Обновляем цвет полоски и текста
        UpdateHungerUI();

        // 🍔 ТЕСТОВАЯ КНОПКА: Нажми 'O' чтобы поесть
        if (Input.GetKeyDown(KeyCode.O))
        {
            ConsumeFood(30f);
        }
    }

    public void ConsumeFood(float amount)
    {
        if (!photonView.IsMine) return;
        
        currentHunger += amount;
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        Debug.Log("🍔 Вы поели! Восстановлено " + amount + ". Текущий голод: " + currentHunger);
    }

    // ✅ НОВЫЙ МЕТОД: Поедание конкретного предмета из инвентаря
    public void ConsumeFoodItem(int blockId, float hungerRestore)
    {
        if (!photonView.IsMine) return;
        
        ConsumeFood(hungerRestore);
        Debug.Log("🍎 Съеден предмет ID " + blockId + ", восстановлено " + hungerRestore + " голода");
    }

    void UpdateHungerUI()
    {
        if (hungerBarFill != null)
        {
            if (currentHunger > maxHunger * 0.5f)
            {
                hungerBarFill.color = new Color(1f, 0.55f, 0f);
                if (hungerText != null) hungerText.color = Color.white;
            }
            else if (currentHunger > maxHunger * 0.2f)
            {
                hungerBarFill.color = Color.yellow;
                if (hungerText != null) hungerText.color = Color.yellow;
            }
            else
            {
                hungerBarFill.color = Color.red;
                if (hungerText != null) hungerText.color = Color.red;
            }
        }
    }

    public void FullRestore()
    {
        if (!photonView.IsMine) return;
        
        currentHunger = maxHunger;
        displayHunger = maxHunger;
        starvationTimer = 0f;
        
        if (hungerBar != null) hungerBar.value = maxHunger;
        if (hungerText != null) hungerText.text = maxHunger.ToString();
        
        UpdateHungerUI();
        Debug.Log("🍖 Голод полностью восстановлен при респавне!");
    }

    public float GetCurrentHunger() { return currentHunger; }
}