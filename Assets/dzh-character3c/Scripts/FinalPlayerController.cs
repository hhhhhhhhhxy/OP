using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class FinalPlayerController : MonoBehaviour
{
    Transform playerTransform;
    Animator animator;
    Transform cameraTransform;
    CharacterController characterController;

    public enum PlayerPosture
    {
        Crouch,
        Stand,
        Midair
    }
    [HideInInspector]
    public PlayerPosture playerPosture = PlayerPosture.Stand;

    float crouchThreshold = 0f;
    float standThreshold = 1f;
    float midairThreshold = 2.3f;

    public enum LocalMotionState
    {
        Idle,
        Walk,
        Run
    }
    [HideInInspector]
    public LocalMotionState localMotionState = LocalMotionState.Idle;

    public enum ArmState
    {
        Normal,
        Aim,
    }
    [HideInInspector]
    public ArmState armState = ArmState.Normal;

    float crouchSpeed = 1.5f;
    float walkeSpeed = 2.5f;
    float runSpeed = 5.5f;

    Vector2 moveInput;
    bool isRunning;
    bool isCrouching;
    bool isAiming;
    bool isJumping;

    int postureHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int verticalVelocityHash;

    Vector3 playerMovement = Vector3.zero;

    public float gravity = -9.8f;
    float verticalVelocity;
    public float jumpVelocity = 5f;

    Vector3 lastVelOnGround;
    static readonly int CACHE_SIZE = 3;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int currentCacheIndex = 0;
    Vector3 averageVel = Vector3.zero;

    float fallMultipler = 1.5f;

    bool isGrounded;
    float groundCheckOffset = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        playerTransform = transform;
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>(); 
        cameraTransform = Camera.main.transform;

        postureHash = Animator.StringToHash("玩家姿态");
        moveSpeedHash = Animator.StringToHash("移动速度");
        turnSpeedHash = Animator.StringToHash("转向速度");
        verticalVelocityHash = Animator.StringToHash("垂直速度");
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        CaculateGravity();
        Jump();
        CaculateInputDirection();
        SwitchPlayerState();
        SetupAnimator();
    }

    #region 输入相关
    public void GetMoveInput(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void GetRunInput(InputAction.CallbackContext ctx)
    {
        isRunning = ctx.ReadValueAsButton();
    }

    public void GetCrouchInput(InputAction.CallbackContext ctx)
    {
        isCrouching = ctx.ReadValueAsButton();
    }

    public void GetAimInput(InputAction.CallbackContext ctx)
    {
        isAiming = ctx.ReadValueAsButton();
    }

    public void GetJumpInput(InputAction.CallbackContext ctx)
    {
        isJumping = ctx.ReadValueAsButton();
    }

    #endregion

    void SwitchPlayerState()
    {
        if (!characterController.isGrounded)
        {
            playerPosture = PlayerPosture.Midair;
        }
        else if (isCrouching)
        {
            playerPosture = PlayerPosture.Crouch;
        }
        else
        {
            playerPosture = PlayerPosture.Stand;
        }

        if(moveInput.magnitude == 0)
        {
            localMotionState = LocalMotionState.Idle;
        }else if (!isRunning)
        {
            localMotionState = LocalMotionState.Walk;
        }
        else
        {
            localMotionState = LocalMotionState.Run;
        }

        if (isAiming)
        {
            armState = ArmState.Aim;
        }
        else
        {
            armState = ArmState.Normal;
        }
    }

    void CheckGround()
    {
        if(Physics.SphereCast(playerTransform.position+(Vector3.up*groundCheckOffset),characterController.radius,Vector3.down,out RaycastHit hit,groundCheckOffset-characterController.radius + 2 * characterController.skinWidth))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    void CaculateGravity()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = gravity * Time.deltaTime;
            return;
        }
        else
        {
            if (verticalVelocity < 0)
            {
                verticalVelocity += gravity * fallMultipler * Time.deltaTime;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            
        }
    }

    void Jump()
    {
        if (characterController.isGrounded && isJumping)
        {
            verticalVelocity = jumpVelocity;
            isJumping = false;
        }
    }

    void CaculateInputDirection()
    {
        Vector3 camForwardProjection = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
        playerMovement = camForwardProjection * moveInput.y + cameraTransform.right * moveInput.x;
        playerMovement = playerTransform.InverseTransformVector(playerMovement);
    }

    void SetupAnimator()
    {
        if(playerPosture == PlayerPosture.Stand)
        {
            animator.SetFloat(postureHash, standThreshold,0.1f,Time.deltaTime);
            switch (localMotionState)
            {
                case LocalMotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;
                case LocalMotionState.Walk:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkeSpeed, 0.1f, Time.deltaTime);
                    break;
                case LocalMotionState.Run:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }else if(playerPosture == PlayerPosture.Crouch)
        {
            animator.SetFloat(postureHash, crouchThreshold, 0.1f, Time.deltaTime);
            switch (localMotionState)
            {
                case LocalMotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;
                default:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Midair)
        {
            animator.SetFloat(postureHash, midairThreshold, 0.1f, Time.deltaTime);
            animator.SetFloat(verticalVelocityHash, verticalVelocity, 0.1f, Time.deltaTime);

        }

            if (armState == ArmState.Normal)
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            playerTransform.Rotate(0, rad * 200 * Time.deltaTime, 0f);
        }
    }

    Vector3 AverageVel(Vector3 newVel)
    {
        velCache[currentCacheIndex] = newVel;
        currentCacheIndex++;
        currentCacheIndex %= CACHE_SIZE;
        Vector3 average = Vector3.zero;
        foreach(Vector3 vel in velCache)
        {
            average += vel;
        }
        return average / CACHE_SIZE;
    }

    private void OnAnimatorMove()
    {
        if (playerPosture != PlayerPosture.Midair)
        {
            Vector3 playerDeltaMovement = animator.deltaPosition;
            playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
            averageVel = AverageVel(animator.velocity);
        }
        else
        {
            // 沿用离地前几帧的平均速度
            averageVel.y = verticalVelocity;
            Vector3 playerDeltaMovement = averageVel * 1.5f * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }

    }
}
