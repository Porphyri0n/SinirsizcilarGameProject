using UnityEngine;

/// <summary>
/// Oyuncu state'lerini Animator'a aktarır. Locomotion (Speed/Grounded), taşıma ve blok bool'ları,
/// zıplama/saldırı trigger'ları buradan set edilir; asıl animasyon GEÇİŞLERİ Animator state machine'inde tanımlıdır.
/// Ragdoll sırasında Animator kapalı olduğundan (RagdollController) güncelleme atlanır.
/// </summary>
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

        // Zıplama tetiklemesini daha kararlı hale getiriyoruz:
        // Sadece dikey hız yukarı doğruysa (gerçek zıplama) Jump trigger'ı basılır.
        // Eğer sadece yere tam basmıyorsa (flicker) tetiklenmez.
        if (wasGrounded && !grounded && controller != null && controller.VerticalVelocityY > 0.1f)
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
