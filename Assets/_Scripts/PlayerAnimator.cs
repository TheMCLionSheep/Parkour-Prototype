using FishNet.Object;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    [SerializeField] private GameObject playerBody;
    [SerializeField] private GameObject playerArms;
    [SerializeField] private GameObject playerBodyMesh;

    private Animator bodyAnimator;
    private Animator armAnimator;

    void Awake()
    {
        bodyAnimator = playerBody.GetComponent<Animator>();
        armAnimator = playerArms.GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            playerBodyMesh.SetActive(false);
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
}
