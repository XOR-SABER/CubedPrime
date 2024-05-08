using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public Joystick moveJoystick;
    public Joystick aimJoystick;

    public enum InputType{
        Mobile,
        Keyboard,
        Controller
    }

    public enum DashType
    {
        InMovementDirection,
        InAimDirection
    }

    public InputType inputType;
    public DashType dashType;
    
    [Header("Stats of the player")]
    public float moveSpeed;

    public float baseDashSpeed;
    
    [Range(0.1f, 2)][Tooltip("Dash duration in seconds")]
    public float dashDuration;
    private float _dashSpeed;
    
    [Tooltip("More deceleration = will lose speed faster as you release the controls")][Range(1, 20)]
    public float deceleration;

    [Range(0.2f, 0.6f)]
    public float joystickDeadZone;

    [Header("Dashing mech")]
    public bool isDashing;
    public float slowFactor = 0.5f;
    public float duration = 0.25f; 
    public float recoveryTime = 0.1f;
    private bool _isTimeStopped = false;
    private PlayersControls _playersControls;
    private Rigidbody2D _rb;
    private Vector2 _movement;
    private Vector2 _direction;
    private Vector2 currentVelocity;
    private Vector2 overAllMovement;
    private InputAction _move;
    private InputAction _look;
    private InputAction _dash;
    private Vector2 lastDir;
    private Vector2 overAllDirection;
    private Camera _camera;
    private Vector2 dashDir;


    private void Awake()
    {
        if (aimJoystick is null || moveJoystick is null)
        {
            throw new NullReferenceException("Before using this script, please link the two joystick found in the canvas to their corresponding fields in the player movement script in the inspector");
        }
        _dashSpeed = baseDashSpeed;
        _playersControls = new PlayersControls();
        _rb = GetComponent<Rigidbody2D>();
        _move = _playersControls.Player.Move;
        _move.Enable();
        _look = _playersControls.Player.Look;
        _look.Enable();
        _dash = _playersControls.Player.Dash;
        _dash.Enable();
        _dash.performed += _ => Dash();
    }

    private void Start()
    {
        _camera = Camera.main;
    }

    private void OnDisable()
    {
        _move.Disable();
        _look.Disable();
        _dash.Disable();
    }

    private void Update()
    {
        if (aimJoystick is null || moveJoystick is null)
        {
            throw new NullReferenceException("Before using this script, please link the two joystick found in the canvas to their corresponding fields in the player movement script in the inspector");
        }
        switch (inputType)
        {
            case InputType.Mobile:
                moveJoystick.enabled = true;
                aimJoystick.enabled = true;
                overAllMovement = moveJoystick.Direction;
                overAllDirection = aimJoystick.Direction;
                break;
            case InputType.Keyboard:
                moveJoystick.enabled = false;
                aimJoystick.enabled = false;
                var dir = Input.mousePosition - _camera.WorldToScreenPoint(transform.position);
                overAllMovement = _move.ReadValue<Vector2>();
                overAllDirection = dir;
                break;
            case InputType.Controller:
                moveJoystick.enabled = false;
                aimJoystick.enabled = false;
                overAllMovement = _move.ReadValue<Vector2>();
                overAllDirection = _look.ReadValue<Vector2>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (isDashing)
        {
            overAllMovement = dashDir;
            _dashSpeed -= (baseDashSpeed - moveSpeed) * Time.deltaTime / dashDuration;
            if (_dashSpeed <= moveSpeed)
            {
                isDashing = false;
                _dashSpeed = baseDashSpeed;
            }
        }

        if (inputType is not InputType.Keyboard)
        {
            if (overAllDirection.magnitude >= joystickDeadZone)
            {
                lastDir = overAllDirection;
            }
            _movement = overAllMovement;
            _direction = lastDir;
        }
        else
        {
            lastDir = overAllDirection;
            _movement = overAllMovement;
            _direction = overAllDirection;
        }
    }

    private void FixedUpdate()
    {
        Vector2 targetVelocity;
        if (isDashing)
        {
            targetVelocity = _movement * _dashSpeed;
        }
        else
        {
            targetVelocity = _movement * moveSpeed;
        }
        if (_movement.magnitude == 0f)
        {
            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * 10f);
        }

        _rb.MovePosition(_rb.position + currentVelocity * Time.fixedDeltaTime);

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90;
        _rb.rotation = angle;
    }
    
    private void Dash()
    {
        if (isDashing)
        {
            return;
        }
        isDashing = true;
        dashDir = dashType switch
        {
            DashType.InAimDirection => lastDir.normalized,
            DashType.InMovementDirection => overAllMovement.normalized,
            _ => dashDir
        };
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Enemy") && !isDashing)
        {
            PlayerStats.instance.TakeDamage(1);
        } else if (other.gameObject.CompareTag("Enemy") && isDashing) {
            Enemy temp = other.GetComponent<Enemy>();
            if(temp != null) {
                temp.TakeDamage(100);
                if(!_isTimeStopped) StartCoroutine(SlowMotionRoutine(slowFactor, duration, recoveryTime));
            }
        }
    }
    // This only exists for the bouncy enemy!
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Enemy")) handleDashing(collision);
    }

    void handleDashing(Collision2D collision){
        if(isDashing) {
            Enemy temp = collision.gameObject.GetComponent<Enemy>();
            if(temp != null) {
                temp.TakeDamage(100);
                PlayerStats.instance.heal(1);
                if(!_isTimeStopped) StartCoroutine(SlowMotionRoutine(slowFactor, duration, recoveryTime));
            }
        } else {
            PlayerStats.instance.TakeDamage(1);
        }
    }

    private IEnumerator SlowMotionRoutine(float slowFactor, float duration, float recoveryTime)
    {
        AudioManager.instance.PlayOnShot("DashSound");
        _isTimeStopped = true;
        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;  
        yield return new WaitForSecondsRealtime(duration);

        float currentTime = 0f;
        while (currentTime < recoveryTime)
        {
            currentTime += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(slowFactor, 1.0f, currentTime / recoveryTime);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return null;
        }

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        _isTimeStopped = false;
    }
    public void onDeath() {
        Destroy(gameObject);
    }
}
