using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [SerializeField] GameObject characterRoot;
    [SerializeField] GameObject characterRig;
    [SerializeField] Collider mainCollider;
    [SerializeField] Animator animator;

    [SerializeField] float respawnHeight = 1.1f;
    
    Collider[] ragdollColliders;
    Rigidbody[] ragdollRigidbodies;

    void Start()
    {
        GetRagdollParts();
        DisableRagdoll();
    }

    private void GetRagdollParts()
    {
        // Find all colliders and rigidbodies inside the character rig
        ragdollColliders = characterRig.GetComponentsInChildren<Collider>();
        ragdollRigidbodies = characterRig.GetComponentsInChildren<Rigidbody>();
    }

    public void EnableRagdoll()
    {
        animator.enabled = false;

        foreach (Collider col in ragdollColliders)
        {
            col.enabled = true;
        }

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = false;
        }

        mainCollider.enabled = false;
    }

    public void DisableRagdoll()
    {
        foreach (Collider col in ragdollColliders)
        {
            col.enabled = false;
        }

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = true;
        }

        mainCollider.enabled = true;

        characterRoot.transform.position = characterRig.transform.GetChild(0).position + (Vector3.up * respawnHeight);

        animator.enabled = true;
    }

    public void ApplyForceOnRagdoll(Vector3 force)
    {
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    public float GetRagdollVelocity()
    {
        Debug.Log(characterRig.GetComponentInChildren<Rigidbody>().velocity.magnitude);
        return characterRig.GetComponentInChildren<Rigidbody>().velocity.magnitude;
    }
}
