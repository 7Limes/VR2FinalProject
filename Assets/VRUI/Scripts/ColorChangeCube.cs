using UnityEngine;

public class ColorChangeCube : MonoBehaviour
{
    [SerializeField] private VRColorPicker colorPicker;

    private Renderer cubeRenderer;
    
    public void UpdateColor()
    {
        if (colorPicker != null)
        {
            Color newColor = colorPicker.getColor();
            cubeRenderer.material.color = newColor;
        }
    }

    private void Start()
    {
        cubeRenderer = GetComponent<Renderer>();
        UpdateColor();
    }
}