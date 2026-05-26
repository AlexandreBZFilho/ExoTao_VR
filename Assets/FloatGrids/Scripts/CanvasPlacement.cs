using System.Collections;
using UnityEngine;

public class CanvasPlacement : MonoBehaviour
{
    [SerializeField]
    private float _angle = 4f; // Height angle at 1 meter distance
    public float Angle { get { return _angle; } set { _angle = value; } }

    [SerializeField]
    private float _distance = 2f; // Distance in centimeters
    public float Distance { get { return _distance; } set { _distance = value; } }

    [SerializeField]
    private float _scale = 2f; // Scale factor to adjust the scale from the script
    public float Scale { get { return _scale; } set { _scale = value; } }

    private RectTransform rectTransform;

    void Start()
    {
        StartCoroutine(CheckForCamera());
    }

    IEnumerator CheckForCamera()
    {
        while (Camera.main == null)
        {
            Debug.LogWarning("Main camera not found. Retrying...");
            yield return new WaitForSeconds(0.5f); // Add a delay to prevent a tight loop
        }

        PlaceCanvas();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(CheckForCamera());
        }
        else
        {
            PlaceCanvas();
        }
    }

    void PlaceCanvas()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            // Calculate height angle based on distance and angle at 1 meter
            float heightAngle = Angle;

            // Calculate position offset based on height angle and distance
            float heightRadians = heightAngle * Mathf.Deg2Rad;
            float yOffset = Mathf.Tan(heightRadians) * Distance;

            Vector3 canvasPosition = mainCamera.transform.position + mainCamera.transform.forward * Distance;
            canvasPosition.y -= yOffset; // Invert the sign to go down

            // Set canvas position
            transform.position = canvasPosition;
            transform.rotation = mainCamera.transform.rotation;

            // Get the RectTransform component
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            // Modify the scale of the RectTransform
            if (rectTransform != null)
            {
                rectTransform.localScale = new Vector3(Scale, Scale, Scale);
            }
        }
        else
        {
            Debug.LogError("Main camera not found!");
        }
    }
}

