using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float _walkSpeed;
    [SerializeField]
    private float _runSpeed;

    private Camera _playerCam;
    private Transform _fpsCamPoint;
    [SerializeField]
    private Vector3 _TPSOffSet;

    private CharacterController _char;
    private Vector3 _velocity;

    private GameObject _model;

    bool _moving;
    bool _grounded;
    bool _walking;
    bool _running;
    bool _jumping;
    bool _falling;

    bool _firstPerson = true;

    byte _stepCounter;
    const byte _stepResetValue = 15;

    // Start is called before the first frame update
    void Start()
    {
        CheckReferences();
    }

    void CheckReferences()
    {
        _playerCam = transform.Find("Player Camera").GetComponent<Camera>();
        _fpsCamPoint = transform.Find("FPS Camera Point");
        _model = transform.Find("Player Model").gameObject;
        _char = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCamera();
    }

    void UpdateCamera()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if(_firstPerson)
        {
            _playerCam.transform.parent = _fpsCamPoint;
            float yRot = _fpsCamPoint.rotation.eulerAngles.y;
            float xRot = _playerCam.transform.rotation.eulerAngles.x;

            yRot += Input.GetAxisRaw("Mouse X");
            xRot += -Input.GetAxisRaw("Mouse Y");

            _fpsCamPoint.localRotation = Quaternion.Euler(0, yRot, 0);
            _playerCam.transform.localRotation = Quaternion.Euler(xRot, 0, 0);

            _model.transform.localRotation = _fpsCamPoint.localRotation;

        }
        else
        {
            _playerCam.transform.parent = null;
        }
    }

    void FixedUpdate()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        Vector3 movement = Vector3.zero;

        float speed = _running ? _runSpeed : _walkSpeed;
        speed = input.magnitude > 0f ? speed : 0;

        if(_firstPerson)
        { movement += _fpsCamPoint.transform.forward + input.normalized; }
    
        

        _char.Move(movement * speed * Time.deltaTime);

        _grounded = (_velocity.y > -0.01f && _velocity.y < 0.01f);
        
        _char.Move(_velocity * Time.deltaTime);

        if(movement.magnitude > 0f && _grounded) { UpdateFootsteps(); }
    }

    void UpdateFootsteps()
    {
        _stepCounter++;

        if(_stepCounter < _stepResetValue) { return; }

        print("Footstep");

        _stepCounter = 0;
    }

}
