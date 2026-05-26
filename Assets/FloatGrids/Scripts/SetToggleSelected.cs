using UnityEngine;
using UnityEngine.UI;

public class SetToggleSelected : MonoBehaviour
{
    public Toggle myToggle; // Reference to the Toggle component

    void Start()
    {
        if (myToggle != null)
        {
            myToggle.isOn = true; // Set the toggle to be selected
        }
    }
}

