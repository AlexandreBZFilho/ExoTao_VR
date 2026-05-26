using UnityEngine;
using UnityEngine.UI; // Ensure correct namespace

public class ButtonTabActivator : MonoBehaviour
{
    public Menu_Navigation menuNavigation; // Reference to the Menu_Navigation script
    public int TabID; // The ID of the tab to activate

    // Start is called before the first frame update
    void Start()
    {
        // Get the Button component and add a listener to it
        Button button = GetComponent<Button>();
        if (button != null && menuNavigation != null)
        {
            button.onClick.AddListener(() => menuNavigation.SwitchToTab(TabID));
        }
    }
}
