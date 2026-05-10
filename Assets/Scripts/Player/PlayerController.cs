using System;
using UnityEngine;

/// <summary>
/// CharacterController bazlı 3rd person oyuncu hareketi.
/// WASD ile hareket, Shift ile koşu, Space ile zıplama. Yerçekimi elle uygulanır.
/// Hız sabitleri GameConstants'tan gelir; CarrySystem gibi sistemler hızı çarpanla düşürebilir.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField] private float moveSpeed = GameConstants.PLAYER_BASE_SPEED;
    [SerializeField] private float sprintSpeed = GameConstants.PLAYER_SPRINT_SPEED;
    [SerializeField] private float jumpForce = GameConstants.PLAYER_JUMP_FORCE;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Referanslar")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector3 verticalVelocity;            // Sadece dikey hız (yerçekimi + zıplama)
    private float speedMultiplier = 1f;          // CarrySystem vb. için hız ölçekleyici (1 = normal)

    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        HandleMovement();
        HandleGravityAndJump();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        IsMoving = input.sqrMagnitude > 0.01f;
        IsSprinting = IsMoving && Input.GetKey(KeyCode.LeftShift);

        float speed = (IsSprinting ? sprintSpeed : moveSpeed) * speedMultiplier;

        Vector3 moveDir = Vector3.zero;
        if (IsMoving)
        {
            // Kameraya göre hareket yönü (3rd person)
            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDir = (camForward * input.z + camRight * input.x).normalized;

            // Karakteri hareket ettiği yöne döndür
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        controller.Move(moveDir * speed * Time.deltaTime);
    }

    private void HandleGravityAndJump()
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;   // Yere yapışık kalması için küçük negatif değer

            if (Input.GetKeyDown(KeyCode.Space))
                verticalVelocity.y = jumpForce;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    /// <summary>Hareket hızını ölçekler (örn. eşya taşırken CARRY_SPEED_MULTIPLIER). 1 = normal hız.</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }
}
