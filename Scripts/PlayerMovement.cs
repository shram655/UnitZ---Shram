using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float speed = 7f;
    public float sprintSpeed = 15f;
    public float jumpSpeed = 8.0F;
    public float gravity = 20.0F;

    private CharacterController controller;
    private Vector3 moveDirection = Vector3.zero;
    private PlayerController playerController;
    private PlayerInventory playerInventory;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        playerInventory = GetComponent<PlayerInventory>();
    }

    void Update()
    {
        if (playerController != null && !playerController.view.IsMine) return;
        if (playerController != null && playerController.isPlayerDead) return;
        if (playerInventory != null && playerInventory.IsInventoryOpen) return;

        Move();
    }

    private void Move()
    {
        if (controller.isGrounded)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            moveDirection = new Vector3(h, 0, v);
            moveDirection = transform.TransformDirection(moveDirection);
            
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
            moveDirection *= currentSpeed;

            if (Input.GetKey(KeyCode.Space)) moveDirection.y = jumpSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }
}