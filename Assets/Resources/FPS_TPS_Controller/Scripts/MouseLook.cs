using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("General Settings")]
    public LayerMask DrawInFirstPerson;
    public float Sensitivity = 5.0f;
    [Header("FPS Camera Settings")]
    public bool LockCursor;
    //public float FPSCamDamping = 100;
    Vector2 targetDirection;
    Vector2 targetCharacterDirection;
    public float HeadOffset;

    [Header("TPS Camera Settings")]
    public GameObject TrackedBody;
    public Vector3 DesiredCameraPositionOffset = new Vector3(0.0f, 2.0f, -2.5f);
    public Vector3 CameraAngleOffset = new Vector3(0.0f, 0.0f, 0.0f);
    public float CameraFollowSpeed = 15;
    public float MinPitch = -30.0f;
    public float MaxPitch = 30.0f;
    public Camera Camera => _playCam;
    private float angleX = 0.0f;
    Transform mPlayer;
    private Camera _playCam;
	private float _originalFov;
    private Quaternion _curRot;
    [HideInInspector] public bool AllowRotation = true;
    [HideInInspector] public float ViewBobValue = 0;

    void Start()
    {
        _playCam = gameObject.GetComponent<Camera>();
        if(!_playCam)
        {
            _playCam = gameObject.AddComponent<Camera>();
        }

		_originalFov = _playCam.fieldOfView;

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;

        // Set target direction for the character body to its inital state.
        if (TrackedBody)
            targetCharacterDirection = TrackedBody.transform.localRotation.eulerAngles;
    }

    public void CameraUpdate(bool firstPerson)
    {
        if(!TrackedBody)
        { 
            Debug.LogWarning($"MouseLook {this} has no Tracked Body and will not run.");
            return;
        }

        if (LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        _playCam.transform.localRotation = _curRot;

        float damping = firstPerson ? 15 : CameraFollowSpeed;

        mPlayer = TrackedBody.transform;

        // Mouse X and Y input
        float mx, my;
        mx = Input.GetAxisRaw("Mouse X");
        my = Input.GetAxisRaw("Mouse Y");

        // Apply the initial rotation to the camera.
        Quaternion initialRotation = Quaternion.Euler(CameraAngleOffset);

        Vector3 camEuler = _playCam.transform.rotation.eulerAngles;

        angleX -= my * Sensitivity;

        // Clamp pitch between tps min and max pitch if third person, else straight up/down
        if (!firstPerson)
            angleX = Mathf.Clamp(angleX, MinPitch, MaxPitch);
        else
            angleX = Mathf.Clamp(angleX, -90, 90);

        camEuler.y += mx * Sensitivity;
        Quaternion newRot = Quaternion.Euler(angleX, camEuler.y, 0.0f) *
          initialRotation;

        Vector3 forward = _playCam.transform.rotation * Vector3.forward;
        Vector3 right = _playCam.transform.rotation * Vector3.right;
        Vector3 up = _playCam.transform.rotation * Vector3.up;

        Vector3 targetPos = TrackedBody.transform.position;

        Vector3 desiredPosition = firstPerson
        ?
            targetPos
                // + (forward)
                + (right * ViewBobValue / 3)
                + (Vector3.up * (HeadOffset + Mathf.Abs(ViewBobValue) / 1.3f))
        :
            targetPos
                + (forward * DesiredCameraPositionOffset.z)
                + (right * DesiredCameraPositionOffset.x)
                + (up * DesiredCameraPositionOffset.y);

        Vector3 position;
        if (firstPerson)
        {
            position = desiredPosition;
        }
        else
        {
            desiredPosition = ValidateCameraPosition(desiredPosition);

            position = Vector3.Lerp(_playCam.transform.position,
            desiredPosition,
            Time.deltaTime * damping);
        }

        _playCam.transform.rotation = newRot;
        _playCam.transform.position = position;
        _curRot = newRot;

        Quaternion newCharacterRot = Quaternion.Euler(TrackedBody.transform.rotation.x, camEuler.y, 0.0f) * initialRotation;

        if(!firstPerson)
        {
            TrackedBody.transform.rotation = Quaternion.Lerp
            (TrackedBody.transform.rotation, newCharacterRot, Time.deltaTime * (damping / 3));
        }
        else
        {
            TrackedBody.transform.rotation = newCharacterRot;
        }

        
    }

    private Vector3 ValidateCameraPosition(Vector3 desiredPosition)
    {
        if(CameraFreeAndViewNotObstructedAtPosition(desiredPosition, out var hitInfo)) { return desiredPosition; }
        Vector3 newPos = hitInfo.point + hitInfo.normal * 0.05f;
        return newPos;
    }

    bool CameraFreeAndViewNotObstructedAtPosition(Vector3 desiredPosition, out RaycastHit hitInfo)
    {
        var pos = TrackedBody.transform.position;
        var direction = desiredPosition - pos;
        float distance = DesiredCameraPositionOffset.magnitude + 0.25f;
        int mask = ~(1 << LayerMask.NameToLayer("Player"));
        float radius = 0.2f;
        hitInfo = new RaycastHit();

        var cast = Physics.SphereCast(pos, radius, direction, out hitInfo, distance, mask, QueryTriggerInteraction.Ignore);
        var cols = Physics.OverlapSphere(pos, radius, mask, QueryTriggerInteraction.Ignore);

        return cols.Length == 0 && !cast;
    }

    public void SetCameraCullMask(int mask)
    {
        _playCam.cullingMask = mask;
    }

    public void SetDefaultValues()
    {
        Sensitivity = 1;
        DrawInFirstPerson = LayerMask.NameToLayer("Everything");
        HeadOffset = 0.8f;
        DesiredCameraPositionOffset = new Vector3(0.8f, 1.2f, -4);
        CameraAngleOffset = Vector3.zero;
        MinPitch = -30;
        MaxPitch = 45;
    }
}
