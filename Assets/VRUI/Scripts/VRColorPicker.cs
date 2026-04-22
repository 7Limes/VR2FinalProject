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
        float hue = hueSlider.GetValue();
        float saturation = saturationSlider.GetValue();
        float value = valueSlider.GetValue();

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

    public VRSlider getHueSlider()
    {
        return hueSlider;
    }

    public VRSlider getSaturationSlider()
    {
        return saturationSlider;
    }

    public VRSlider getValueSlider()
    {
        return valueSlider;
    }

    private void Start()
    {
        UpdateColor();
    }
}
