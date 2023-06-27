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
	public Vector3 CameraPositionOffset = new Vector3(0.0f, 2.0f, -2.5f);
    public Vector3 CameraAngleOffset = new Vector3(0.0f, 0.0f, 0.0f);
	public float CameraFollowSpeed = 15;
    public float MinPitch = -30.0f;
    public float MaxPitch = 30.0f;
    private float angleX = 0.0f;
    Transform mPlayer;
	private Camera _playCam;
	private Quaternion _curRot;
	[HideInInspector] public bool AllowRotation = true;

	void Start()
	{
		_playCam = gameObject.GetComponent<Camera>();

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;

		// Set target direction for the character body to its inital state.
		if (TrackedBody)
			targetCharacterDirection = TrackedBody.transform.localRotation.eulerAngles;
	}

	public void CameraUpdate(bool firstPerson)
    {
		if(_playCam == null) { return; }

		if (LockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
		}
		else
		{
			Cursor.lockState = CursorLockMode.None;
		}

		_playCam.transform.localRotation = _curRot;

		float damping = firstPerson ? 1000 : CameraFollowSpeed;

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
        if(!firstPerson)
			angleX = Mathf.Clamp(angleX, MinPitch, MaxPitch);
		else
			angleX = Mathf.Clamp(angleX, -90, 90);

        camEuler.y += mx * Sensitivity;
        Quaternion newRot = Quaternion.Euler(angleX, camEuler.y, 0.0f) *
          initialRotation;

        Vector3 forward =   _playCam.transform.rotation * Vector3.forward;
        Vector3 right =     _playCam.transform.rotation * Vector3.right;
        Vector3 up =        _playCam.transform.rotation * Vector3.up;

        Vector3 targetPos = TrackedBody.transform.position;

        Vector3 desiredPosition = firstPerson
		?
			targetPos
				// + (forward)
        	    // + (right)
        	    + (Vector3.up * HeadOffset)
		:
			targetPos
        	    + (forward * CameraPositionOffset.z)
        	    + (right * CameraPositionOffset.x)
        	    + (up * CameraPositionOffset.y);

        Vector3 position = Vector3.Lerp(_playCam.transform.position,
            desiredPosition,
            Time.deltaTime * damping);

		_playCam.transform.rotation = newRot;

		Quaternion newCharacterRot = Quaternion.Euler(TrackedBody.transform.rotation.x, camEuler.y, 0.0f) * initialRotation;
		TrackedBody.transform.rotation = Quaternion.Lerp
		(TrackedBody.transform.rotation, newCharacterRot, Time.deltaTime * (damping / 5));

        _playCam.transform.position = position;
		_curRot = newRot;
    }

	public void SetCameraCullMask(int mask)
	{
		_playCam.cullingMask = mask;
	}
}
