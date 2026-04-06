using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRSlider : MonoBehaviour
{
    [SerializeField] private XRSimpleInteractable interactable;

    [SerializeField] private GameObject thumbVisual;

    [SerializeField] private Transform lowerBound;
    [SerializeField] private Transform upperBound;

    [SerializeField] private Vector2 valueRange = new Vector2(0, 1);
    [SerializeField] private float currentValue = 0.5f;

    [SerializeField] private float notchInterval = 0.0f;

    [SerializeField] private UnityEvent changeEvent = null;


    private IXRSelectInteractor currentInteractor = null;

    public void OnSelectEnter()
    {
        currentInteractor = interactable.firstInteractorSelecting;
    }

    public void OnSelectExit()
    {
        currentInteractor = null;
    }

    public float getValue()
    {
        return currentValue;
    }

    void Start()
    {
        UpdateThumb();
    }

    void Update()
    {
        if (currentInteractor != null)
        {
            float t = Mathf.Clamp01(Vec3InverseLerp(lowerBound.position, upperBound.position, currentInteractor.transform.position));

            // Update current value
            currentValue = Mathf.Lerp(valueRange.x, valueRange.y, t);

            // Round to nearest notch if applicable
            if (notchInterval > 0)
            {
                currentValue = Mathf.Round(currentValue / notchInterval) * notchInterval;
                currentValue = Mathf.Clamp(currentValue, valueRange.x, valueRange.y);
            }

            // Invoke change event
            if (changeEvent != null)
            {
                changeEvent.Invoke();
            }

            UpdateThumb();
        }
    }

    private void UpdateThumb()
    {
        float t = Mathf.InverseLerp(valueRange.x, valueRange.y, currentValue);

        // Update thumb position
        Vector3 thumbPosition = Vector3.Lerp(lowerBound.localPosition, upperBound.localPosition, t);
        thumbPosition.y = thumbVisual.transform.localPosition.y;
        thumbVisual.transform.localPosition = thumbPosition;
    }

    private static float Vec3InverseLerp(Vector3 a, Vector3 b, Vector3 value)
    {
        Vector3 AB = b - a;
        Vector3 AV = value - a;
        return Vector3.Dot(AV, AB) / Vector3.Dot(AB, AB);
    }
}
