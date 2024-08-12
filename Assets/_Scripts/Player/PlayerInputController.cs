using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : NetworkBehaviour
{
    [SerializeField] private Transform playerCamera;
    public PlayerCharacterController character;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction diveAction;
    private InputAction crouchAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        lookAction = playerInput.actions.FindAction("Look");
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        diveAction = playerInput.actions.FindAction("Dive");
        crouchAction = playerInput.actions.FindAction("Crouch");
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            EnableCamera();
        }
        else
        {
            this.enabled = false;
        }
    }

    public void EnableCamera()
    {
        playerCamera.gameObject.SetActive(true);
    }

    private void Update()
    {
        // if (Input.GetMouseButtonDown(0))
        // {
        //     Cursor.lockState = CursorLockMode.Locked;
        // }

        HandleCharacterInput();
    }

    private void HandleCharacterInput()
    {
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

        // Build the CharacterInputs struct
        characterInputs.moveVector = moveAction.ReadValue<Vector2>();
        characterInputs.lookDelta = lookAction.ReadValue<Vector2>();

        characterInputs.jumpPressed = jumpAction.WasPressedThisFrame();
        characterInputs.divePressed = diveAction.WasPressedThisFrame();

        // Prevent moving the camera while the cursor isn't locked
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            characterInputs.lookDelta = Vector3.zero;
        }

        // Apply inputs to character
        character.SetInputs(ref characterInputs);
    }
}
