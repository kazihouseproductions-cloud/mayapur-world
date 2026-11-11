using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    private static readonly int MovingId = Animator.StringToHash("Moving");
    private static readonly int JumpingId = Animator.StringToHash("Jumping");

    [SerializeField, Min(0f)] private float moveSpeed = 0.75f;
    [Tooltip("Height of a jump in world units.")]
    [SerializeField, Min(0f)] private float jumpHeight = 0.1f;
    [Tooltip("Maximum turning speed in degrees per second.")]
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [Tooltip("Use the Main Camera. If empty, the script finds Camera.main.")]
    [SerializeField] private Transform movementCamera;
    [Tooltip("Animator on Sheela's model. If empty, the Player hierarchy is searched.")]
    [SerializeField] private Animator characterAnimator;

    private Rigidbody body;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private readonly HashSet<Collider> groundContacts = new HashSet<Collider>();
    private bool jumpRequested;
    private bool jumping;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        // Read this PlayerInput's actions rather than a global input asset.
        moveAction = playerInput.actions?.FindAction("Player/Move");
        if (moveAction == null)
        {
            Debug.LogError("PlayerMovement needs a Player Input actions asset with a Player/Move action.", this);
            enabled = false;
            return;
        }

        if (movementCamera == null && Camera.main != null)
            movementCamera = Camera.main.transform;

        jumpAction = playerInput.actions.FindAction("Player/Jump");
        if (jumpAction != null)
            jumpAction.performed += OnJumpPressed;
    }

    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        if (isActiveAndEnabled && playerInput.inputIsActive && playerInput.currentActionMap == context.action.actionMap)
            jumpRequested = true;
    }

    private void FixedUpdate()
    {
        if (body.isKinematic)
        {
            jumpRequested = false;
            jumping = false;
            UpdateAnimator(false);
            return;
        }

        Vector2 input = Vector2.zero;
        if (moveAction != null && playerInput.isActiveAndEnabled && playerInput.inputIsActive
            && playerInput.currentActionMap == moveAction.actionMap && moveAction.enabled)
        {
            input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }

        Vector3 forward = Vector3.forward;
        if (movementCamera != null)
        {
            forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up);
            // A camera looking straight down uses its screen-up direction.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(movementCamera.up, Vector3.up);
            forward.Normalize();
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 movement = (right * input.x + forward * input.y) * moveSpeed;

        float verticalSpeed = body.linearVelocity.y;
        if (jumpRequested || jumping)
        {
            groundContacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (groundContacts.Count > 0 && verticalSpeed <= 0.01f)
                jumping = false;

            if (jumpRequested && !jumping && groundContacts.Count > 0 && jumpHeight > 0f && Physics.gravity.y < 0f
                && jumpAction.enabled && playerInput.inputIsActive && playerInput.currentActionMap == jumpAction.actionMap)
            {
                verticalSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
                jumping = true;
            }
            jumpRequested = false;
        }
        body.linearVelocity = new Vector3(movement.x, verticalSpeed, movement.z);

        bool moving = movement.sqrMagnitude > 0.000001f;
        UpdateAnimator(moving);
        if (moving)
        {
            Quaternion facing = Quaternion.LookRotation(movement, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, facing, turnSpeed * Time.fixedDeltaTime));
        }
    }

    private void OnCollisionEnter(Collision collision) => UpdateGroundContact(collision);
    private void OnCollisionStay(Collision collision) => UpdateGroundContact(collision);
    private void OnCollisionExit(Collision collision) => groundContacts.Remove(collision.collider);

    private void UpdateGroundContact(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            // Walls and ceilings cannot support a jump.
            if (collision.GetContact(i).normal.y >= 0.7f)
            {
                groundContacts.Add(collision.collider);
                return;
            }
        }
        groundContacts.Remove(collision.collider);
    }

    private void OnDisable()
    {
        jumpRequested = false;
        jumping = false;
        UpdateAnimator(false);
        if (body != null && !body.isKinematic)
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
    }

    private void OnDestroy()
    {
        if (jumpAction != null)
            jumpAction.performed -= OnJumpPressed;
    }

    private void UpdateAnimator(bool moving)
    {
        if (characterAnimator != null && characterAnimator.isInitialized)
        {
            characterAnimator.SetBool(MovingId, moving);
            characterAnimator.SetBool(JumpingId, jumping);
        }
    }
}
