using FishNet.Object;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    [SerializeField] private GameObject playerBody;
    [SerializeField] private GameObject playerArms;
    [SerializeField] private GameObject playerBodyMesh;

    private Animator bodyAnimator;
    private Animator armAnimator;
    int isRunningHash;
    int isDivingHash;
    int jumpHash;

    void Awake()
    {
        bodyAnimator = playerBody.GetComponent<Animator>();
        armAnimator = playerArms.GetComponent<Animator>();
        isRunningHash = Animator.StringToHash("isRunning");
        isDivingHash = Animator.StringToHash("isDiving");
        jumpHash = Animator.StringToHash("jump");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            playerBodyMesh.SetActive(false);
            ToggleView(false);
        }
        else
        {
            playerArms.SetActive(false);
        }
    }

    public void AnimateJumpInDive()
    {
        armAnimator.SetTrigger("Push");
    }

    public void ToggleView(bool firstPerson)
    {
        playerBodyMesh.SetActive(!firstPerson);
        playerArms.SetActive(firstPerson);
    }

    public void AnimateRun(bool run)
    {
        bodyAnimator.SetBool(isRunningHash, run);
    }

    public void AnimateDive(bool dive)
    {
        bodyAnimator.SetBool(isDivingHash, dive);
    }

    public void Jump()
    {
        bodyAnimator.SetTrigger(jumpHash);
    }
}
