using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerBuilding))]
[RequireComponent(typeof(PlayerWeaponManager))]
public class PlayerController : MonoBehaviourPunCallbacks
{
    [Header("Ссылки на компоненты")]
    public PhotonView view;
    public Camera playerCamera;
    public TextMeshPro nick;
    public GameObject Torch;

    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public PlayerBuilding building;
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public PlayerHealth health;
    [HideInInspector] public PlayerHunger hunger;

    [HideInInspector] public bool isPlayerDead = false;

    void Awake()
    {
        view = GetComponent<PhotonView>();
        movement = GetComponent<PlayerMovement>();
        inventory = GetComponent<PlayerInventory>();
        building = GetComponent<PlayerBuilding>();
        weaponManager = GetComponent<PlayerWeaponManager>();
        health = GetComponent<PlayerHealth>();
        hunger = GetComponent<PlayerHunger>();
    }

    void Start()
    {
        string nickname = PhotonNetwork.NickName;
        if (string.IsNullOrEmpty(nickname)) nickname = "Player";
        if (nick != null) nick.text = nickname;

        if (!view.IsMine)
        {
            enabled = false;
            movement.enabled = false;
            inventory.enabled = false;
            building.enabled = false;
            weaponManager.enabled = false;
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            // 🆕 ГЛАВНЫЙ ФИКС: скрываем ВЕСЬ UI чужого игрока!
            // Иначе его полоса голода / уведомления рисуются поверх твоих
            foreach (Canvas c in GetComponentsInChildren<Canvas>(true))
            {
                c.gameObject.SetActive(false);
            }

            Debug.Log($"👥 Чужой игрок {nickname}: UI полностью скрыт");
            return;
        }

        Debug.Log($"✅ Локальный игрок создан: {nickname}");
    }

    void Update()
    {
        if (!view.IsMine || isPlayerDead) return;

        // БЛОКИРОВКА ВСЕХ КЛАВИШ ВО ВРЕМЯ ЧАТА
        if (ChatManager.IsChatOpen) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (Torch != null) Torch.SetActive(!Torch.activeSelf);
        }

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.Confined;
            PhotonNetwork.Disconnect();
            PhotonNetwork.LoadLevel("LobbyScene");
        }
    }

    public void SetDead(bool dead)
    {
        isPlayerDead = dead;
        if (dead)
        {
            if (weaponManager != null) weaponManager.UnequipCurrentWeapon();
            if (inventory != null) inventory.ClearInventory();
        }
    }

    // ═════════════════════════════════════════════════════
    // МЕТОДЫ ДЛЯ МИНИКАРТЫ
    // ═════════════════════════════════════════════════════
    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public float GetRotationY()
    {
        return transform.eulerAngles.y;
    }

    public bool IsLocalPlayer()
    {
        return view != null && view.IsMine;
    }
}