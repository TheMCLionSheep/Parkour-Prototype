using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class PlayerMovement : MonoBehaviour
{
    public const float minPitch = -90f;
    public const float maxPitch = 70f;
    public const float epsilon = 0.001f;
    public const float maxAngleShoveDegrees = 60f;

    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxSpeed = 7.5f;
    [SerializeField] private float runAcceleration = 50f;
    [SerializeField] private float runDeceleration = 30f;
    [SerializeField] private float groundDist = 0.01f;
    [SerializeField] private float maxWalkingAngle = 60f;
    [SerializeField] private float anglePower = 0.5f;

    [SerializeField] private float jumpPower = 7f;
    [SerializeField] private Vector2 divePower = new Vector2(5, 5);
    [SerializeField] private Vector2 jumpDivePower = new Vector2(5, 7);
    [SerializeField] private Vector2 divingJumpPower = new Vector2(5, 7);

    [SerializeField] private float chainActionBuffer = 0.05f;
    [SerializeField] private float coyoteTime = 0.05f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float reuseDelay = 0.2f;
    [SerializeField] private float ragdollDelay = 0.2f;

    [SerializeField] private float ragdollControlSpeed = 5f;

    [SerializeField] private float diveAngleSpeed = 10f;
    [SerializeField] private float maxDiveAngle = 90f;
    [SerializeField] private float maxDiveCameraAngle = 45f;
    [SerializeField] private float armLength = 1.5f;
    [SerializeField] private float fadeArmLength = 1f;

    [SerializeField] private float anchorOffset = -1.5f;
    [SerializeField] private float fullHeight = 2f;
    [SerializeField] private float tuckedHeight = 1f;
    [SerializeField] private float standupSpeed = 0.5f;

    [SerializeField] private int maxBounces = 5;

    [SerializeField] private float gravity;
    [SerializeField] private float diveDrag;
    [SerializeField] private float ragdollDrag;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform playerModel;
    [SerializeField] private GameObject handprintDecal;
    [SerializeField] private GameObject playerRagdollCamera;

    private Vector2 cameraAngle;
    private float diveAngle = 0f;

    private float timeSinceJump = Mathf.Infinity;
    private float timeSinceDive = Mathf.Infinity;
    private float timeSinceJumpPressed = Mathf.Infinity;
    private float timeSinceDivePressed = Mathf.Infinity;
    private float timeSinceOnGround = 0f;
    private float timeSinceDiveReady = Mathf.Infinity;
    private float timeInRagdoll = 0f;

    private bool pressingDive = false;
    private bool diving = false;
    private bool ragdoll = false;

    private float verticalVelocity;
    private Vector2 horizontalRunVelocity;
    private Vector2 diveVelocity;
    private Vector2 ragdollVelocity;
    
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction diveAction;

    private CapsuleCollider capsuleCollider;
    private DecalProjector decalProjector;

    // Start is called before the first frame update
    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        playerInput = GetComponent<PlayerInput>();
        capsuleCollider = playerBody.GetComponent<CapsuleCollider>();

        lookAction = playerInput.actions.FindAction("Look");
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        diveAction = playerInput.actions.FindAction("Dive");
    }

    private void Start() {
        // Set up the decal project based on variable settings
        handprintDecal.transform.localRotation = Quaternion.Euler(maxDiveCameraAngle, 0f, 0f);
        decalProjector = handprintDecal.GetComponent<DecalProjector>();
        decalProjector.size = new Vector3(1f, 1f, armLength);
        decalProjector.fadeFactor = 0f;
    }

    private void OnEnable()
    {
        jumpAction.performed += JumpPressed;
        diveAction.performed += DivePressed;
        diveAction.canceled += DiveCancelled;
    }

    private void OnDisable() {
        jumpAction.performed -= JumpPressed;
        diveAction.performed -= DivePressed;
        diveAction.canceled -= DiveCancelled;
    }

    private void DiveCancelled(InputAction.CallbackContext context)
    {
        pressingDive = false;
    }

    private void JumpPressed(InputAction.CallbackContext context)
    {
        if (timeSinceJumpPressed >= reuseDelay)
        {
            timeSinceJumpPressed = 0f;
        }
    }

    private void DivePressed(InputAction.CallbackContext context)
    {
        if (timeSinceDivePressed >= reuseDelay)
        {
            timeSinceDivePressed = 0f;
        }
        pressingDive = true;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Read camera movement from player
        Vector2 lookDelta = lookAction.ReadValue<Vector2>();
        Vector2 playerMove = moveAction.ReadValue<Vector2>();

        // Calculate new camera angle based on change
        if (!ragdoll)
        {
            cameraAngle.x += -lookDelta.y * mouseSensitivity;
            cameraAngle.y += lookDelta.x * mouseSensitivity;
        }

        // Clamp max up and down angle
        cameraAngle.x = Mathf.Clamp(cameraAngle.x, minPitch, maxPitch);

        // Rotate the camera based on y movement
        playerCamera.localRotation = Quaternion.Euler(cameraAngle.x, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, cameraAngle.y, 0f);
        
        var moveDirection = new Vector3(playerMove.x, 0, playerMove.y);

        // Get the move direction based on player rotation
        var viewYaw = Quaternion.Euler(0, 0, -cameraAngle.y);
        Vector2 rotatedVector = viewYaw * playerMove;
        Vector2 normalizedMoveDirection = rotatedVector.normalized * Mathf.Min(rotatedVector.magnitude, 1.0f);

        // Check if the player is on the ground
        bool falling = !CheckGrounded(out RaycastHit groundhit);
        if (falling) // If falling, apply gravity downward
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (!ragdoll)
            {
                timeInRagdoll = 0f;
            }
        }
        else // If on ground, reset vertical velocity.
        {
            verticalVelocity = 0;
            timeSinceOnGround = 0f;
            if (diving)
            {
                timeInRagdoll += Time.deltaTime;
            }
        }

        if (timeInRagdoll >= ragdollDelay && !ragdoll)
        {
            ragdoll = true;
            ragdollVelocity = horizontalRunVelocity + diveVelocity;
            playerCamera.gameObject.SetActive(false);
            playerRagdollCamera.SetActive(true);
        }

        HandleJumpDive(viewYaw * Vector2.up);

        // Apply a constant stopping velocity to the player's run movement to slow the player down.
        Vector2 stoppingVelocity = -horizontalRunVelocity.normalized * runDeceleration * Time.deltaTime;
        if (stoppingVelocity.magnitude > horizontalRunVelocity.magnitude) // If the deceleration will cause the player to change velocity to negative, set to 0
        {
            horizontalRunVelocity = Vector2.zero;
        }
        else {
            horizontalRunVelocity += stoppingVelocity;
        }

        // If pressing in a direction, speed up in that direction
        if (moveDirection.magnitude > 0f && !ragdoll)
        {
            horizontalRunVelocity += normalizedMoveDirection * runAcceleration * Time.deltaTime;
            if (horizontalRunVelocity.magnitude > maxSpeed)
            {
                horizontalRunVelocity = horizontalRunVelocity.normalized * maxSpeed;
            }
        }
        Vector3 movement;

        if (!ragdoll)
        {
            // The resultant movement is a combination of the run movement (controlled by keys), and diving velocity.
            movement = new Vector3(horizontalRunVelocity.x + diveVelocity.x, 0f, horizontalRunVelocity.y + diveVelocity.y);            
        }
        else
        {
            // Apply a constant stopping velocity to the player's slide movement to slow the player down.
            Vector2 slideStopVelocity = -ragdollVelocity * ragdollDrag * Time.deltaTime;
            if (slideStopVelocity.magnitude > ragdollVelocity.magnitude) // If the deceleration will cause the player to change velocity to negative, set to 0
            {
                ragdollVelocity = Vector2.zero;
            }
            else {
                ragdollVelocity += slideStopVelocity;
            }

            movement = new Vector3(ragdollVelocity.x, 0f, ragdollVelocity.y);
        }

        transform.position = MovePlayer(movement * Time.deltaTime);
        transform.position = MovePlayer(verticalVelocity * Time.deltaTime * Vector3.up);
    }

    private void HandleJumpDive(Vector2 diveDirection)
    {
        if (!ragdoll) {
            // If in dive, check the arm length for in-air jumping/diving
            if (diving)
            {
                if (CheckArms(handprintDecal.transform.position, handprintDecal.transform.rotation, handprintDecal.transform.forward, armLength, out RaycastHit obstacleHit))
                {
                    timeSinceDiveReady = 0f;
                    // Fade the hand prints based on how far from the obstacle
                    if (obstacleHit.distance < fadeArmLength)
                    {
                        decalProjector.fadeFactor = 1;
                    }
                    else {
                        decalProjector.fadeFactor = 1 - (obstacleHit.distance - fadeArmLength) / (armLength - fadeArmLength);
                        Debug.Log("Obstacle: " + obstacleHit + " Fade: " + decalProjector.fadeFactor);
                    }
                }
                else
                {
                    timeSinceDiveReady += Time.deltaTime;
                    decalProjector.fadeFactor = 0;
                }
            }

            // If player hit jump within the buffer window, attempting jump
            bool attemptingJump = timeSinceJumpPressed <= jumpBufferTime;

            if (attemptingJump && !diving && timeSinceOnGround <= coyoteTime)
            {
                // If you are trying to jump and you are on the ground, apply jump force.
                verticalVelocity += jumpPower;
                timeSinceJump = 0;
                timeSinceOnGround = 0;
                timeSinceJumpPressed = Mathf.Infinity;
                Debug.Log("Jump");
            }
            else if (attemptingJump && diving && timeSinceDive <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                // If you pressed the jump button and you just dived from ground, apply jump force
                verticalVelocity += jumpDivePower.y - divePower.y;
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;

                Debug.Log("Jump chain from dive");
            }
            else if (attemptingJump && diving && timeSinceDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime)
            {
                // If you pressed the jump button while diving, and there's an obstacle, apply diving jump force
                verticalVelocity += divingJumpPower.y;
                diveVelocity += diveDirection * divingJumpPower.x;
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;

                Debug.Log("Jump while diving");
            }

            bool attemptingDive = timeSinceDivePressed <= jumpBufferTime;
            
            if (attemptingDive && !diving && timeSinceJump > chainActionBuffer && timeSinceOnGround <= coyoteTime)
            {
                // If you are trying to dive and you on the ground, apply dive force.
                verticalVelocity += divePower.y;
                diveVelocity += diveDirection * divePower.x;
                timeSinceDive = 0;
                timeSinceOnGround = 0;
                timeSinceDivePressed = Mathf.Infinity;

                Debug.Log("Dive");
            }
            else if (attemptingDive && !diving && timeSinceJump <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                // If you pressed dive and you just jumped off the ground, apply dive force.
                verticalVelocity += jumpDivePower.y - jumpPower;
                diveVelocity += diveDirection * divePower.x;
                timeSinceDive = 0;
                timeSinceDivePressed = Mathf.Infinity;
                Debug.Log("Dive chain from jump");
            }

            diving = pressingDive;
        } else {
            // If player hit jump within the buffer window, attempting jump
            bool attemptingJump = timeSinceJumpPressed <= jumpBufferTime;

            if (attemptingJump && ragdollVelocity.magnitude < ragdollControlSpeed)
            {
                ragdoll = false;
                diving = false;
                playerCamera.gameObject.SetActive(true);
                playerRagdollCamera.SetActive(false);
            }
        }

        // If you have dived, change the hitbox
        if (diving)
        {
            capsuleCollider.height = tuckedHeight;
            capsuleCollider.center = new Vector3(0f, anchorOffset + (fullHeight + fullHeight - tuckedHeight) / 2, 0f);
        }
        else
        {
            // How much the capsule will get taller this frame
            float heightChange = Mathf.Min(standupSpeed * Time.deltaTime, fullHeight - capsuleCollider.height);

            // Check if we will hit the ground with this height change
            RaycastHit groundHit;
            if (CastSelf(transform.position, transform.rotation, Vector3.down, heightChange, out groundHit))
            {
                // Move upward if we would grow into the ground.
                transform.position += new Vector3(0f, heightChange + groundDist - groundHit.distance, 0f);
            }

            capsuleCollider.height += heightChange;
            capsuleCollider.center = new Vector3(0f, anchorOffset + (fullHeight + fullHeight - capsuleCollider.height) / 2, 0f);
        }
        
        // If you are diving, rotate into diving position, else, move back upright.
        if (diving)
        {
            diveAngle += diveAngleSpeed * Time.deltaTime;
        }
        else if (!diving && !ragdoll)
        {
            diveAngle -= diveAngleSpeed * Time.deltaTime;
        }
        diveAngle = Mathf.Clamp(diveAngle, 0f, maxDiveAngle);

        // Reduce any diving forces by air friction
        if (diveVelocity.magnitude > 0f)
        {
            Vector2 dragVelocity = diveVelocity.normalized * diveDrag * Time.deltaTime;
            if (dragVelocity.magnitude >= diveVelocity.magnitude) {
                diveVelocity = Vector2.zero;
            }
            else {
                diveVelocity -= diveVelocity.normalized * diveDrag * Time.deltaTime;
            }
        }

        // Rotate the player based on diving angle
        playerCamera.parent.localRotation = Quaternion.Euler(Mathf.Clamp(diveAngle, 0f, maxDiveCameraAngle), 0f, 0f);
        playerModel.localRotation = Quaternion.Euler(diveAngle, 0f, 0f);

        // Add time to each time reference variable
        timeSinceJump += Time.deltaTime;
        timeSinceDive += Time.deltaTime;
        timeSinceJumpPressed += Time.deltaTime;
        timeSinceDivePressed += Time.deltaTime;
        timeSinceOnGround += Time.deltaTime;
    }

    public Vector3 MovePlayer(Vector3 movement)
    {
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;

        Vector3 remaining = movement;

        int bounces = 0;

        while (bounces < maxBounces && remaining.magnitude > epsilon)
        {
            // Do a cast of the collider to see if an object is hit during this
            // movement bounce
            float distance = remaining.magnitude;
            if (!CastSelf(position, rotation, remaining.normalized, distance, out RaycastHit hit))
            {
                // If there is no hit, move to desired position
                position += remaining;

                // Exit as we are done bouncing
                break;
            }

            // If we are overlapping with something, just exit.
            if (hit.distance == 0)
            {
                break;
            }

            float fraction = hit.distance / distance;
            // Set the fraction of remaining movement (minus some small value)
            position += remaining * (fraction);
            // Push slightly along normal to stop from getting caught in walls
            position += hit.normal * epsilon * 2;
            // Decrease remaining movement by fraction of movement remaining
            remaining *= (1 - fraction);

            // Plane to project rest of movement onto
            Vector3 planeNormal = hit.normal;

            // Only apply angular change if hitting something
            // Get angle between surface normal and remaining movement
            float angleBetween = Vector3.Angle(hit.normal, remaining) - 90.0f;

            // Normalize angle between to be between 0 and 1
            // 0 means no angle, 1 means 90 degree angle
            angleBetween = Mathf.Min(maxAngleShoveDegrees, Mathf.Abs(angleBetween));
            float normalizedAngle = angleBetween / maxAngleShoveDegrees;

            // Reduce the remaining movement by the remaining movement that ocurred
            remaining *= Mathf.Pow(1 - normalizedAngle, anglePower) * 0.9f + 0.1f;

            // Rotate the remaining movement to be projected along the plane 
            // of the surface hit (emulate pushing against the object)
            Vector3 projected = Vector3.ProjectOnPlane(remaining, planeNormal).normalized * remaining.magnitude;

            // If projected remaining movement is less than original remaining movement (so if the projection broke
            // due to float operations), then change this to just project along the vertical.
            if (projected.magnitude + epsilon < remaining.magnitude)
            {
                remaining = Vector3.ProjectOnPlane(remaining, Vector3.up).normalized * remaining.magnitude;
            }
            else
            {
                remaining = projected;
            }

            // Track number of times the character has bounced
            bounces++;
        }

        // We're done, player was moved as part of loop
        return position;
    }

    private bool CheckGrounded(out RaycastHit groundHit)
    {
        bool onGround = CastSelf(transform.position, transform.rotation, Vector3.down, groundDist, out groundHit);
        float angle = Vector3.Angle(groundHit.normal, Vector3.up);
        return onGround && angle < maxWalkingAngle;
    }

    public bool CastSelf(Vector3 pos, Quaternion rot, Vector3 dir, float dist, out RaycastHit hit)
    {
        // Get Parameters associated with the KCC
        Vector3 center = rot * (playerBody.localPosition + capsuleCollider.center) + pos;

        Debug.DrawRay(center, transform.forward, Color.yellow);

        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;

        // Get top and bottom points of collider
        Vector3 bottom = center + rot * Vector3.down * (height / 2 - radius);
        Vector3 top = center + rot * Vector3.up * (height / 2 - radius);

        // Check what objects this collider will hit when cast with this configuration excluding itself
        IEnumerable<RaycastHit> hits = Physics.CapsuleCastAll(
            top, bottom, radius, dir, dist, ~0, QueryTriggerInteraction.Ignore)
            .Where(hit => hit.collider.transform != playerBody);
        bool didHit = hits.Count() > 0;

        // Find the closest objects hit
        float closestDist = didHit ? Enumerable.Min(hits.Select(hit => hit.distance)) : 0;
        IEnumerable<RaycastHit> closestHit = hits.Where(hit => hit.distance == closestDist);

        // Get the first hit object out of the things the player collides with
        hit = closestHit.FirstOrDefault();

        // Return if any objects were hit
        return didHit;
    }

    private bool CheckArms(Vector3 pos, Quaternion rot, Vector3 dir, float dist, out RaycastHit hit)
    {
        // Check what objects this the ray will hit, excluding self
        IEnumerable<RaycastHit> hits = Physics.RaycastAll(
            pos, dir, dist, ~0, QueryTriggerInteraction.Ignore)
            .Where(hit => hit.collider.transform != transform);
        bool didHit = hits.Count() > 0;

        // Find the closest objects hit
        float closestDist = didHit ? Enumerable.Min(hits.Select(hit => hit.distance)) : 0;
        IEnumerable<RaycastHit> closestHit = hits.Where(hit => hit.distance == closestDist);

        // Get the first hit object out of the things the player collides with
        hit = closestHit.FirstOrDefault();

        if (didHit)
        {
            Debug.DrawRay(pos, dir.normalized * dist, Color.green, 3f);
        }
        else
        {
            Debug.DrawRay(pos, dir.normalized * dist, Color.red, 3f);
        }

        // Return if any objects were hit
        return didHit;
    }
}
