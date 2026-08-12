using UnityEngine;
using UnityEngine.InputSystem;

public class BallMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public InputActionAsset inputActions; // drag your InputSystem_Actions asset here

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private InputAction moveAction;

    void Awake()
    {
        // grab the "Move" action directly from the "Player" action map
        moveAction = inputActions.FindActionMap("Player").FindAction("Move");
    }

    void OnEnable()
    {
        moveAction.Enable();
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
    }

    void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        moveAction.Disable();
    }

    void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveInput.normalized * moveSpeed;
    }
}