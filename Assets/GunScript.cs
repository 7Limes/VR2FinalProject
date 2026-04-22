using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRLaserGun : MonoBehaviour
{
    public GameObject bulletPrefab;
    public Transform spawnPoint;
    public float fireForce = 20f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        // Subscribe to the "Trigger" press event
        grabInteractable.activated.AddListener(FireLaser);
    }

    void OnDestroy()
    {
        grabInteractable.activated.RemoveListener(FireLaser);
    }

    void FireLaser(ActivateEventArgs args)
    {
        GameObject bullet = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);
        Rigidbody rb = bullet.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(spawnPoint.forward * fireForce, ForceMode.Impulse);
        }

        // Cleanup bullet after 3 seconds if it hits nothing
        Destroy(bullet, 3f);
    }
}