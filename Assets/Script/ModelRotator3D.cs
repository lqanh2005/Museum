using UnityEngine;
using UnityEngine.EventSystems;

public class ModelRotator3D : MonoBehaviour
{
    [Header("Rotate Speed")]
    [SerializeField] private float rotateSpeedX = 5f;
    [SerializeField] private float rotateSpeedY = 5f;

    [Header("Auto Rotate")]
    [SerializeField] private bool autoRotate = false;
    [SerializeField] private float autoRotateSpeed = 15f;

    [Header("Clamp Pitch")]
    [SerializeField] private bool clampPitch = true;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Input")]
    [SerializeField] private bool blockRotateWhenPointerOverUI = true;

    private float yaw;
    private float pitch;
    private bool isDragging;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);
    }

    private void Update()
    {
        HandleInput();
        HandleAutoRotate();
        ApplyRotation();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (blockRotateWhenPointerOverUI &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (!isDragging) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * rotateSpeedX;
        pitch -= mouseY * rotateSpeedY;

        if (clampPitch)
        {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private void HandleAutoRotate()
    {
        if (isDragging) return;
        if (!autoRotate) return;

        yaw += autoRotateSpeed * Time.deltaTime;
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}