using UnityEngine;
using UnityEngine.Events;

public class VRButtonSelectorPanel : MonoBehaviour
{
    [SerializeField] private VRButton[] buttons;

    [SerializeField] private Material pressedMaterial;
    [SerializeField] private Material unpressedMaterial;

    private int currentButtonIndex = 0;

    public int GetCurrentButtonIndex()
    {
        return currentButtonIndex;
    }

    public void UpdatePressPartMaterials() {
        buttons[currentButtonIndex].SetPressPartColor(pressedMaterial.color);
        for (int j = 0; j < buttons.Length; j++)
        {
            if (j != currentButtonIndex)
            {
                buttons[j].SetPressPartColor(unpressedMaterial.color);
            }
        }
    }

    void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].AddPriorityAction(() =>
            {
                currentButtonIndex = index;
                UpdatePressPartMaterials();
            });
        }

        UpdatePressPartMaterials();
    }
}
