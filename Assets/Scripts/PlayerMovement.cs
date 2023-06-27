using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


    public sealed class PlayerMovement : MonoBehaviour
    {
        public bool UseCustomMovementValues;
        [Header("Custom Movement Values")]
        public float Gravity = -9.8f;
        public float JumpHeight = 13;
        public float RunSpeed = 10;
        public float WalkSpeed = 6;
        public float CrouchSpeed = 4;
        public float StandingHeight = 1.8f;
        public float CrouchingHeight = 0.6f;
        private MouseLook _mouseLook;
        private Camera _playCam;
        private CharacterController _char;
        private Vector3 _curVelocity = Vector3.zero;
        const float GRAVITY = -25.8f;
        const float TERMINAL_SPD = -120f;
        const float JUMP_HEIGHT = 13;
        const float RUN_SPEED = 10;
        const float WALK_SPEED = 6;
        const float CROUCH_SPEED = 4;
        const float STANDING_HEIGHT = 2f;
        const float CROUCH_HEIGHT = 0.5f;
        const int _JUMP_GRACEFRAMES = 12;
        private int _graceCounter = 0;
        private bool _grounded = true;
        private bool _firstPerson = true;
        private bool _crouched = false;
        private bool _sprinting = false;
        private bool _initialized = false;
        private AudioClip _jumpClip;
        private Vector3 _lastPos;
        private bool _hasTriedMove;
        private int _stepCounter;
        private int _stepResetValue = 15;
        private bool shouldRefreshCullMask = false;

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            if(!_initialized) { return; }

            UpdateInput();
        }

        void Initialize()
        {
            _char = GetComponent<CharacterController>();

            var cam = transform.Find("PlayerCamera").gameObject;
            cam.transform.parent = null;

            _playCam = cam.GetComponent<Camera>();
            _mouseLook = cam.GetComponent<MouseLook>();

            if(_mouseLook.TrackedBody == null)
            {
                _mouseLook.TrackedBody = gameObject;
            }

            if(_jumpClip == null)
            {
                var jumpClip = Resources.Load("FPS_TPS_Controller/Sounds/jump");
                _jumpClip = jumpClip as AudioClip;
            }

            _mouseLook.LockCursor = true;
            _initialized = true;
        }

    	void UpdateInput()
        {
            if(IsMovementDesired(out Vector3 movement))
            {
                _char.Move(movement * Time.deltaTime);
                _hasTriedMove = true;
                _lastPos = transform.position;
            }
            else
            {
                _hasTriedMove = false;
            }

            _char.Move(_curVelocity * Time.deltaTime);

            if(Input.GetKeyDown(KeyCode.Tab))
            {
                _firstPerson = !_firstPerson;
                shouldRefreshCullMask = true;
            }

            if(Input.GetKeyDown(KeyCode.LeftControl))
            {
                _crouched = true;
            }
            // jumping also resets crouching; can't just check if control is held since that would override jump's crouch check
            else if(Input.GetKeyUp(KeyCode.LeftControl))
            {
                _crouched = false;
            }

            _sprinting = Input.GetKey(KeyCode.LeftShift);

            if(UseCustomMovementValues)
            {
                _char.height = _crouched ? CrouchingHeight : StandingHeight;
            }
            else _char.height = _crouched ? CROUCH_HEIGHT : STANDING_HEIGHT;

            _mouseLook.CameraUpdate(_firstPerson);
            UpdateJump();

            if(shouldRefreshCullMask)
            {
                // Set a cull mask to selectively cull by what is specified in MouseLook, or cull nothing = draw everything
                int layerMask = _firstPerson ? ~_mouseLook.DrawInFirstPerson.value : 1 << LayerMask.NameToLayer("Everything");
                _mouseLook.SetCameraCullMask(~layerMask);
            }
        }

        void SetChildLayersRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            int c = obj.transform.childCount;
            if(c == 0) { return; }
            for(int i = 0; i < c; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                child.layer = layer;
                SetChildLayersRecursive(child, layer);
            }
        }

        void UpdateJump()
        {
            if(Input.GetButtonDown("Jump") && _grounded)
            {
                TryJump();
            }

            if(_char.isGrounded)
            {
                _grounded = true;
            }
            else
            {
                _grounded = false;
            }

            if(!_grounded) { _graceCounter++; }
            else
            {
                _graceCounter = 0;
            }

            if(!_grounded)
            {
                _curVelocity.y += GRAVITY * Time.deltaTime;
            }
            
            if(_curVelocity.y < TERMINAL_SPD)
            {
                _curVelocity.y = TERMINAL_SPD;
            }
        }

        bool IsMovementDesired(out Vector3 movementVector)
        {
            float moveHorizontal = Input.GetAxisRaw("Horizontal");
            float moveVertical = Input.GetAxisRaw("Vertical");

            movementVector = Vector3.zero;

            Vector3 input = Quaternion.Euler (0, transform.eulerAngles.y, 0) * new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            bool pressingMoveKeys = (moveHorizontal > 0 || moveHorizontal < 0 || moveVertical > 0 || moveVertical < 0);
            bool moveDesired = pressingMoveKeys;

            if(moveDesired)
            {
                float speed = 0;
                if(UseCustomMovementValues)
                {
                    speed = _crouched ? CrouchSpeed : _sprinting ? RunSpeed : WalkSpeed;
                }
                else speed = _crouched ? CROUCH_SPEED : _sprinting ? RUN_SPEED : WALK_SPEED;

                movementVector = input.normalized * speed;
            }

            return moveDesired;
        }

        IEnumerator CameraShakeCoroutine(float amplitude, float duration)
        {
            for(float i = duration * 1.02f; i > 0; i -= 0.02f)
            {
                Vector3 _camShakePos = _playCam.transform.position + (Vector3)UnityEngine.Random.insideUnitCircle / (1 - amplitude);
                _playCam.transform.position = Vector3.Lerp(_playCam.transform.position, _camShakePos, i * 0.02f / (1 - amplitude));
                yield return new WaitForSeconds(0.02f);
            }
            //_cameraShaking = false;
            yield break;
        }
        
        void TryJump()
        {
            if((!_grounded)) { return; }

            _crouched = false;

            PlaySound(_jumpClip, 0.5f, transform.position);

            _grounded = false;
        
            _curVelocity.y = UseCustomMovementValues ? JumpHeight : JUMP_HEIGHT;
        }

        void PlaySound(AudioClip clip, float volume = 0.8f, Vector3? position = null, float pitch = 1)
        {
            if(clip == null) { return; }

            GameObject audio = new GameObject(clip.name);
            AudioSource source = audio.AddComponent<AudioSource>();
            if(position != null) 
            { audio.transform.position = (Vector3)position; source.spatialBlend = 1; }

            source.clip = clip;
            source.volume = volume;
            source.dopplerLevel = 0;
            source.Play();

            Destroy(audio, clip.length + 0.15f);
        }

        void UpdateFootsteps()
        {
            _stepCounter++;

            if(_stepCounter < _stepResetValue) { return; }

            // play footstep sound

            _stepCounter = 0;
        }
    }


