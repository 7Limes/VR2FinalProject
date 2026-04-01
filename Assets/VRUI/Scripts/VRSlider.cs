using UnityEngine;
using UnityEngine.InputSystem;

public class VRSlider : MonoBehaviour
{
    private InputAction action;

    private bool canDrag = false;

    public void OnSelectEnter()
    {
        Debug.Log("Entered");
        canDrag = true;
    }

    public void OnSelectExit()
    {
        Debug.Log("Exited");
        canDrag = false;
    }

    public void OnActivate(InputAction.CallbackContext ctx)
    {
        if (canDrag)
        {
            Debug.Log("activated");
        }
    }

    public void OnDeactivate(InputAction.CallbackContext ctx)
    {
        if (canDrag)
        {
            Debug.Log("deactivated");
        }
    }


    void Start()
    {
        action = InputSystem.actions.FindAction("ActivateVRSlider");
        action.performed += OnActivate;
        action.canceled += OnDeactivate;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
