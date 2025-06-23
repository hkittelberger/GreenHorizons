using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow2DLERP : MonoBehaviour {

    private Transform target;
    public float camSpeed = 4.0f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 50f;
    public float minZoom = 1f;
    public float maxZoom = 15f;

    private Camera cam;
    private InputAction zoomAction;

    void Awake() {
        cam = Camera.main;

        // Set up zoom scroll input action
        zoomAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll");
        zoomAction.Enable();
    }

    /* void Start() {
        target = GameObject.FindWithTag("Player");
    } */

    public void SetTarget(Transform newTarget) {
        target = newTarget;
    }

    void FixedUpdate () {
        if (target != null) {
            Vector2 pos = Vector2.Lerp((Vector2)transform.position, (Vector2)target.transform.position, camSpeed * Time.fixedDeltaTime);
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }
    }

    void Update() {
        HandleZoom();
    }

    void HandleZoom() {
        Vector2 scrollValue = zoomAction.ReadValue<Vector2>();
        float scroll = scrollValue.y;

        if (Mathf.Abs(scroll) > 0.01f) {
            float newSize = cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }
    }

    void OnDestroy() {
        zoomAction?.Disable();
    }
}
