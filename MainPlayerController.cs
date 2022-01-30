using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


namespace CharController{
 
    public class MainPlayerController : MonoBehaviour {
    // External hooks
    public Vector3 Vel { get; private set; }
    public FrameInput Input { get; private set; }
    public bool IsJumpingThisFrame { get; private set; }
    public bool IsLandingThisFrame { get; private set; }
    public Vector3 RawMovements { get; private set; }
    public bool IsGrounded => _colDown;
    
    private Vector3 _LastPosition;
    private float _CurrentHorizontalSpeed, _CurrentVerticalSpeed;
    
    
    private void Update(){
        //calculation nation the velocity (Vel)
        Vel = (transform.position - _LastPosition) / Time.deltaTime;
        _LastPosition = transform.position;
    
        // All the functions go here
        GrabInput();
        CollisionChecks();
    
        WalkingCalc();
        ApexJumpCalc();
        GravityCalc();
        JumpCalc();
    
        CharacterMove();
    }
    
    #region Grab Input
    
    private void GrabInput(){
        Input = new FrameInput{
            JumpDown = UnityEngine.Input.GetButtonDown("Jump"),
            JumpUp = UnityEngine.Input.GetButtonUp("Jump"),
            X = UnityEngine.Input.GetAxisRaw("Horizontal")
        };
        if(Input.JumpDown){
            _lastJumpPressed = Time.time;
        }
    }
    
    #endregion
    
    #region Collisions
    
    [Header("COLLISIONS")]
    [SerializeField] private Bounds _CharacterBounds;
    [SerializeField] private LayerMask _GroundLayer;
    [SerializeField] private int _DetectorCount = 3;
    [SerializeField] private float _DetectionRayLength = 0.1f;
    [SerializeField] [Range(0.1f, 0.3f)] private float _RayBuffer = 0.1f; //Prevents the side detectors to hit the ground;
    
    private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
    private bool _colUp, _colRight, _colDown, _colLeft;
    
