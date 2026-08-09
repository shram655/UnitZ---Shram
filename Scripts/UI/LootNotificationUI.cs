using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LootNotificationUI : MonoBehaviour
{
    public static LootNotificationUI Instance;

    [Header("Настройки")]
    public float displayTime = 3f;

    private GameObject panel;
    private Image iconImage;
    private TextMeshProUGUI tmpText;
    private Text uiText;
    private NotificationRunner runner;

    private readonly Queue<NotificationData> queue = new Queue<NotificationData>();
    private bool showing = false;
    private bool isRemote = false;

    public struct NotificationData
    {
        public Sprite icon;
        public string text;
    }

    void Awake()
    {
        // ✅ Если этот UI висит на ЧУЖОМ игроке — не регистрируем как Instance
        Move_Player mp = GetComponentInParent<Move_Player>();
        if (mp != null && mp.view != null && !mp.view.IsMine)
        {
            isRemote = true;
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (isRemote) return;
        Setup();
    }

    void Setup()
    {
        // ✅ 1. Панель — это сам объект компонента?
        if (gameObject.name == "LootNotificationPanel")
        {
            panel = gameObject;
        }

        // ✅ 2. Ищем панель в СВОЕЙ иерархии (игрок / сцена)
        if (panel == null)
        {
            foreach (var t in transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LootNotificationPanel")
                {
                    panel = t.gameObject;
                    break;
                }
            }
        }

        // ✅ 3. Ищем панель глобально (даже скрытую)
        if (panel == null)
        {
            panel = GameObject.Find("LootNotificationPanel");
        }

        if (panel == null)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "LootNotificationPanel" && go.scene.isLoaded && go.hideFlags == HideFlags.None)
                {
                    panel = go;
                    break;
                }
            }
        }

        if (panel != null)
        {
            foreach (var img in panel.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject != panel)
                {
                    iconImage = img;
                    break;
                }
            }

            tmpText = panel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText == null) uiText = panel.GetComponentInChildren<Text>(true);

            panel.SetActive(false);

            GameObject runnerObj = new GameObject("LootNotificationRunner");
            runner = runnerObj.AddComponent<NotificationRunner>();
            runner.ui = this;

            Debug.Log("✅ LootNotificationUI: панель найдена и скрыта при входе");
        }
        else
        {
            Debug.LogError("❌ LootNotificationPanel не найден в сцене!");
        }
    }

    public void ShowNotification(Sprite icon, string itemName, int count)
    {
        try
        {
            if (isRemote) return; // ✅ Чужой UI ничего не показывает

            if (panel == null) Setup();
            if (panel == null) return;

            queue.Enqueue(new NotificationData
            {
                icon = icon,
                text = $"+{count} {itemName}"
            });

            if (!showing && runner != null)
            {
                runner.StartNext();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ LootNotificationUI ошибка (не критично): {e.Message}");
        }
    }

    public NotificationData? PeekNext()
    {
        if (queue.Count > 0)
        {
            return queue.Dequeue();
        }
        return null;
    }

    public void ApplyNotification(NotificationData data)
    {
        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
            iconImage.color = Color.white;
        }

        if (tmpText != null) tmpText.text = data.text;
        else if (uiText != null) uiText.text = data.text;

        panel.SetActive(true);

        Debug.Log($"🖼️ Показываю уведомление: {data.text} (иконка: {(data.icon != null ? data.icon.name : "НЕТ")})");
    }

    public void HidePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void SetShowing(bool value)
    {
        showing = value;
    }
}

public class NotificationRunner : MonoBehaviour
{
    public LootNotificationUI ui;
    private bool processing = false;

    public void StartNext()
    {
        if (!processing)
        {
            StartCoroutine(Process());
        }
    }

    IEnumerator Process()
    {
        processing = true;
        ui.SetShowing(true);

        while (true)
        {
            var next = ui.PeekNext();
            if (next == null) break;

            ui.ApplyNotification(next.Value);

            yield return new WaitForSeconds(ui.displayTime);

            ui.HidePanel();

            yield return new WaitForSeconds(0.2f);
        }

        ui.SetShowing(false);
        processing = false;
    }
}