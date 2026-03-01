using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class OrbitViewController : MonoBehaviour
{
    [Header("Movement")]
    [FormerlySerializedAs("translationRate")]
    public float moveSpeed = 10f;
    [FormerlySerializedAs("sprintFactor")]
    public float boostMultiplier = 3f;
    [FormerlySerializedAs("crawlFactor")]
    public float slowMultiplier = 0.25f;

    [Header("Look")]
    [FormerlySerializedAs("rotationGain")]
    public float lookSensitivity = 2f;
    [FormerlySerializedAs("dampingFactor")]
    public float smoothFactor = 5f;

    private float m_YawAngle;
    private float m_PitchAngle;
    private bool m_IsLooking;

    private void OnEnable()
    {
        var euler = transform.eulerAngles;
        m_YawAngle = euler.y;
        m_PitchAngle = euler.x;
    }

    private void Update()
    {
        // Right mouse button to look
        if (Input.GetMouseButtonDown(1))
        {
            m_IsLooking = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Input.GetMouseButtonUp(1))
        {
            m_IsLooking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (m_IsLooking)
        {
            m_YawAngle += Input.GetAxis("Mouse X") * lookSensitivity;
            m_PitchAngle -= Input.GetAxis("Mouse Y") * lookSensitivity;
            m_PitchAngle = Mathf.Clamp(m_PitchAngle, -90f, 90f);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(m_PitchAngle, m_YawAngle, 0f),
            Time.deltaTime * smoothFactor * 10f);

        // Speed modifier
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= boostMultiplier;
        if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMultiplier;

        // Scroll wheel adjusts base speed
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            moveSpeed = Mathf.Clamp(moveSpeed + scroll * moveSpeed, 0.1f, 200f);

        // WASD + QE movement
        var move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move += Vector3.back;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move += Vector3.down;

        transform.Translate(move.normalized * speed * Time.deltaTime, Space.Self);
    }
}
