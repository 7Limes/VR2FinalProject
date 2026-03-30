using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRButton : MonoBehaviour
{
    [SerializeField] private XRSimpleInteractable interactable;

    private IXRSelectInteractor currentInteractor = null;

    public void SelectEnter()
    {
        currentInteractor = interactable.firstInteractorSelecting;
    }

    public void SelectExit()
    {
        currentInteractor = null;
        Debug.Log("Select exit");
    }

    private void Update()
    {
        if (currentInteractor != null)
        {
            Debug.Log(currentInteractor.GetAttachTransform(interactable).position);
        }
    }
}
