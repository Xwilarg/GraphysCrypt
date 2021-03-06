using GamesPlusJam.Action;
using GamesPlusJam.SO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GamesPlusJam
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField]
        private Sprite _crossOn, _crossOff;

        [SerializeField]
        private PlayerInfo _info;

        [SerializeField]
        private Transform _head;
        private float _headRotation;

        [SerializeField]
        private Image _crosshair;

        private List<AudioClip> _footstepsWalk, _footstepsRun;

        private AudioSource _audioSource;
        private CharacterController _controller;
        private bool _isSprinting;
        private float _verticalSpeed;
        private float _footstepDelay;

        private Vector2 _mov;
        private int _ignorePlayerLayer;

        private Interactible _interactible;

        private bool _canMove = true;

        public void Victory()
        {
            _canMove = false;
        }

        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            _controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;

            _footstepsWalk = _info.FootstepsWalk.ToList();
            _footstepsRun = _info.FootstepsRun.ToList();

            _ignorePlayerLayer = ~(1 << LayerMask.NameToLayer("Player"));
        }

        private void FixedUpdate()
        {
            if (!_canMove)
            {
                return;
            }

            var pos = _mov;
            Vector3 desiredMove = transform.forward * pos.y + transform.right * pos.x;

            // Get a normal for the surface that is being touched to move along it
            Physics.SphereCast(transform.position, _controller.radius, Vector3.down, out RaycastHit hitInfo,
                               _controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

            Vector3 moveDir = Vector3.zero;
            moveDir.x = desiredMove.x * _info.ForceMultiplier * (_isSprinting ? _info.SpeedRunningMultiplicator : 1f);
            moveDir.z = desiredMove.z * _info.ForceMultiplier * (_isSprinting ? _info.SpeedRunningMultiplicator : 1f);

            if (_controller.isGrounded && _verticalSpeed < 0f) // We are on the ground and not jumping
            {
                moveDir.y = -.1f; // Stick to the ground
                _verticalSpeed = -_info.GravityMultiplicator;
            }
            else
            {
                // We are currently jumping, reduce our jump velocity by gravity and apply it
                _verticalSpeed += Physics.gravity.y * _info.GravityMultiplicator;
                moveDir.y += _verticalSpeed;
            }

            var p = transform.position;
            _controller.Move(moveDir);

            // Footsteps
            if (_controller.isGrounded)
            {
                _footstepDelay -= Vector3.SqrMagnitude(p - transform.position);
                if (_footstepDelay < 0f)
                {
                    var target = _isSprinting ? _footstepsRun : _footstepsWalk;
                    var clipIndex = Random.Range(1, target.Count);
                    var clip = target[clipIndex];
                    target.RemoveAt(clipIndex);
                    target.Insert(0, clip);

                    _audioSource.PlayOneShot(clip);
                    _footstepDelay += _info.FootstepDelay * (_isSprinting ? _info.FootstepDelayRunMultiplier : 1f);
                }
            }

            // Detect if can interract with smth in front of us
            _crosshair.sprite = _crossOff;
            if (Physics.Raycast(new Ray(_head.position, _head.forward), out RaycastHit interInfo, 2f, _ignorePlayerLayer))
            {
                _interactible = interInfo.collider.GetComponent<Interactible>();
                bool canInteract = _interactible != null && _interactible.IsAvailable;
                if (canInteract)
                {
                    _crosshair.sprite = _crossOn;
                }
            }
            else
            {
                _interactible = null;
            }
        }

        public void OnMovement(InputAction.CallbackContext value)
        {
            _mov = value.ReadValue<Vector2>().normalized;
        }

        public void OnLook(InputAction.CallbackContext value)
        {
            if (!_canMove)
            {
                return;
            }
            var rot = value.ReadValue<Vector2>();

            transform.rotation *= Quaternion.AngleAxis(rot.x * _info.HorizontalLookMultiplier, Vector3.up);

            _headRotation -= rot.y * _info.VerticalLookMultiplier; // Vertical look is inverted by default, hence the -=

            _headRotation = Mathf.Clamp(_headRotation, -89, 89);
            _head.transform.localRotation = Quaternion.AngleAxis(_headRotation, Vector3.right);
        }

        public void OnJump(InputAction.CallbackContext value)
        {
            if (_controller.isGrounded && _canMove)
            {
                _verticalSpeed = _info.JumpForce;
            }
        }

        public void OnSprint(InputAction.CallbackContext value)
        {
            _isSprinting = value.ReadValueAsButton();
        }

        public void OnAction(InputAction.CallbackContext value)
        {
            if (value.performed && _interactible != null && _interactible.IsAvailable)
            {
                if (_interactible.IsOneWay)
                {
                    _interactible.InteractOn(this);
                }
                else
                {
                    if (_canMove)
                    {
                        _canMove = false;
                        _interactible.InteractOn(this);
                    }
                    else
                    {
                        _canMove = true;
                        _interactible.InteractOff(this);
                    }
                }
            }
        }
    }
}