    private float _timeLeftGrounded;
    
    
    // Raycast! in this case are used to check for pre-collision info!
    private void CollisionChecks(){
        //Generate the ray ranges in another function
        RayRangeCalc();
    
    
        //ground
        IsLandingThisFrame = false;
        var groundedCheck = RunDetection(_raysDown);
        if(_colDown && !groundedCheck){
            _timeLeftGrounded = Time.time;
        }else if(!_colDown && groundedCheck) {
            _coyoteUsable = false;
            IsLandingThisFrame = true;
        }
    
        _colDown = groundedCheck;
    
        //The rest
        _colUp = RunDetection(_raysUp);
        _colLeft = RunDetection(_raysLeft);
        _colRight = RunDetection(_raysRight);
    
        bool RunDetection(RayRange range){
            return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, _DetectionRayLength, _GroundLayer));
        }
    }
    
    private void RayRangeCalc(){
        var b =  new Bounds(transform.position, _CharacterBounds.size);
    
        _raysDown = new RayRange(b.min.x + _RayBuffer, b.min.y, b.max.x - _RayBuffer, b.min.y, Vector2.down);
        _raysRight = new RayRange(b.max.x, b.min.y + _RayBuffer, b.max.x, b.max.y - _RayBuffer, Vector2.right);
        _raysUp = new RayRange(b.min.x + _RayBuffer, b.max.y, b.max.x - _RayBuffer, b.max.y, Vector2.up);
        _raysLeft = new RayRange(b.min.x, b.min.y + _RayBuffer, b.min.x, b.max.y + _RayBuffer, Vector2.left);
    }
    
    private IEnumerable<Vector2> EvaluateRayPositions(RayRange range){
        for(var i = 0; i < _DetectorCount; i++){
            var t = (float)i / (_DetectorCount - 1);
            yield return Vector2.Lerp(range.Start, range.End, t);
        }
    }
    
    //Gizmos ! let you visualize the bounds and rays
    
    private void OnDrawGizmos(){
        // Bounds
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + _CharacterBounds.center, _CharacterBounds.size);
    
        // Rays
        if(!Application.isPlaying){
            RayRangeCalc();
            Gizmos.color = Color.blue;
            foreach(var range in new List<RayRange>{_raysUp, _raysRight, _raysDown, _raysLeft}){
                foreach(var point in EvaluateRayPositions(range)){
                    Gizmos.DrawRay(point, range.Dir * _DetectionRayLength);
                }
            }
        }
    
        if(!Application.isPlaying){
            return;
        }
        
        // Draw the future position
        Gizmos.color = Color.green;
        var move = new Vector3(_CurrentHorizontalSpeed, _CurrentVerticalSpeed) * Time.deltaTime;
        Gizmos.DrawWireCube(transform.position + move, _CharacterBounds.size);
    }

    #endregion

    #region Walking
    [Header("WALKING")]
    [SerializeField] private float _acceleration = 90;
    [SerializeField] private float _moveClamp = 13;
    [SerializeField] private float _deAcceleration = 60f;
    [SerializeField] private float _apexBounds = 2;

    private void WalkingCalc(){
        if(Input.X != 0){
            // set the horizontal move speed
            _CurrentHorizontalSpeed += Input.X * _acceleration * Time.deltaTime;

            //clamped by the max framerate
            _CurrentHorizontalSpeed = Mathf.Clamp(_CurrentHorizontalSpeed, -_moveClamp, _moveClamp);

            //apply bonus at the apex of a jump
            var apexBonus = Mathf.Sign(Input.X) * _apexBounds * _apexPoint;
            _CurrentHorizontalSpeed += apexBonus * Time.deltaTime;
        }else{
            _CurrentHorizontalSpeed = Mathf.MoveTowards(_CurrentHorizontalSpeed, 0, _deAcceleration * Time.deltaTime);
        }
        if(_CurrentHorizontalSpeed > 0 && _colRight || _CurrentHorizontalSpeed < 0 && _colLeft){
            //no walking through walls now 
            _CurrentHorizontalSpeed = 0;
        }
    }

    #endregion

    #region  Gravity

    [Header("GRAVITY")]
    [SerializeField] private float _fallClamp = -40f;
    [SerializeField] private float _minFallSpeed = 80f;
    [SerializeField] private float _maxFallSpeed = 120f;
    private float _fallSpeed;

    private void GravityCalc(){
        if(_colDown){
            //move out of the ground 
            if(_CurrentVerticalSpeed < 0){
                _CurrentVerticalSpeed = 0;
            }
        }else{
            var fallSpeed = _endedJumpEarly && _CurrentVerticalSpeed > 0 ? _fallSpeed * _jumpEndEarlyGravityModifier : _fallSpeed;

            _CurrentVerticalSpeed -= fallSpeed * Time.deltaTime;

            //clamp
            if(_CurrentVerticalSpeed < _fallClamp) _CurrentVerticalSpeed = _fallClamp;
        }
    }

    #endregion

    #region Jump

    [Header("JUMPING")]
    [SerializeField] private float _jumpHeight = 30f;
    [SerializeField] private float _jumpApexThreshold = 10f;
    [SerializeField] private float _coyoteTimeThreshold = 0.1f;
    [SerializeField] private float _jumpBuffer = 0.1f;
    [SerializeField] private float _jumpEndEarlyGravityModifier = 3;

    private bool _coyoteUsable;
    private bool _endedJumpEarly = true;
    private float _apexPoint;
    private float _lastJumpPressed;
    private bool CanUseCoyote => _coyoteUsable && !_colDown && _timeLeftGrounded + _coyoteTimeThreshold > Time.time;
    private bool HasBufferedJump => _colDown && _lastJumpPressed + _jumpBuffer > Time.time;

    private void ApexJumpCalc(){
        if(!_colDown){
            _apexPoint = Mathf.InverseLerp(_jumpApexThreshold, 0, Mathf.Abs(Vel.y));
            _fallSpeed = Mathf.Lerp(_minFallSpeed, _maxFallSpeed, _apexPoint);
        }else{
            _apexPoint = 0;
        }
    }

    private void JumpCalc(){
        if(Input.JumpDown && CanUseCoyote || HasBufferedJump){
            _CurrentVerticalSpeed = _jumpHeight;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            IsJumpingThisFrame = true;
        }else{
            IsJumpingThisFrame = false;
        }

        if(!_colDown && Input.JumpUp && !_endedJumpEarly && Vel.y > 0){
            _endedJumpEarly = true;
        }

        if(_colUp){
            if(_CurrentVerticalSpeed > 0){
                _CurrentVerticalSpeed = 0;
            }
        }
    }

    #endregion

    #region Move

        [Header("MOVE")]
        [SerializeField, Tooltip("Raising this value increases collision accuracy at the cost of performance.")]
        private int _freeColliderIterations = 10;


        private void CharacterMove(){
            var pos = transform.position;
            RawMovements = new Vector3(_CurrentHorizontalSpeed, _CurrentVerticalSpeed);
            var move = RawMovements * Time.deltaTime;
            var furthestPoint = pos + move;

            var hit = Physics2D.OverlapBox(furthestPoint, _CharacterBounds.size, 0, _GroundLayer);
            if(!hit){
                transform.position += move;
                return;
            }

            var positionToMoveTo = transform.position;
            for(int i = 1; i < _freeColliderIterations; i++){
                var t = (float)i / _freeColliderIterations;
                var posToTry = Vector2.Lerp(pos, furthestPoint, t);

                if(Physics2D.OverlapBox(posToTry, _CharacterBounds.size, 0, _GroundLayer)){
                    transform.position = positionToMoveTo;


                    if(i == 1){
                        if(_CurrentVerticalSpeed < 0){
                            _CurrentVerticalSpeed = 0;
                        }
                        var dir = transform.position - hit.transform.position;
                        transform.position += dir.normalized * -move.magnitude;
                    }

                    return;
                }

                positionToMoveTo = posToTry;
            }
        }
        #endregion
    }
}