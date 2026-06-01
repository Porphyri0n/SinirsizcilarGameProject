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

    [Header("Kamera Kontrolü")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    private CharacterController controller;
    private PlayerStamina stamina;
    private Vector3 verticalVelocity;            // Sadece dikey hız (yerçekimi + zıplama)
    private Vector3 impulseVelocity;             // Saldırı vb. anlık itmeler
    private float speedMultiplier = 1f;          // CarrySystem vb. için hız ölçekleyici (1 = normal)

    private Vector3 smoothedMoveDir;             // Yon yumusatma icin
private float currentSpeed;                  // SmoothDamp ile yumusatilmis yatay hiz
    private float speedDampVel;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool wasGrounded;

    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    private Vector3 cameraOffset;

    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }
    public bool JustLanded { get; private set; }   // Bir karelik bayrak — ses/efekt icin
    public float VerticalVelocityY => verticalVelocity.y;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        // OnEnable içerisinde anında kilitlemek yerine Update kontrolüne bırakıyoruz.
        // Böylece Lobby gibi durumlarda cursor serbest kalır ve diğer oyuncuların 
        // spawn olması local cursor'ı etkilemez.
    }

    private void OnDisable()
    {
        // Script kapandığında (örn. ölüm, menü) cursor'ı serbest bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        if (cameraTransform != null)
        {
            // Kameranın başlangıç rotasyonunu al
            Vector3 rot = cameraTransform.eulerAngles;
            cameraYaw = rot.y;
            cameraPitch = rot.x;
            if (cameraPitch > 180f) cameraPitch -= 360f;

            // Kameranın oyuncuya göre başlangıç local offsetini hesapla
            cameraOffset = transform.InverseTransformPoint(cameraTransform.position);
        }
    }

    private bool wasGameStarted;

    private void Update()
    {
        // UI üzerindeysek girişleri atla
        bool isUIActive = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        
        // Oyun başlama durumunu kontrol et
        bool gameStarted = false;
        if (GameStateSync.Instance != null && GameStateSync.Instance.IsSpawned)
        {
            gameStarted = GameStateSync.Instance.GameStarted.Value;
        }
        else if (GameNetworkManager.Instance != null)
        {
            gameStarted = GameNetworkManager.Instance.GameStarted;
        }

        // Oyun ilk başladığında cursor'ı otomatik kilitlemeyi dene
        if (gameStarted && !wasGameStarted)
        {
            wasGameStarted = true;
            if (!isUIActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // İmleci oyuna almak için tıklama kontrolü (sadece UI dışına tıklandığında ve oyun başladıysa)
        if (Input.GetMouseButtonDown(0) && !isUIActive && gameStarted)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // İmleci serbest bırakmak için Escape kontrolü
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Yerçekimi her zaman uygulanmalı (oyun başladıysa), böylece karakter havada asılı kalmaz.
        if (gameStarted)
        {
            HandleGravityAndJump();
            HandleImpulse();
            
            // Hareket sadece cursor kilitliyken
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                HandleMovement();
            }
            else
            {
                IsMoving = false;
                currentSpeed = 0f;
            }
        }
    }

    private void HandleImpulse()
    {
        if (impulseVelocity.sqrMagnitude > 0.001f)
        {
            controller.Move(impulseVelocity * Time.deltaTime);
            impulseVelocity = Vector3.Lerp(impulseVelocity, Vector3.zero, 10f * Time.deltaTime);
        }
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        impulseVelocity += impulse;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        // İmleç kilitliyken mouse hareketleriyle kamerayı döndür
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            cameraYaw += mouseX;
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        }

        // Kameranın dünya rotasyonunu ve pozisyonunu güncelle
        Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 targetPosition = transform.position + rotation * cameraOffset;

        cameraTransform.rotation = rotation;
        cameraTransform.position = targetPosition;
    }


    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        IsMoving = input.sqrMagnitude > 0.01f;
        
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift);
        bool canSprint = IsMoving && wantsToSprint && (stamina == null || stamina.HasStamina);
        IsSprinting = canSprint;

        if (stamina != null)
        {
            if (IsSprinting)
            {
                stamina.ConsumeStamina(GameConstants.PLAYER_STAMINA_CONSUMPTION * Time.deltaTime);
            }
            else
            {
                stamina.RegenerateStamina(GameConstants.PLAYER_STAMINA_REGEN * Time.deltaTime);
            }
        }

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
