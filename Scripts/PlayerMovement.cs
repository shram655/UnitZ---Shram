using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("═══ НАСТРОЙКИ ДВИЖЕНИЯ ═══")]
    [Tooltip("Обычная скорость ходьбы")]
    public float speed = 7f;

    [Tooltip("Скорость бега при зажатом Shift")]
    public float sprintSpeed = 15f;

    [Tooltip("Сила прыжка")]
    public float jumpSpeed = 8.0f;

    [Tooltip("Сила гравитации")]
    public float gravity = 20.0f;

    [Header("═══ НАСТРОЙКА CHARACTER CONTROLLER ═══")]
    [Tooltip("ВКЛ = скрипт сам правильно настроит CharacterController, чтобы ноги были на земле")]
    public bool autoSetupCharacterController = true;

    [Tooltip("Высота коллайдера персонажа. Если персонаж цепляется головой/слишком высокий — уменьши")]
    public float controllerHeight = 1.45f;

    [Tooltip("Радиус коллайдера персонажа")]
    public float controllerRadius = 0.35f;

    [Tooltip("Смещение подошв относительно корня игрока. Для CubeWorldCharacter обычно -0.06. Если персонаж всё ещё висит — двигай ближе к 0")]
    public float visualFeetOffset = -0.06f;

    [Tooltip("Ширина 'кожи' CharacterController. Маленькое значение уменьшает визуальное зависание")]
    public float skinWidth = 0.03f;

    [Tooltip("Высота ступеньки, на которую игрок может заходить")]
    public float stepOffset = 0.3f;

    [Tooltip("Максимальный угол склона")]
    public float slopeLimit = 45f;

    [Header("═══ ПРИЖАТИЕ К ЗЕМЛЕ ПРИ СТАРТЕ ═══")]
    [Tooltip("ВКЛ = при спавне игрок будет сразу опущен на ближайшую землю под ним")]
    public bool snapToGroundOnStart = true;

    [Tooltip("С какой высоты над игроком начинать луч поиска земли")]
    public float snapRayStartHeight = 3f;

    [Tooltip("Максимальная дистанция поиска земли вниз")]
    public float snapRayDistance = 20f;

    [Tooltip("Слои, которые считаются землёй")]
    public LayerMask groundMask = ~0;

    private CharacterController controller;
    private PlayerController playerController;
    private PlayerInventory playerInventory;

    private float verticalVelocity = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();

        SetupCharacterController();
    }

    void Start()
    {
        if (snapToGroundOnStart)
        {
            SnapToGround();
        }
    }

    void Update()
    {
        if (playerController != null && playerController.view != null && !playerController.view.IsMine) return;
        if (playerController != null && playerController.isPlayerDead) return;

        bool inputBlocked = false;

        if (playerInventory != null && playerInventory.IsInventoryOpen)
            inputBlocked = true;

        if (ChatManager.IsChatOpen)
            inputBlocked = true;

        Move(inputBlocked);
    }

    void SetupCharacterController()
    {
        if (!autoSetupCharacterController) return;
        if (controller == null) return;

        controller.height = controllerHeight;
        controller.radius = controllerRadius;
        controller.skinWidth = skinWidth;
        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;

        /*
         * ВАЖНО:
         * У CharacterController низ капсулы считается так:
         * transform.position.y + center.y - height / 2
         *
         * Нам нужно, чтобы низ капсулы совпадал с подошвами визуальной модели.
         * У CubeWorldCharacter подошвы примерно на -0.06 ниже корня.
         */
        controller.center = new Vector3(0f, controllerHeight / 2f + visualFeetOffset, 0f);
    }

    private void Move(bool inputBlocked)
    {
        if (controller == null) return;

        Vector3 horizontalMove = Vector3.zero;

        // Движение читаем только когда ввод не заблокирован
        if (!inputBlocked)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            horizontalMove = new Vector3(h, 0f, v);
            horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f);
            horizontalMove = transform.TransformDirection(horizontalMove);

            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
            horizontalMove *= currentSpeed;
        }

        // Гравитация работает ВСЕГДА, даже когда открыт чат/инвентарь
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (!inputBlocked && Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = jumpSpeed;
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 finalMove = horizontalMove + Vector3.up * verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    public void SnapToGround()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null) return;

        bool wasEnabled = controller.enabled;

        // Временно выключаем CharacterController, чтобы луч не попал в самого игрока
        controller.enabled = false;

        Vector3 rayStart = transform.position + Vector3.up * snapRayStartHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, snapRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 newPosition = transform.position;

            /*
             * Нужно поставить transform так, чтобы:
             * transform.y + visualFeetOffset = земля
             *
             * Значит:
             * transform.y = земля - visualFeetOffset
             */
            newPosition.y = hit.point.y - visualFeetOffset;
            transform.position = newPosition;

            verticalVelocity = -2f;

            Debug.Log($"✅ PlayerMovement: персонаж прижат к земле. Земля Y={hit.point.y}, новая позиция Y={transform.position.y}");
        }
        else
        {
            Debug.LogWarning("⚠️ PlayerMovement: земля под игроком не найдена, SnapToGround пропущен");
        }

        controller.enabled = wasEnabled;
    }
}