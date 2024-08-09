using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class PlayerMovement : NetworkBehaviour
{
    public const float minPitch = -90f;
    public const float maxPitch = 70f;
    public const float epsilon = 0.001f;
    public const float maxAngleShoveDegrees = 60f;

    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float mouseSnappiness = 10f;

    [SerializeField] private float maxSpeed = 7.5f;
    [SerializeField] private float runAcceleration = 50f;
    [SerializeField] private float runDeceleration = 30f;
    [SerializeField] private float maxWalkSpeed = 4f;
    [SerializeField] private float walkAcceleration = 40f;
    [SerializeField] private float walkDeceleration = 30f;

    [SerializeField] private float groundDist = 0.01f;
    [SerializeField] private float maxWalkingAngle = 60f;
    [SerializeField] private float anglePower = 0.5f;

    [SerializeField] private float jumpPower = 7f;
    [SerializeField] private Vector2 divePower = new Vector2(5, 5);
    [SerializeField] private Vector2 jumpDivePower = new Vector2(5, 7);
    [SerializeField] private Vector2 divingJumpPower = new Vector2(5, 7);
    [SerializeField] private Vector2 slidingPower = new Vector2(5, 0);

    [SerializeField] private float chainActionBuffer = 0.05f;
    [SerializeField] private float coyoteTime = 0.05f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float reuseDelay = 0.2f;
    [SerializeField] private float slideDelay = 0.1f;
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

    [SerializeField] private float gravity = -25;
    [SerializeField] private float diveDrag = 5;
    [SerializeField] private float addedDiveDrag = 0.1f;
    [SerializeField] private float slideDrag = 3;
    [SerializeField] private float ragdollDrag = 2;

    [SerializeField] private float minImpactVelocity = 1;
    [SerializeField] private float maxImpactVelocity = 10;
    [SerializeField] private float impactRecovery = 1;
    [SerializeField] private float impactToHeight = 1;

    [SerializeField] private float voidLevel;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform playerModel;
    [SerializeField] private GameObject handprintDecal;
    [SerializeField] private GameObject playerRagdollCamera;

    private bool isControllingPlayer = false;

    private Vector2 cameraAngle;
    private Vector2 cameraAngleAccumulator;
    private float diveAngle = 0f;

    private float timeSinceJump = Mathf.Infinity;
    private float timeSinceEnterDive = Mathf.Infinity;
    private float timeSinceExitDive = Mathf.Infinity;
    private float timeSinceJumpPressed = Mathf.Infinity;
    private float timeSinceDivePressed = Mathf.Infinity;
    private float timeSinceCrouchPressed = Mathf.Infinity;
    private float timeSinceOnGround = 0f;
    private float timeSinceDiveReady = Mathf.Infinity;
    private float timeInSlide = 0f;
    private float timeInRagdoll = 0f;

    private bool pressingDive = false;
    private bool pressingCrouch = false;
    private bool diving = false;
    private bool ragdoll = false;
    private bool crouching = false;
    private bool sliding = false;

    private float verticalVelocity;
    private Vector2 horizontalRunVelocity;
    private Vector2 diveVelocity;
    private Vector2 slideVelocity;
    private Vector2 ragdollVelocity;

    private float landingVelocity = 0;

    private Vector3 lastGroundedPosition;
    
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction diveAction;
    private InputAction crouchAction;

    private CapsuleCollider capsuleCollider;
    private DecalProjector decalProjector;
    private PlayerCTFController playerCTFController;
    private RagdollController ragdollController;
    private PlayerAnimator playerAnimator;

    // Start is called before the first frame update
    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        playerInput = GetComponent<PlayerInput>();
        capsuleCollider = playerBody.GetComponent<CapsuleCollider>();
        ragdollController = playerBody.GetComponent<RagdollController>();
        playerAnimator = GetComponent<PlayerAnimator>();

        if (playerBody.TryGetComponent(out PlayerCTFController ctfController))
        {
            playerCTFController = ctfController;
        }

        lookAction = playerInput.actions.FindAction("Look");
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        diveAction = playerInput.actions.FindAction("Dive");
        crouchAction = playerInput.actions.FindAction("Crouch");
    }

    private void Start() {
        // Set up the decal project based on variable settings
        handprintDecal.transform.localRotation = Quaternion.Euler(maxDiveCameraAngle, 0f, 0f);
        decalProjector = handprintDecal.GetComponent<DecalProjector>();
        decalProjector.size = new Vector3(1f, 1f, armLength);
        decalProjector.fadeFactor = 0f;
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
        isControllingPlayer = true;
        playerCamera.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        jumpAction.performed += JumpPressed;
        diveAction.performed += DivePressed;
        crouchAction.performed += CrouchPressed;
        crouchAction.canceled += CrouchCancelled;
    }

    private void OnDisable() {
        jumpAction.performed -= JumpPressed;
        diveAction.performed -= DivePressed;
        crouchAction.performed -= CrouchPressed;
        crouchAction.canceled -= CrouchCancelled;
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
    }

    private void CrouchPressed(InputAction.CallbackContext context)
    {
        if (timeSinceCrouchPressed >= reuseDelay)
        {
            timeSinceCrouchPressed = 0f;
        }
        pressingCrouch = true;
    }

    private void CrouchCancelled(InputAction.CallbackContext context)
    {
        pressingCrouch = false;
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
            cameraAngle.x += lookDelta.x * mouseSensitivity;
            cameraAngle.y += -lookDelta.y * mouseSensitivity;

            // Clamp max up and down angle
            cameraAngle.y = Mathf.Clamp(cameraAngle.y, minPitch, maxPitch);

            // Smoothly move toward the correct destination (avoid jitteriness)
            cameraAngleAccumulator = Vector2.Lerp(cameraAngleAccumulator, cameraAngle, mouseSnappiness * Time.deltaTime);
        }

        // Rotate the camera based on y movement
        playerCamera.localRotation = Quaternion.Euler(cameraAngleAccumulator.y, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, cameraAngleAccumulator.x, 0f);
        
        var moveDirection = new Vector3(playerMove.x, 0, playerMove.y);

        // Get the move direction based on player rotation
        var viewYaw = Quaternion.Euler(0, 0, -cameraAngleAccumulator.x);
        Vector2 rotatedVector = viewYaw * playerMove;
        Vector2 normalizedMoveDirection = rotatedVector.normalized * Mathf.Min(rotatedVector.magnitude, 1.0f);

        // Check if the player is on the ground
        bool falling = !CheckGrounded(out RaycastHit groundhit);
        if (falling) // If falling, apply gravity downward
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (!sliding)
            {
                timeInSlide = 0f;
            }
            timeSinceOnGround += Time.deltaTime;
        }
        else // If on ground, calculate impact force and reset vertical velocity
        {
            lastGroundedPosition = transform.position;

            if (timeSinceOnGround > 0f && minImpactVelocity <= -verticalVelocity && -verticalVelocity <= maxImpactVelocity)
            {
                landingVelocity = verticalVelocity;
                Debug.Log("landing Velocity: " + landingVelocity);
            }
            else if (-verticalVelocity > maxImpactVelocity)
            {
                EnableRagdollServer();
                EnableRagdoll(Vector3.down);
                Debug.Log("Too much landing impact! " + -verticalVelocity);
            }

            verticalVelocity = 0;
            timeSinceOnGround = 0f;
            if (diving)
            {
                timeInSlide += Time.deltaTime;
            }
        }

        // Make the player shorter (impact) depending on how hard they hit the ground
        if (landingVelocity < 0 && !diving)
        {
            // Apply first half of the height impact
            ManageHeight(landingVelocity * impactToHeight * Time.deltaTime * 0.5f);

            landingVelocity += impactRecovery * Time.deltaTime;

            if (landingVelocity > 0)
            {
                landingVelocity = 0;
            }

            // Apply second half of height impact (to get midpoint of height impact)
            ManageHeight(landingVelocity * impactToHeight * Time.deltaTime * 0.5f);
        }
        else if (landingVelocity == 0 && !diving) // If the player is on the ground standing, and not impacting, stand up
        {
            // How much the capsule will get taller this frame
            float heightChange = Mathf.Min(standupSpeed * Time.deltaTime, fullHeight - capsuleCollider.height);

            // Check if we will hit the ground with this height change
            ManageHeight(heightChange);
        }

        if (timeInSlide >= ragdollDelay && !sliding) // Move into sliding if you are diving and hit the ground
        {
            Debug.Log("Sliding!");
            sliding = true;
            slideVelocity = horizontalRunVelocity + diveVelocity;

            // Reduce the slide velocity as a fraction depending on adjacent / hypotenus
            float percentReduction = slideVelocity.magnitude / (slideVelocity.magnitude - landingVelocity);
            slideVelocity *= percentReduction;
        }

        HandleActions(viewYaw * Vector2.up);

        // Apply a constant stopping velocity to the player's run movement to slow the player down.
        if (!crouching)
        {
            horizontalRunVelocity = CalculateDrag(horizontalRunVelocity, runDeceleration);
        }
        else {
            horizontalRunVelocity = CalculateDrag(horizontalRunVelocity, walkDeceleration);
        }
        

        // If pressing in a direction, speed up in that direction
        if (moveDirection.magnitude > 0f && !ragdoll)
        {
            horizontalRunVelocity += normalizedMoveDirection * runAcceleration * Time.deltaTime;
            if (!crouching && horizontalRunVelocity.magnitude > maxSpeed)
            {
                horizontalRunVelocity = horizontalRunVelocity.normalized * maxSpeed;
            }
            else if (crouching && horizontalRunVelocity.magnitude > maxWalkSpeed)
            {
                horizontalRunVelocity = horizontalRunVelocity.normalized * maxWalkSpeed;
            }
        }
        Vector3 movement;

        // Slow the dive velocity down if not pressing in the direction of the dive velocity
        if (diveVelocity.magnitude > 0f)
        {
            // Project movement direction onto dive direction. Subtract dive direction from move projection. Remainder is extra drag to apply
            float moveProjectionDistance = Vector2.Dot(diveVelocity.normalized, normalizedMoveDirection);
            if (moveProjectionDistance >= 0f)
            {
                diveVelocity = CalculateDrag(diveVelocity, (1 - moveProjectionDistance) * addedDiveDrag);
            }
            else
            {
                diveVelocity = Vector2.zero;
            }
        }

        if (sliding)
        {
            // Apply a constant stopping velocity to the player's slide movement to slow the player down.
            slideVelocity = CalculateDrag(slideVelocity,slideDrag);

            movement = new Vector3(slideVelocity.x, 0f, slideVelocity.y);

            landingVelocity = 0;
        }
        else
        {
            // The resultant movement is a combination of the run movement (controlled by keys), and diving velocity.
            movement = new Vector3(horizontalRunVelocity.x + diveVelocity.x, 0f, horizontalRunVelocity.y + diveVelocity.y);   
        }

        transform.position = MovePlayer((movement + (verticalVelocity * Vector3.up)) * Time.deltaTime);

        if (transform.position.y <= voidLevel)
        {
            verticalVelocity = 0;
            landingVelocity = 0;
            if (playerCTFController != null)
            {
                playerCTFController.DropPlayerFlag();
                playerCTFController.RespawnPlayer();
            }
        }
    }

    private void HandleActions(Vector2 diveDirection)
    {
        decalProjector.fadeFactor = 0;
        if (!ragdoll) {
            // If in dive, check the arm length for in-air jumping/diving
            if (diving && !sliding)
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
            }
            else if (attemptingJump && diving && timeSinceEnterDive <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                // If you pressed the jump button and you just dived from ground, apply jump force
                verticalVelocity += jumpPower;
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;
            }
            else if (attemptingJump && diving && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime && !sliding)
            {
                // Alternative controls: If you pressed the jump button while diving, and there's an obstacle, leave dive
                verticalVelocity += divePower.y;
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;
                timeSinceExitDive = 0;
                diving = false;
                playerAnimator.AnimateJumpInDive();
            }

            bool attemptingDive = timeSinceDivePressed <= jumpBufferTime;
            
            if (attemptingDive && !diving && timeSinceJump > chainActionBuffer && timeSinceOnGround <= coyoteTime)
            {
                // If you are trying to dive and you on the ground, apply dive force.
                // verticalVelocity += divePower.y;
                diveVelocity += diveDirection * divePower.x;
                timeSinceEnterDive = 0;
                timeSinceOnGround = 0;
                timeSinceDivePressed = Mathf.Infinity;
                diving = true;
            }
            else if (attemptingDive && !diving && timeSinceJump <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                // If you pressed dive and you just jumped off the ground, apply dive force.
                //verticalVelocity += jumpDivePower.y - jumpPower; // TODO fix this!!
                diveVelocity += diveDirection * divePower.x;
                timeSinceEnterDive = 0;
                timeSinceDivePressed = Mathf.Infinity;
                diving = true;
            }
            else if (attemptingDive && diving && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime && !sliding)
            {
                // Alternative control: If you pressed the dive button while diving, and there's an obstacle, add dive momentum forward
                verticalVelocity += divingJumpPower.y;
                diveVelocity += diveDirection * divingJumpPower.x;
                timeSinceEnterDive = 0;
                timeSinceDivePressed = Mathf.Infinity;
                playerAnimator.AnimateJumpInDive();
            }
            else if (attemptingDive && timeSinceExitDive <= chainActionBuffer && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime && !sliding)
            {
                // Alternative control: If you pressed the dive button while diving, and there's an obstacle, add dive momentum forward
                verticalVelocity += divingJumpPower.y;
                diveVelocity += diveDirection * divingJumpPower.x;
                timeSinceDivePressed = Mathf.Infinity;
                playerAnimator.AnimateJumpInDive();
            }

            // If you crouch and dive at the same time on the ground, you will slide
            bool attemptingSlide = timeSinceCrouchPressed <= jumpBufferTime;
            if (attemptingSlide && diving && timeSinceEnterDive <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                verticalVelocity = -divePower.y;
                diveVelocity += diveDirection * slidingPower.x;
                timeSinceCrouchPressed = Mathf.Infinity;
                slideVelocity = horizontalRunVelocity + diveVelocity;
                sliding = true;
            }
            if (sliding && attemptingJump)
            {
                sliding = false;
                diving = false;
                timeInSlide = 0f;
            }

            crouching = pressingCrouch;
        } else {
            // If in ragdoll, and hit jump, leave the ragdoll
            bool attemptingJump = timeSinceJumpPressed <= jumpBufferTime;

            if (attemptingJump && ragdollController.GetRagdollVelocity() < ragdollControlSpeed)
            {
                DisableRagdoll();
                DisableRagdollServer();
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
            // // How much the capsule will get taller this frame
            // float heightChange = Mathf.Min(standupSpeed * Time.deltaTime, fullHeight - capsuleCollider.height);

            // // Check if we will hit the ground with this height change
            // ManageHeight(heightChange);
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
            diveVelocity = CalculateDrag(diveVelocity, diveDrag);
        }

        // Rotate the player based on diving angle
        playerCamera.parent.localRotation = Quaternion.Euler(Mathf.Clamp(diveAngle, 0f, maxDiveCameraAngle), 0f, 0f);
        playerModel.localRotation = Quaternion.Euler(diveAngle, 0f, 0f);

        // Add time to each time reference variable
        timeSinceJump += Time.deltaTime;
        timeSinceEnterDive += Time.deltaTime;
        timeSinceExitDive += Time.deltaTime;
        timeSinceJumpPressed += Time.deltaTime;
        timeSinceDivePressed += Time.deltaTime;
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

            if (playerCTFController != null)
            {
                playerCTFController.CollideWithObject(hit.collider, movement / Time.deltaTime);
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

        if (onGround && playerCTFController != null)
        {
            playerCTFController.CheckOnCaptureZone(groundHit.transform);
        }

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
        //TODO: make the raycast more forgiving (potentially based on speed)

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

    private Vector2 CalculateDrag(Vector2 curVelocity, float dragForce)
    {
        Vector2 stoppingVelocity = -curVelocity.normalized * dragForce * Time.deltaTime;
        if (stoppingVelocity.magnitude > curVelocity.magnitude) // If the deceleration will cause the player to change velocity to negative, set to 0
        {
            curVelocity = Vector2.zero;
        }
        else {
            curVelocity += stoppingVelocity;
        }
        return curVelocity;
    }

    private void ManageHeight(float heightChange)
    {
        if (heightChange >= 0)
        {
            // Check if we will hit the ground with this height change
            RaycastHit groundHit;
            if (CastSelf(transform.position, transform.rotation, Vector3.down, heightChange, out groundHit))
            {
                // Move upward if we would grow into the ground.
                transform.position += new Vector3(0f, heightChange + groundDist - groundHit.distance - epsilon, 0f);
            }
        }
        else if (heightChange < 0)
        {
            // Check if we will hit the ground with this height change
            RaycastHit groundHit;
            if (CastSelf(transform.position, transform.rotation, Vector3.down, -heightChange + groundDist, out groundHit))
            {
                // Move right above the ground
                transform.position += new Vector3(0f, heightChange, 0f);
            }
            else {
                transform.position += new Vector3(0f, heightChange, 0f);
            }
        }        

        capsuleCollider.height += heightChange;
        capsuleCollider.center = new Vector3(0f, anchorOffset + (fullHeight + fullHeight - capsuleCollider.height) / 2, 0f);
    }

    public bool CanTackle()
    {
        return diving;
    }

    public Vector3 GetLastGroundedPosition()
    {
        return lastGroundedPosition;
    }

    [ServerRpc]
    private void EnableRagdollServer()
    {
        EnableRagdollObserver();
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void EnableRagdollObserver()
    {
        EnableRagdoll(Vector3.down);
    }

    public void EnableRagdoll(Vector3 collisionForce)
    {
        if (isControllingPlayer)
        {
            playerAnimator.ToggleView(false);
            playerCamera.gameObject.SetActive(false);
            playerRagdollCamera.SetActive(true);
        }

        ragdoll = true;
        ragdollController.EnableRagdoll();
        ragdollController.ApplyForceOnRagdoll(collisionForce);
    }

    [ServerRpc]
    private void DisableRagdollServer()
    {
        DisableRagdollObserver();
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void DisableRagdollObserver()
    {
        DisableRagdoll();
    }

    private void DisableRagdoll()
    {
        ragdoll = false;
        ragdollController.DisableRagdoll();

        if (isControllingPlayer)
        {
            playerAnimator.ToggleView(true);
            playerCamera.gameObject.SetActive(true);
            playerRagdollCamera.SetActive(false);
        }
    }
}
