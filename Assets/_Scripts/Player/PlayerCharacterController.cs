using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using KinematicCharacterController;

public struct PlayerCharacterInputs
{
    public Vector2 moveVector;
    public Vector2 lookDelta;
    public bool jumpPressed;
    public bool divePressed;
}

public class PlayerCharacterController : NetworkBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;

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
    [SerializeField] private Vector2 divingJumpPower = new Vector2(4, 5.5f);
    [SerializeField] private Vector2 slidingPower = new Vector2(2, 0);

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
    [SerializeField] private float diveDragMultiplier = 5f;
    [SerializeField] private float diveDragOffset = -7f;

    [SerializeField] private float anchorOffset = -1.5f;
    [SerializeField] private float fullHeight = 2f;
    [SerializeField] private float tuckedHeight = 1f;
    [SerializeField] private float standupSpeed = 0.5f;

    [SerializeField] private int maxBounces = 5;

    [SerializeField] private Vector3 gravity = new Vector3(0, -25, 0);
    [SerializeField] private float diveDrag = 5;
    [SerializeField] private float addedDiveDrag = 0.1f;
    [SerializeField] private float slideDrag = 3;
    [SerializeField] private float ragdollDrag = 2;

    [SerializeField] private float minImpactVelocity = 1;
    [SerializeField] private float maxImpactVelocity = 10;
    [SerializeField] private float impactRecovery = 1;
    [SerializeField] private float impactToHeight = 1;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform playerModel;
    [SerializeField] private GameObject handprintDecal;
    [SerializeField] private GameObject playerRagdollCamera;

    private bool isControllingPlayer = false;

    private Vector2 cameraAngle;
    private Vector2 cameraAngleAccumulator;
    private Vector3 cameraPlanarDirection;
    private float diveAngle = 0f;

    private Vector3 moveInputVector;
    private Vector3 lookInputVector;

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
    private Vector3 diveVelocity;
    private Vector2 slideVelocity;
    private Vector2 ragdollVelocity;
    private Vector3 previousVelocity;

    private float landingVelocity = 0;

    private RaycastHit[] _probedRaycastHits = new RaycastHit[8];

    private Vector3 lastGroundedPosition;
    
    private PlayerInput playerInput;

    private CapsuleCollider capsuleCollider;
    private DecalProjector decalProjector;
    private PlayerCTFController playerCTFController;
    private RagdollController ragdollController;
    private PlayerAnimator playerAnimator;

    // Start is called before the first frame update
    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        capsuleCollider = playerBody.GetComponent<CapsuleCollider>();
        ragdollController = playerBody.GetComponent<RagdollController>();
        playerAnimator = GetComponent<PlayerAnimator>();

        if (playerBody.TryGetComponent(out PlayerCTFController ctfController))
        {
            playerCTFController = ctfController;
        }
    }

    private void Start() {
        Motor.CharacterController = this;

        // Set up the decal project based on variable settings
        handprintDecal.transform.localRotation = Quaternion.Euler(90f - maxDiveCameraAngle, 0f, 0f);
        decalProjector = handprintDecal.GetComponent<DecalProjector>();
        decalProjector.size = new Vector3(1f, 1f, armLength);
        decalProjector.fadeFactor = 0f;
    }

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        // Calculate new camera angle based on change
        if (!ragdoll)
        {
            cameraAngle.x += inputs.lookDelta.x * mouseSensitivity;
            cameraAngle.y += -inputs.lookDelta.y * mouseSensitivity;

            // Clamp max up and down angle
            cameraAngle.y = Mathf.Clamp(cameraAngle.y, minPitch, maxPitch);

            // Smoothly move toward the correct destination (avoid jitteriness)
            cameraAngleAccumulator = Vector2.Lerp(cameraAngleAccumulator, cameraAngle, mouseSnappiness * Time.deltaTime);
        }

        // Rotate the camera based on y movement
        playerCamera.localRotation = Quaternion.Euler(cameraAngleAccumulator.y, 0f, 0f);

        cameraPlanarDirection = Vector3.ProjectOnPlane(playerCamera.rotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(playerCamera.rotation * Vector3.up, Motor.CharacterUp).normalized;
        }
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
        
        moveInputVector = cameraPlanarRotation * new Vector3(inputs.moveVector.x, 0, inputs.moveVector.y);

        if (inputs.jumpPressed)
        {
            timeSinceJumpPressed = 0f;
        }

        if (inputs.divePressed)
        {
            timeSinceDivePressed = 0f;
        }
    }

    // Update is called once per frame
    // void FixedUpdate()
    // {
    //     // Check if the player is on the ground
    //     bool falling = !CheckGrounded(out RaycastHit groundhit);
    //     if (falling) // If falling, apply gravity downward
    //     {
    //         verticalVelocity += gravity * Time.deltaTime;
    //         if (!sliding)
    //         {
    //             timeInSlide = 0f;
    //         }
    //         timeSinceOnGround += Time.deltaTime;
    //     }
    //     else // If on ground, calculate impact force and reset vertical velocity
    //     {
    //         lastGroundedPosition = transform.position;

    //         if (timeSinceOnGround > 0f && minImpactVelocity <= -verticalVelocity && -verticalVelocity <= maxImpactVelocity)
    //         {
    //             landingVelocity = verticalVelocity;
    //             Debug.Log("landing Velocity: " + landingVelocity);
    //         }
    //         else if (-verticalVelocity > maxImpactVelocity)
    //         {
    //             EnableRagdollServer();
    //             EnableRagdoll(Vector3.down);
    //             Debug.Log("Too much landing impact! " + -verticalVelocity);
    //         }

    //         verticalVelocity = 0;
    //         timeSinceOnGround = 0f;
    //         if (diving)
    //         {
    //             timeInSlide += Time.deltaTime;
    //         }
    //     }

    //     // Make the player shorter (impact) depending on how hard they hit the ground
    //     if (landingVelocity < 0 && !diving)
    //     {
    //         // Apply first half of the height impact
    //         ManageHeight(landingVelocity * impactToHeight * Time.deltaTime * 0.5f);

    //         landingVelocity += impactRecovery * Time.deltaTime;

    //         if (landingVelocity > 0)
    //         {
    //             landingVelocity = 0;
    //         }

    //         // Apply second half of height impact (to get midpoint of height impact)
    //         ManageHeight(landingVelocity * impactToHeight * Time.deltaTime * 0.5f);
    //     }
    //     else if (landingVelocity == 0 && !diving) // If the player is on the ground standing, and not impacting, stand up
    //     {
    //         // How much the capsule will get taller this frame
    //         float heightChange = Mathf.Min(standupSpeed * Time.deltaTime, fullHeight - capsuleCollider.height);

    //         // Check if we will hit the ground with this height change
    //         ManageHeight(heightChange);
    //     }

    //     if (timeInSlide >= ragdollDelay && !sliding) // Move into sliding if you are diving and hit the ground
    //     {
    //         Debug.Log("Sliding!");
    //         sliding = true;
    //         slideVelocity = horizontalRunVelocity + diveVelocity;

    //         // Reduce the slide velocity as a fraction depending on adjacent / hypotenus
    //         float percentReduction = slideVelocity.magnitude / (slideVelocity.magnitude - landingVelocity);
    //         slideVelocity *= percentReduction;
    //     }

    //     HandleActions(viewYaw * Vector2.up);

    //     // Apply a constant stopping velocity to the player's run movement to slow the player down.
    //     if (!crouching)
    //     {
    //         horizontalRunVelocity = CalculateDrag(horizontalRunVelocity, runDeceleration);
    //     }
    //     else {
    //         horizontalRunVelocity = CalculateDrag(horizontalRunVelocity, walkDeceleration);
    //     }
        

    //     // If pressing in a direction, speed up in that direction
    //     if (moveDirection.magnitude > 0f && !ragdoll)
    //     {
    //         horizontalRunVelocity += normalizedMoveDirection * runAcceleration * Time.deltaTime;
    //         if (!crouching && horizontalRunVelocity.magnitude > maxSpeed)
    //         {
    //             horizontalRunVelocity = horizontalRunVelocity.normalized * maxSpeed;
    //         }
    //         else if (crouching && horizontalRunVelocity.magnitude > maxWalkSpeed)
    //         {
    //             horizontalRunVelocity = horizontalRunVelocity.normalized * maxWalkSpeed;
    //         }
    //     }
    //     Vector3 movement;

    //     // Slow the dive velocity down if not pressing in the direction of the dive velocity
    //     if (diveVelocity.magnitude > 0f)
    //     {
    //         // Project movement direction onto dive direction. Subtract dive direction from move projection. Remainder is extra drag to apply
    //         float moveProjectionDistance = Vector2.Dot(diveVelocity.normalized, normalizedMoveDirection);
    //         if (moveProjectionDistance >= 0f)
    //         {
    //             diveVelocity = CalculateDrag(diveVelocity, (1 - moveProjectionDistance) * addedDiveDrag);
    //         }
    //         else
    //         {
    //             diveVelocity = Vector2.zero;
    //         }
    //     }

    //     // if (sliding)
    //     // {
    //     //     // Apply a constant stopping velocity to the player's slide movement to slow the player down.
    //     //     slideVelocity = CalculateDrag(slideVelocity,slideDrag);

    //     //     movement = new Vector3(slideVelocity.x, 0f, slideVelocity.y);

    //     //     landingVelocity = 0;
    //     // }
    //     // else
    //     // {
    //     //     // The resultant movement is a combination of the run movement (controlled by keys), and diving velocity.
    //     //     movement = new Vector3(horizontalRunVelocity.x + diveVelocity.x, 0f, horizontalRunVelocity.y + diveVelocity.y);   
    //     // }
    //     movement = new Vector3(horizontalRunVelocity.x, verticalVelocity, horizontalRunVelocity.y) * Time.deltaTime;

    //     Vector3 newPosition = MovePlayer(ref movement);

    //     Debug.Log(movement / Time.deltaTime);

    //     horizontalRunVelocity.x = movement.x / Time.deltaTime;
    //     //verticalVelocity = movement.y / Time.deltaTime;
    //     horizontalRunVelocity.y = movement.z / Time.deltaTime;

    //     // Calculate the true movement vector (based on collisions), and modify the movement accordingly
    //     // Vector3 changeInMovement = (newPosition - transform.position) / Time.deltaTime;
    //     // verticalVelocity = changeInMovement.y;
    //     // horizontalRunVelocity = new Vector2(changeInMovement.x, changeInMovement.z);

    //     // Change the player's position
    //     transform.position = newPosition;
    // }

    private Vector3 HandleActions(Vector3 diveDirection, float deltaTime)
    {
        Vector3 jumpVector = Vector3.zero;
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
                    timeSinceDiveReady += deltaTime;
                }
            }

            // If player hit jump within the buffer window, attempting jump
            bool attemptingJump = timeSinceJumpPressed <= jumpBufferTime;

            if (attemptingJump && !diving && timeSinceOnGround <= coyoteTime)
            {
                // If you are trying to jump and you are on the ground, apply jump force.
                jumpVector.y = jumpPower;
                timeSinceJump = 0;
                timeSinceOnGround = 0;
                timeSinceJumpPressed = Mathf.Infinity;
            }
            else if (attemptingJump && diving && timeSinceEnterDive <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                // If you pressed the jump button and you just dived from ground, apply jump force
                jumpVector.y = jumpPower;
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;
            }
            else if (attemptingJump && diving && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime && !sliding)
            {
                // If you pressed the jump button while diving, and there's an obstacle, leave dive
                timeSinceJump = 0;
                timeSinceJumpPressed = Mathf.Infinity;
                timeSinceExitDive = 0;
                diving = false;
                playerAnimator.AnimateJumpInDive();
            }

            bool attemptingDive = timeSinceDivePressed <= jumpBufferTime;
            
            if (attemptingDive && !diving && timeSinceJump > chainActionBuffer && timeSinceOnGround <= coyoteTime)
            {
                Debug.Log("Diving!");
                // If you are trying to dive and you on the ground, apply dive force.
                // verticalVelocity += divePower.y;
                //diveVelocity += diveDirection * divePower.x;
                timeSinceEnterDive = 0;
                timeSinceOnGround = 0;
                timeSinceDivePressed = Mathf.Infinity;
                diving = true;
            }
            else if (attemptingDive && !diving && timeSinceJump <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            {
                Debug.Log("Diving!");
                // If you pressed dive and you just jumped off the ground, apply dive force.
                //verticalVelocity += jumpDivePower.y - jumpPower;
                //diveVelocity += diveDirection * divePower.x;
                timeSinceEnterDive = 0;
                timeSinceDivePressed = Mathf.Infinity;
                diving = true;
            }
            else if (attemptingDive && diving && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime && !sliding)
            {
                // If you pressed the dive button while diving, and there's an obstacle, add dive momentum forward
                Debug.Log("Dive again!");
                jumpVector.y += divingJumpPower.y;
                jumpVector += diveDirection * divingJumpPower.x;
                timeSinceEnterDive = 0;
                timeSinceDivePressed = Mathf.Infinity;
                playerAnimator.AnimateJumpInDive();
            }
            else if (attemptingDive && timeSinceExitDive <= chainActionBuffer && timeSinceEnterDive > chainActionBuffer && timeSinceDiveReady <= coyoteTime + chainActionBuffer && !sliding)
            {
                // If you pressed the dive button right after jumping from dive, and there's an obstacle, add dive momentum forward
                jumpVector.y += divingJumpPower.y;
                jumpVector += diveDirection * divingJumpPower.x;
                timeSinceDivePressed = Mathf.Infinity;
                playerAnimator.AnimateJumpInDive();
            }

            // If you crouch and dive at the same time on the ground, you will slide
            // bool attemptingSlide = timeSinceCrouchPressed <= jumpBufferTime;
            // if (attemptingSlide && diving && timeSinceEnterDive <= chainActionBuffer && timeSinceOnGround <= chainActionBuffer + coyoteTime)
            // {
            //     diveVelocity += diveDirection * slidingPower.x;
            //     timeSinceCrouchPressed = Mathf.Infinity;
            //     slideVelocity = horizontalRunVelocity + diveVelocity;
            //     sliding = true;
            // }
            // if (sliding && attemptingJump)
            // {
            //     sliding = false;
            //     diving = false;
            //     timeInSlide = 0f;
            // }

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
            Motor.SetCapsuleDimensions(0.5f, 1f, 0f);
        }

        return jumpVector;
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

    private Vector3 CalculateDrag(Vector3 curVelocity, float dragForce, float deltaTime)
    {
        Vector3 stoppingVelocity = -curVelocity.normalized * dragForce * deltaTime;
        if (stoppingVelocity.magnitude > curVelocity.magnitude) // If the deceleration will cause the player to change velocity to negative, set to 0
        {
            curVelocity = Vector3.zero;
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
            if (Motor.CharacterSweep(Motor.TransientPosition, Motor.TransientRotation, Vector3.zero, heightChange, out groundHit, _probedRaycastHits, ~0, QueryTriggerInteraction.Ignore) > 0)
            {
                Motor.SetPosition(Motor.TransientPosition + (Vector3.up * (heightChange + groundDist - groundHit.distance - epsilon)));
            }

            // if (CastSelf(transform.position, transform.rotation, Vector3.down, heightChange, out groundHit))
            // {
            //     // Move upward if we would grow into the ground.
            //     transform.position += new Vector3(0f, heightChange + groundDist - groundHit.distance - epsilon, 0f);
            // }
        }
        else if (heightChange < 0)
        {
            // Check if we will hit the ground with this height change
            // RaycastHit groundHit;
            // if (CastSelf(transform.position, transform.rotation, Vector3.down, -heightChange + groundDist, out groundHit))
            // {
            //     // Move right above the ground
            //     transform.position += new Vector3(0f, heightChange, 0f);
            // }
            // else {
            //     transform.position += new Vector3(0f, heightChange, 0f);
            // }

            Motor.SetPosition(Motor.TransientPosition + (Vector3.up * heightChange));
        }        

        Motor.SetCapsuleDimensions(0.5f, Motor.Capsule.height + heightChange, anchorOffset + (fullHeight + fullHeight - Motor.Capsule.height) / 2);
    }

    public void ResetPlayer()
    {
        verticalVelocity = 0;
        landingVelocity = 0;
        diving = false;
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

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
        // {
        //     // Smoothly interpolate from current to target look direction
        //     Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

        //     // Set the current rotation (which will be used by the KinematicCharacterMotor)
        //     currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
        // }

        currentRotation = Quaternion.Euler(0f, cameraAngleAccumulator.x, 0f);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector3 reorientedInput;
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            // Reorient velocity on slope
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;
            previousVelocity = Motor.GetDirectionTangentToSurface(previousVelocity, Motor.GroundingStatus.GroundNormal) * previousVelocity.magnitude;

            // Get the reoriented input based on ground normal
            Vector3 inputRight = Vector3.Cross(moveInputVector, Motor.CharacterUp);
            reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized;

            diveVelocity = Motor.GetDirectionTangentToSurface(diveVelocity, Motor.GroundingStatus.GroundNormal) * diveVelocity.magnitude;
        } else {
            verticalVelocity = currentVelocity.y;
            currentVelocity.y = 0;
            previousVelocity.y = 0;

            // Get the reoriented input based on ground normal
            reorientedInput = moveInputVector;

            diveVelocity = Motor.GetDirectionTangentToSurface(diveVelocity, Motor.CharacterUp) * diveVelocity.magnitude;
        }

        Debug.Log(diveVelocity + ", Cross product" + Vector3.Dot(diveVelocity, previousVelocity - currentVelocity));

        // Calculate remaining dive velocity
        diveVelocity = CalculateDrag(diveVelocity, Vector3.Dot(diveVelocity, previousVelocity - currentVelocity), 1);

        // Calculate the necessary drag on dive velocity, based on how well the input aligns with the dive velocity. (More alignment => less drag)
        float diveDrag = Vector3.Dot(reorientedInput.normalized, diveVelocity.normalized) * diveDragMultiplier + diveDragOffset;
        diveVelocity = CalculateDrag(diveVelocity, -diveDrag, deltaTime);

        Debug.Log("dive after drag: " + diveVelocity);

        // Apply a constant stopping velocity to the player's run movement to slow the player down.
        currentVelocity = CalculateDrag(currentVelocity, runDeceleration, deltaTime);

        // If pressing in a direction, speed up in that direction
        if (reorientedInput.magnitude > 0f && !ragdoll)
        {
            currentVelocity += reorientedInput.normalized * runAcceleration * deltaTime;
        }
        if (!crouching && currentVelocity.magnitude > maxSpeed)
        {
            currentVelocity = currentVelocity.normalized * maxSpeed;
        }
        else if (crouching && currentVelocity.magnitude > maxWalkSpeed)
        {
            currentVelocity = currentVelocity.normalized * maxWalkSpeed;
        }

        //Re-add the dive velocity after affecting the current velocity
        currentVelocity += diveVelocity;

        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity.y = verticalVelocity;
            // Apply gravity to the player if you are not grounded
            currentVelocity += gravity * deltaTime;
        }

        Vector3 jumpVector = HandleActions(cameraPlanarDirection, deltaTime);

        if (jumpVector.magnitude > 0f)
        {
            if (jumpVector.y > 0f)
            {
                Motor.ForceUnground(0.1f);
            }

            // Add to the return velocity and reset jump state
            currentVelocity += jumpVector - Vector3.Project(currentVelocity, Motor.CharacterUp);

            diveVelocity += Vector3.ProjectOnPlane(jumpVector, Motor.CharacterUp);
        }

        previousVelocity = currentVelocity;
        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            previousVelocity.y = 0;
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // Handle jumping while sliding
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            timeSinceOnGround = 0f;
        }
        else
        {
            // Keep track of time since we were last able to jump (for grace period)
            timeSinceOnGround += deltaTime;
            
        }

        // If you are diving, rotate into diving position, else, move back upright.
        if (diving)
        {
            diveAngle += diveAngleSpeed * deltaTime;
        }
        else if (!diving && !ragdoll)
        {
            diveAngle -= diveAngleSpeed * deltaTime;
        }
        diveAngle = Mathf.Clamp(diveAngle, 0f, maxDiveAngle);

        // Rotate the player based on diving angle
        playerCamera.parent.localRotation = Quaternion.Euler(Mathf.Clamp(diveAngle, 0f, maxDiveCameraAngle), 0f, 0f);
        playerModel.localRotation = Quaternion.Euler(diveAngle, 0f, 0f);

        if (!diving) // If the player is on the ground standing, and not impacting, stand up
        {
            // How much the capsule will get taller this frame
            float heightChange = Mathf.Min(standupSpeed * deltaTime, fullHeight - capsuleCollider.height);

            // Check if we will hit the ground with this height change
            ManageHeight(heightChange);
        }

        // Add time to each time reference variable
        timeSinceJump += deltaTime;
        timeSinceEnterDive += deltaTime;
        timeSinceExitDive += deltaTime;
        timeSinceJumpPressed += deltaTime;
        timeSinceDivePressed += deltaTime;
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
}
