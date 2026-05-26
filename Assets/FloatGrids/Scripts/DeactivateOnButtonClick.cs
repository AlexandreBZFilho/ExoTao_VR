using UnityEngine;

public class DeactivateOnButtonClick : MonoBehaviour
{
    [SerializeField]
    private GameObject targetGameObject;

    public void Deactivate()
    {
        if (targetGameObject != null)
        {
            targetGameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Target GameObject is not assigned.");
        }
    }
}

