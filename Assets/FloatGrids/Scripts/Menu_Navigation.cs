using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Ensure correct namespace

public class Menu_Navigation : MonoBehaviour
{
    public GameObject[] Views; // Ensure this is public to be visible in the Inspector
    public int DefaultTabID = 0; // Add a variable for the default tab ID

    // Method to switch between tabs
    public void SwitchToTab(int TabID)
    {
        // Deactivate all views
        foreach (GameObject go in Views)
        {
            go.SetActive(false);
        }

        // Activate the selected view if the TabID is valid
        if (TabID >= 0 && TabID < Views.Length) 
        {
            Views[TabID].SetActive(true);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // Set the default selected tab
        SwitchToTab(DefaultTabID);
    }

    // Update is called once per frame
    void Update()
    {
        // Optional: Add update logic if needed
    }
}