using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomizer : MonoBehaviour
{
    [SerializeField] private VRButtonSelectorPanel materialSelectorPanel;
    [SerializeField] private VRColorPicker colorPicker;

    [SerializeField] private List<Material> materials;

    VRSlider hueSlider, saturationSlider, valueSlider;


    public void UpdateMaterial()
    {
        Color color = colorPicker.getColor();
        int materialIndex = materialSelectorPanel.GetCurrentButtonIndex();
        materials[materialIndex].color = color;
        UpdateThumbColors(color);
    }

    public void UpdateSelectedMaterial()
    {
        int index = materialSelectorPanel.GetCurrentButtonIndex();
        Debug.Log(index);
        Color color = materials[index].color;
        float h, s, v;
        Color.RGBToHSV(color, out h, out s, out v);
        hueSlider.SetValue(h);
        saturationSlider.SetValue(s);
        valueSlider.SetValue(v);
        UpdateThumbColors(color);
    }

    private void UpdateThumbColors(Color color)
    {
        hueSlider.SetThumbColor(color);
        saturationSlider.SetThumbColor(color);
        valueSlider.SetThumbColor(color);
    }

    private void Start()
    {
        hueSlider = colorPicker.getHueSlider();
        saturationSlider = colorPicker.getSaturationSlider();
        valueSlider = colorPicker.getValueSlider();
    }
}
