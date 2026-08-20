using UnityEngine;

// Свободная камера: ПКМ (зажать) — обзор, WASD — полёт,
// Space/C — вверх/вниз, Shift — ускорение, колесо — регулировка скорости полёта.
public class FlyCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float shiftBoost = 4f;
    public float lookSensitivity = 320f;   // была 180 — крутится быстрее

    float speedScale = 1f;
    float yaw, pitch;

    void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x > 180f ? transform.eulerAngles.x - 360f
                                               : transform.eulerAngles.x;
    }

    void Update()
    {
        // Обзор — только пока зажат ПКМ.
        if (Input.GetMouseButton(1))
        {
            yaw   += Input.GetAxis("Mouse X") * lookSensitivity * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Колесо без Alt — скорость полёта (с Alt колесо перехватывает палитра).
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 1e-4f && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            speedScale = Mathf.Clamp(speedScale * (1f + wheel * 2f), 0.05f, 40f);

        Vector3 dir = transform.right * Input.GetAxisRaw("Horizontal")
                    + transform.forward * Input.GetAxisRaw("Vertical");
        if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) dir += Vector3.down;

        if (dir.sqrMagnitude > 0.001f)
        {
            float s = moveSpeed * speedScale * (Input.GetKey(KeyCode.LeftShift) ? shiftBoost : 1f);
            transform.position += dir.normalized * s * Time.deltaTime;
        }
    }
}