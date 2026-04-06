using UnityEngine;
using UnityEngine.Events;

public class VRColorPicker : MonoBehaviour
{
    [SerializeField] private VRSlider hueSlider;
    [SerializeField] private VRSlider saturationSlider;
    [SerializeField] private VRSlider valueSlider;

    [SerializeField] private UnityEvent changeEvent = null;

    private Color currentColor = Color.white;

    public void UpdateColor()
    {
        float hue = hueSlider.getValue();
        float saturation = saturationSlider.getValue();
        float value = valueSlider.getValue();

        currentColor = Color.HSVToRGB(hue, saturation, value);

        if (changeEvent != null)
        {
            changeEvent.Invoke();
        }
    }

    public Color getColor()
    {
        return currentColor;
    }

    private void Start()
    {
        UpdateColor();
    }
}
