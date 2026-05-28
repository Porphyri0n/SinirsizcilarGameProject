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

    [Header("His Polish")]
    [SerializeField] private float accelerationTime = 0.12f;   // hizin hedef hiza ulasma suresi (yumusatma)
    [SerializeField] private float directionSmoothing = 16f;   // yon degisikliklerini yumusatir (daha buyuk = daha cevik)
    [SerializeField] private float coyoteTime = 0.1f;          // yerden ayrildiktan sonra ziplamaya izin verilen sure
    [SerializeField] private float jumpBufferTime = 0.1f;      // yere inmeden once basilan ziplama input'unu hatirla

    [Header("Referanslar")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector3 verticalVelocity;            // Sadece dikey hız (yerçekimi + zıplama)
    private float speedMultiplier = 1f;          // CarrySystem vb. için hız ölçekleyici (1 = normal)

    private Vector3 smoothedMoveDir;             // Yon yumusatma icin
    private float currentSpeed;                  // SmoothDamp ile yumusatilmis yatay hiz
    private float speedDampVel;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool wasGrounded;

    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }
    public bool JustLanded { get; private set; }   // Bir karelik bayrak — ses/efekt icin

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

        float targetSpeed = IsMoving ? (IsSprinting ? sprintSpeed : moveSpeed) * speedMultiplier : 0f;
        // Hizi anlik degil yumusak yaklastir — kalkis ve durus daha agirlikli hissediyor
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedDampVel, accelerationTime);

        Vector3 rawDir = Vector3.zero;
        if (IsMoving)
        {
            // Kameraya göre hareket yönü (3rd person)
            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            rawDir = (camForward * input.z + camRight * input.x).normalized;

            // Karakteri hareket ettiği yöne döndür
            Quaternion targetRotation = Quaternion.LookRotation(rawDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Yon degisikliklerini de yumusat (ani 180 donuste savrulma hissi azalir)
        smoothedMoveDir = Vector3.Lerp(smoothedMoveDir, rawDir, directionSmoothing * Time.deltaTime);

        controller.Move(smoothedMoveDir * currentSpeed * Time.deltaTime);
    }

    private void HandleGravityAndJump()
    {
        bool grounded = controller.isGrounded;

        // Coyote: yerden ayrildiktan sonra kisa sure ziplama hala kabul edilir
        if (grounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.deltaTime;

        // Jump buffer: input erken basildiysa yere indikten sonra hala gecerli sayilir
        if (Input.GetKeyDown(KeyCode.Space)) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (grounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;   // Yere yapışık kalması için küçük negatif değer

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            verticalVelocity.y = jumpForce;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);

        // Inis tespiti — havada degildim, simdi yerdeyim → bir karelik flag
        JustLanded = grounded && !wasGrounded;
        wasGrounded = grounded;
    }

    /// <summary>Hareket hızını ölçekler (örn. eşya taşırken CARRY_SPEED_MULTIPLIER). 1 = normal hız.</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }
}

// Oyuncu state'lerini Animator'a aktarır. Locomotion (Speed/Grounded), taşıma ve blok bool'ları,
// zıplama/saldırı trigger'ları buradan set edilir; asıl animasyon GEÇİŞLERİ Animator state machine'inde tanımlıdır.
// Ragdoll sırasında Animator kapalı olduğundan (RagdollController) güncelleme atlanır.
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private CarrySystem carry;
    [SerializeField] private float speedDamp = 0.12f;   // Speed parametresi yumuşatma süresi

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int CarryingHash = Animator.StringToHash("Carrying");
    private static readonly int BlockingHash = Animator.StringToHash("Blocking");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private bool wasGrounded = true;
    private bool wasOnCooldown;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (controller == null) controller = GetComponent<PlayerController>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (carry == null) carry = GetComponent<CarrySystem>();
    }

    private void Update()
    {
        if (animator == null || !animator.enabled) return;   // ragdoll'dayken Animator kapalı

        UpdateLocomotion();
        UpdateActions();
    }

    private void UpdateLocomotion()
    {
        // 0 = idle, 0.5 = yürüme, 1 = koşu (blend tree eşikleri)
        float targetSpeed = 0f;
        if (controller != null && controller.IsMoving)
            targetSpeed = controller.IsSprinting ? 1f : 0.5f;

        animator.SetFloat(SpeedHash, targetSpeed, speedDamp, Time.deltaTime);

        bool grounded = controller == null || controller.IsGrounded;
        animator.SetBool(GroundedHash, grounded);

        // Yerden ayrılma anında zıplama geçişi
        if (wasGrounded && !grounded)
            animator.SetTrigger(JumpHash);
        wasGrounded = grounded;
    }

    private void UpdateActions()
    {
        if (carry != null) animator.SetBool(CarryingHash, carry.IsCarrying);
        if (combat != null) animator.SetBool(BlockingHash, combat.IsBlocking);

        // Saldırı cooldown'u yeni başladıysa bir saldırı tetiklendi demektir → Attack geçişi
        bool onCooldown = combat != null && combat.IsOnAttackCooldown;
        if (onCooldown && !wasOnCooldown)
            animator.SetTrigger(AttackHash);
        wasOnCooldown = onCooldown;
    }
}
