using UnityEngine;

public class ClosePopup : MonoBehaviour
{
    // Reference to the pop-up GameObject
    public GameObject popup;

    // Method to close the pop-up
    public void Close()
    {
        // Set the pop-up GameObject to inactive
        if (popup != null)
        {
            popup.SetActive(false);
        }
    }
}
