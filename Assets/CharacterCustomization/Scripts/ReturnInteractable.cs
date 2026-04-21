using UnityEngine;
using UnityEngine.Events;

public class ReturnInteractable : MonoBehaviour
{
    [SerializeField] private Transform returnTransform;
    [SerializeField] private float returnDuration = 0.5f;

    private Rigidbody rb;

    private bool isGrabbed = false;
    private float returnTimer = 0;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        returnTimer = Mathf.MoveTowards(returnTimer, returnDuration, Time.deltaTime);
        if (isGrabbed)
        {
            returnTimer = 0;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            float t = returnTimer / returnDuration;
            transform.position = Vector3.Lerp(transform.position, returnTransform.position, t);
            transform.rotation = Quaternion.Lerp(transform.rotation, returnTransform.rotation, t);
        }
    }

    public void OnGrabbed()
    {
        isGrabbed = true;
    }

    public void OnReleased()
    {
        isGrabbed = false;
    }
}