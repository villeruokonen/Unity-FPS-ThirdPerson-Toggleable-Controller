using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public sealed class PlayerMovement : MonoBehaviour
{
    public bool PlayFootSteps;
    public bool PlayJumpSound;
    public bool PlayLandingSound;
    public bool ViewBobbing;
    public float ViewBobbingMagnitude = 1;
    /*
    public bool UseCustomMovementValues;
    [Header("Custom Movement Values")]
    public float Gravity = -9.8f;
    public float JumpHeight = 13;
    //public float RunSpeed = 10;
    public float WalkSpeed = 6;
    public float CrouchSpeed = 4;
    public float AirMovementMultiplier = 0.4f;
    public float StandingHeight = 1.8f;
    public float CrouchingHeight = 0.6f;
    public float FrictionOnGround = 15;
    public float FrictionInAir = 0.6f;
    */

    // MouseLook should be attached to the player's camera object in the scene;
    // if it's not, one will be added
    private MouseLook _mouseLook;
    // Reference to the camera component attached to the gameobject containing MouseLook
    private Camera _playCam;
    private CharacterController _char;
    private enum MovementState
    {
        Grounded,
        Falling,
        Climbing
    }
    private MovementState _state;
    private const float GRAVITY = -25.8f;
    private const float TERMINAL_SPD = -120f;
    private const float JUMP_HEIGHT = 12;
    private const float WALK_SPEED = 10;
    private const float CROUCH_SPEED = 5;
    private const float GROUND_FRICTION = 15;
    private const float AIR_FRICTION = 0.6f;
    private const float AIR_MOVEMENT_FACTOR = 1f;
    private const float STANDING_HEIGHT = 1.8f;
    private const float CROUCH_HEIGHT = 0.4f;
    private const float STANDING_HEAD_OFFSET = 0.8f;
    private const float CROUCH_HEAD_OFFSET = 0.5f;
    private const float FALLING_LANDSOUND_THRESHOLD = -10f;
    private bool _grounded { get { return _char.isGrounded; } /* readonly */ }
    private bool _jumping;
    private bool _climbing;
    private bool _wasGrounded;
    private bool _firstPerson = true;
    private bool _crouched = false;
    private bool _initialized = false;
    private bool _pressingMoveKeys;
    private bool _hasAnimator;
    private AudioClip _jumpClip;
    private AudioClip _landingClip;
    private Dictionary<string, AudioClip> _footstepDictionary;
    // Vectors
    private Vector3 _lastPos;
    private Vector3 _curPos;
    private Vector3 _lastDesiredMovement;
    private Vector3 _lastPreStopVelocity;
    private Vector3 _curVelocity = Vector3.zero;
    private float _curSpeed;

    // cullmask is used to not render the player model when in first person
    private bool shouldRefreshCullMask = false;

    // animation IDs (Unity's Starter Assets / Third Person Controller asset)
    private Animator _animator;
    private float _animationBlend;
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;

    void Start()
    {
        Initialize();
    }

    void Update()
    {
        if (!_initialized) { return; }

        MainUpdate();
    }

    // Set references and load defaults
    void Initialize()
    {
        _char = GetComponent<CharacterController>();
        if (!_char)
        {
            _char = gameObject.AddComponent<CharacterController>();
        }

        var cam = transform.Find("PlayerCamera").gameObject;
        if (!cam)
        {
            cam = new GameObject("PlayerCamera");
            cam.transform.position = transform.position;
        }
        cam.transform.parent = null;

        _mouseLook = cam.GetComponent<MouseLook>();
        if (!_mouseLook)
        {
            _mouseLook = cam.AddComponent<MouseLook>();
            _mouseLook.SetDefaultValues();
        }

        _playCam = _mouseLook.Camera;

        if (_mouseLook.TrackedBody == null)
        {
            _mouseLook.TrackedBody = gameObject;
        }
        _mouseLook.LockCursor = true;

        var jumpClip = Resources.Load("FPS_TPS_Controller/Sounds/jump");
        _jumpClip = jumpClip as AudioClip;

        var landClip = Resources.Load("FPS_TPS_Controller/Sounds/land");
        _landingClip = landClip as AudioClip;

        // Initialize footstep dictionary with 1 default footstep sound;
        _footstepDictionary = new Dictionary<string, AudioClip>();
        var defaultFootstep = Resources.Load("FPS_TPS_Controller/Sounds/Footsteps/footstep_default") as AudioClip;
        _footstepDictionary.Add("default", defaultFootstep);

        _animator = GetComponentInChildren<Animator>();
        _hasAnimator = _animator != null;

        if (_hasAnimator) AssignAnimationIDs();

        _initialized = true;
    }

    // stolen from Unity's Starter Assets / Third Person Controller
    private void AssignAnimationIDs()
    {
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDFreeFall = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    void MainUpdate()
    {
        UpdateMovement();
        UpdateJumpAndGravity();
        UpdateViewBobbingAndFootsteps();
        _mouseLook.CameraUpdate(_firstPerson);
    }

    void UpdateMovement()
    {
        _lastPos = _char.transform.position;

        // Get input and normalize it to a length of 1
        _pressingMoveKeys = IsMovementDesired(out var directionNormalized);
        _lastDesiredMovement = directionNormalized * _curSpeed;

        Vector3 lerpedVelocity = LerpMovement();

        _curVelocity = new Vector3(lerpedVelocity.x, _curVelocity.y, lerpedVelocity.z);
        _curVelocity = ClampHorizontalVelocity(_curVelocity);

        _char.Move(_curVelocity * Time.deltaTime);

        // Toggle view mode and set camera cull mask update flag
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            _firstPerson = !_firstPerson;
            shouldRefreshCullMask = true;
        }

        // Pressing LeftControl initiates crouching
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            _crouched = true;

        }
        // Releasing LeftControl uncrouches if it's possible 
        // (if there's enough room above the player's head to accommodate their standing size)
        else if (Input.GetKeyUp(KeyCode.LeftControl) && CanUnCrouch())
        {
            _crouched = false;
        }

        // Check if can uncrouch but player does not wish to crouch any more
        if (_crouched && CanUnCrouch() && !Input.GetKey(KeyCode.LeftControl))
        {
            _crouched = false;
        }

        if (_crouched)
        { _mouseLook.HeadOffset = CROUCH_HEAD_OFFSET; }
        else
            _mouseLook.HeadOffset = STANDING_HEAD_OFFSET;

        _char.height = _crouched ? CROUCH_HEIGHT : STANDING_HEIGHT;

        if (shouldRefreshCullMask)
        {
            // Set a cull mask to selectively cull by what is specified in MouseLook, or cull nothing = draw everything
            int layerMask = _firstPerson ? ~_mouseLook.DrawInFirstPerson.value : 1 << LayerMask.NameToLayer("Everything");
            _mouseLook.SetCameraCullMask(~layerMask);
        }

        _curPos = _char.transform.position;

        // Lerp animation blending and set its floats
        if (_hasAnimator)
        {
            _animationBlend = Mathf.Lerp(_animationBlend, _curSpeed, Time.deltaTime * 2);
            if (_animationBlend < 0.01f) _animationBlend = 0f;
            _animator.SetFloat(_animIDSpeed, _lastPreStopVelocity.magnitude);
            _animator.SetFloat(_animIDMotionSpeed, directionNormalized.magnitude);
        }
    }

    Vector3 ClampHorizontalVelocity(Vector3 velocity)
    {
        Vector3 vel = velocity;

        vel.x = Mathf.Clamp(vel.x, -WALK_SPEED, WALK_SPEED);
        vel.z = Mathf.Clamp(vel.z, -WALK_SPEED, WALK_SPEED);

        return vel;
    }

    void SetChildLayersRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        int c = obj.transform.childCount;
        if (c == 0) { return; }
        for (int i = 0; i < c; i++)
        {
            var child = obj.transform.GetChild(i).gameObject;
            child.layer = layer;
            SetChildLayersRecursive(child, layer);
        }
    }

    // Messy math for smooth movement
    Vector3 LerpMovement()
    {
        if (_pressingMoveKeys)
        {
            if (!_grounded)
            {
                var airFactor = AIR_MOVEMENT_FACTOR;
                var airFriction = AIR_FRICTION;

                _lastPreStopVelocity = Vector3.Lerp(_lastPreStopVelocity,
                _lastDesiredMovement * airFactor * 2,
                Time.deltaTime * airFriction);

                return _lastPreStopVelocity;
            }

            var friction = GROUND_FRICTION;
            _lastPreStopVelocity = Vector3.Lerp(_lastPreStopVelocity, _lastDesiredMovement, Time.deltaTime * friction);

            if (Vector3.Distance(_lastPreStopVelocity, _lastDesiredMovement) < 2f)
            {
                _lastPreStopVelocity = _lastDesiredMovement;
            }

            return _lastPreStopVelocity;
        }

        if (!_grounded)
        {
            var friction = AIR_FRICTION;
            Vector3 surplusAirMovement = Vector3.Lerp(_lastPreStopVelocity, Vector3.zero, Time.deltaTime * friction);
            _lastPreStopVelocity = surplusAirMovement;

            if (Vector3.Distance(_lastPreStopVelocity, Vector3.zero) < 0.5f)
            {
                _lastPreStopVelocity = Vector3.zero;
            }

            return _lastPreStopVelocity;
        }
        else
        {
            var friction = GROUND_FRICTION;

            Vector3 surplusGroundMovement = Vector3.Lerp(_lastPreStopVelocity, Vector3.zero, Time.deltaTime * friction);
            _lastPreStopVelocity = surplusGroundMovement;

            if (Vector3.Distance(_lastPreStopVelocity, Vector3.zero) < 0.5f)
            {
                _lastPreStopVelocity = Vector3.zero;
            }

            return _lastPreStopVelocity;
        }
    }

    bool CanUnCrouch()
    {
        int mask = LayerMask.NameToLayer("Player");
        mask = 1 << mask;
        mask = ~mask;
        return !Physics.BoxCast(transform.position, new Vector3(_char.radius, STANDING_HEIGHT / 4, _char.radius),
        Vector3.up, Quaternion.identity, STANDING_HEIGHT, mask, QueryTriggerInteraction.Ignore);
    }

    bool HitsHead()
    {
        int mask = LayerMask.NameToLayer("Player");
        mask = 1 << mask;
        mask = ~mask;
        return Physics.BoxCast(transform.position, new Vector3(_char.radius, STANDING_HEIGHT / 8, _char.radius),
        Vector3.up, Quaternion.identity, STANDING_HEIGHT / 2, mask, QueryTriggerInteraction.Ignore);
    }

    void UpdateJumpAndGravity()
    {
        if (Input.GetButtonDown("Jump") && _grounded)
        {
            TryJump();
        }

        if (!_wasGrounded && _grounded)
        {
            _jumping = false;
            if (_char.velocity.y < 0)
            {
                _curVelocity = new Vector3(_curVelocity.x, _char.velocity.y, _curVelocity.z);
            }

            if (_curVelocity.y < FALLING_LANDSOUND_THRESHOLD && PlayLandingSound)
            {
                PlayMovementSound(_landingClip, 0.5f, transform.position, 1);
            }

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, true);
            }
        }

        var gravity = GRAVITY;

        if (_jumping && HitsHead())
        {
            _curVelocity.y = gravity * Time.deltaTime;  // head bonk => stop and fall instantly with a bit of downwards gravity
            _jumping = false;
        }

        if (!_grounded)
        {
            _curVelocity.y += gravity * Time.deltaTime; // add gravity when falling
        }

        if (_curVelocity.y < TERMINAL_SPD)
        {
            _curVelocity.y = TERMINAL_SPD;
        }

        if (_curVelocity.y < 0)
        {
            _jumping = false;
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, true);
            }
        }

        _state = _climbing ? MovementState.Climbing : _grounded ? MovementState.Grounded : MovementState.Falling;
        _wasGrounded = _grounded;

        if (_hasAnimator)
        {
            _animator.SetBool(_animIDFreeFall, !_grounded && !_jumping);
            _animator.SetBool(_animIDJump, _jumping);
        }
    }

    bool IsMovementDesired(out Vector3 directionNormalized)
    {
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        directionNormalized = Vector3.zero;

        // normalize input vector to player forward
        Vector3 input = Quaternion.Euler(0, transform.eulerAngles.y, 0) * new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        bool pressingMoveKeys = (moveHorizontal > 0 || moveHorizontal < 0 || moveVertical > 0 || moveVertical < 0);
        bool moveDesired = pressingMoveKeys;

        if (!moveDesired)
        {
            directionNormalized = Vector3.zero;
            _curSpeed = 0;
            return false;
        }

        float speed = 0;

        speed = _crouched ? CROUCH_SPEED : WALK_SPEED;

        _curSpeed = speed;

        directionNormalized = input.normalized;

        return true;
    }

    IEnumerator CameraShakeCoroutine(float amplitude, float duration)
    {
        for (float i = duration * 1.02f; i > 0; i -= 0.02f)
        {
            Vector3 _camShakePos = _playCam.transform.position + (Vector3)UnityEngine.Random.insideUnitCircle / (1 - amplitude);
            _playCam.transform.position = Vector3.Lerp(_playCam.transform.position, _camShakePos, i * 0.02f / (1 - amplitude));
            yield return new WaitForSeconds(0.02f);
        }
        yield break;
    }

    void TryJump()
    {
        if ((!_grounded)) { return; }

        // Return if there's no room to jump
        if (HitsHead()) { return; }
        _jumping = true;

        if (!_crouched)
        {
            _curVelocity.y = JUMP_HEIGHT;
        }
        else
        {
            // Jump a little lower if crouched
            _curVelocity.y = JUMP_HEIGHT * 0.8f;
        }

        if (_hasAnimator)
        {
            _animator.SetBool(_animIDJump, true);
        }

        // Return early if not playing jump sounds
        if (!PlayJumpSound) { return; }

        PlayMovementSound(_jumpClip, 0.5f, transform.position, 1.8f);
    }

    void PlayMovementSound(AudioClip clip, float volume = 0.8f, Vector3? position = null, float pitch = 1)
    {
        if (clip == null) { return; }

        GameObject audio = new GameObject(clip.name);
        AudioSource source = audio.AddComponent<AudioSource>();
        source.spatialBlend = 0.5f;
        source.clip = clip;
        source.volume = volume;
        source.Play();

        if (position != null)
        { audio.transform.position = (Vector3)position; source.spatialBlend = 1; }

        Destroy(audio, clip.length + 0.15f);
    }

    // Used solely for playing different footstep sounds depending on the texture
    // under the player's feet
    public int GetHitSubmesh(RaycastHit hitInfo)
    {
        var meshCollider = hitInfo.collider as MeshCollider;
        if (meshCollider == null)
            return 0;

        if (meshCollider.sharedMesh.subMeshCount > 1)
        {
            int submeshStartIndex = 0;
            for (int i = 0; i < meshCollider.sharedMesh.subMeshCount; i++)
            {
                int numSubmeshTris = meshCollider.sharedMesh.GetTriangles(i).Length / 3;
                if (hitInfo.triangleIndex < submeshStartIndex + numSubmeshTris)
                    return i;
                submeshStartIndex += numSubmeshTris;
            }
            return -1;
        }
        return 0;
    }

    // Get correct footstep sound based on what we're standing on
    AudioClip GetFootstepClip()
    {
        var defaultClip = _footstepDictionary["default"];

        var ray = new Ray(transform.position, Vector3.down);
        var mask = ~(1 << LayerMask.NameToLayer("Player"));

        // If there's nothing below us, don't return any sound
        // (This state should never be reached; footsteps are only played when grounded)
        if (!Physics.Raycast(ray, out var hitInfo, _char.height + 0.25f, mask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        var rend = hitInfo.transform.GetComponent<Renderer>();
        if (rend == null || rend.material.mainTexture == null) { return null; }

        var texName = rend.material.mainTexture.name;

        if (hitInfo.collider is MeshCollider)
        {
            int submesh = GetHitSubmesh(hitInfo);

            texName = rend.materials[submesh].mainTexture.name;
        }

        // Find correct sound for each material from the string,AudioClip dictionary.
        // If the pairing doesn't exist, try loading it into the dictionary for later use.
        // If the particular sound can't be loaded or it doesn't exist for that material, 
        // return the default footstep sound.

        if (texName.Contains("grass", StringComparison.OrdinalIgnoreCase))
        {
            if (_footstepDictionary.ContainsKey("grass")) return _footstepDictionary["grass"];
            else
            {
                var grassClip = Resources.Load("FPS_TPS_Controller/Sounds/Footsteps/footstep_grass") as AudioClip;
                if (grassClip == null) { return defaultClip; }
                _footstepDictionary.Add("grass", grassClip);
                return grassClip;
            }
        }
        else if (texName.Contains("concrete", StringComparison.OrdinalIgnoreCase))
        {
            if (_footstepDictionary.ContainsKey("concrete")) return _footstepDictionary["concrete"];
            else
            {
                var concreteClip = Resources.Load("FPS_TPS_Controller/Sounds/Footsteps/footstep_brick") as AudioClip;
                if (concreteClip == null) { return defaultClip; }
                _footstepDictionary.Add("concrete", concreteClip);
                return concreteClip;
            }
        }
        else if (texName.Contains("brick", StringComparison.OrdinalIgnoreCase))
        {
            if (_footstepDictionary.ContainsKey("brick")) return _footstepDictionary["brick"];
            else
            {
                var brickClip = Resources.Load("FPS_TPS_Controller/Sounds/Footsteps/footstep_brick") as AudioClip;
                if (brickClip == null) { return defaultClip; }
                _footstepDictionary.Add("brick", brickClip);
                return brickClip;
            }
        }
        else return defaultClip;
    }

    // What are you doing down here, little private fields?!
    private float _viewBobSine;
    private float _stepSine;
    private float _stepTime;
    private int _stepSign;
    private int _lastSign;
    void UpdateViewBobbingAndFootsteps()
    {
        // Don't waste time doing math if it's not used in either of these effects
        if (!ViewBobbing && !PlayFootSteps) { return; }

        // Don't bob the view or play footstep sounds if we're not moving enough 
        // (eg. running against or very acutely along a wall)
        bool actuallyMoving = Vector3.Distance(_curPos, _lastPos) > 0.002f;

        if (!actuallyMoving) { return; }
        _lastPos = _char.transform.position;

        if (ViewBobbing)
        {
            if (_pressingMoveKeys && _grounded)
            {
                _viewBobSine = Mathf.Lerp(_viewBobSine, (Mathf.Sin(Time.time * _curSpeed) * ViewBobbingMagnitude), Time.deltaTime);
            }
            else
            {
                _viewBobSine = Mathf.Lerp(_viewBobSine, 0, Time.deltaTime * 15);
            }
            _mouseLook.ViewBobValue = _viewBobSine;
        }

        if (PlayFootSteps)
        {
            // Don't bother continuing if we haven't been grounded for long enough, or not trying to move
            if (!_grounded || !_pressingMoveKeys) { return; }
            _stepTime += Time.deltaTime;

            _stepSine = Mathf.Lerp(_stepSine, (Mathf.Sin(_stepTime * _curSpeed) * ViewBobbingMagnitude), Time.deltaTime);
            // Check whether step sine is negative or positive and set sign accordingly (see further)
            _stepSign = _stepSine < 0 ? -1 : 1;

            // Hacky solution to footsteps:
            // If the sine has passed 0
            // (into negative if it was positive, into positive if it was negative),
            // only then play a footstep sound, and update the last sign of the step sine function
            if (_stepSign == _lastSign) { return; }
            _lastSign = _stepSign;

            float multiplier = _curSpeed / 3.5f;
            var randPitch = Random.Range(0.85f, 1f);
            var volume = Mathf.Clamp(Random.Range(0.8f, 1) * multiplier / 2, 0.2f, 1);
            PlayMovementSound(GetFootstepClip(), volume, transform.position, randPitch);
        }
    }
}


