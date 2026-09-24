using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ARFurniture
{
    public sealed class ARPlacementController : MonoBehaviour
    {
        private const float EditorRotationSpeed = 0.2f;
        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        private ARPlaneManager _planeManager;
        private ARRaycastManager _raycastManager;
        private Material _planeLineMaterial;
        private GameObject _prefab;
        private GameObject _placedObject;
        private bool _ownsEnhancedTouch;

        public void Initialize()
        {
            if (_planeManager != null)
            {
                return;
            }

            _planeManager = GetComponent<ARPlaneManager>() ?? gameObject.AddComponent<ARPlaneManager>();
            _raycastManager = GetComponent<ARRaycastManager>() ?? gameObject.AddComponent<ARRaycastManager>();
            _planeLineMaterial = new Material(Shader.Find("Sprites/Default"));
            _planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            _planeManager.trackablesChanged.AddListener(OnPlanesChanged);

            _ownsEnhancedTouch = !EnhancedTouchSupport.enabled;
            if (_ownsEnhancedTouch)
            {
                EnhancedTouchSupport.Enable();
            }

            Exit();
        }

        public void Enter(GameObject prefab)
        {
            _prefab = prefab;
            DestroyPlacedObject();
            _planeManager.enabled = true;
            _raycastManager.enabled = true;
            enabled = true;
        }

        public void Exit()
        {
            enabled = false;
            _prefab = null;
            DestroyPlacedObject();

            if (_planeManager != null)
            {
                _planeManager.enabled = false;
                _raycastManager.enabled = false;
            }
        }

        private void Update()
        {
            var touches = Touch.activeTouches;
            if (touches.Count >= 2 && _placedObject != null && touches[0].inProgress && touches[1].inProgress)
            {
                if (!IsPointerOverUI(touches[0].touchId) && !IsPointerOverUI(touches[1].touchId))
                {
                    _placedObject.transform.Rotate(
                        Vector3.up,
                        RotationDelta(touches[0], touches[1]),
                        Space.World);
                }

                return;
            }

            if (touches.Count == 1 && touches[0].inProgress && !IsPointerOverUI(touches[0].touchId))
            {
                var touch = touches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began && _placedObject == null)
                {
                    Place(touch.screenPosition);
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && _placedObject != null)
                {
                    Move(touch.screenPosition);
                }

                return;
            }

#if UNITY_EDITOR
            HandleMouse();
#endif
        }

        private void Place(Vector2 screenPosition)
        {
            if (_prefab == null || _placedObject != null || !TryGetPlanePose(screenPosition, out var pose))
            {
                return;
            }

            _placedObject = Instantiate(_prefab, pose.position, pose.rotation);
        }

        private void Move(Vector2 screenPosition)
        {
            if (TryGetPlanePose(screenPosition, out var pose))
            {
                _placedObject.transform.position = pose.position;
            }
        }

        private bool TryGetPlanePose(Vector2 screenPosition, out Pose pose)
        {
            if (_raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            {
                pose = Hits[0].pose;
                return true;
            }

            pose = default;
            return false;
        }

        private static float RotationDelta(Touch first, Touch second)
        {
            return RotationDelta(
                first.screenPosition,
                first.delta,
                second.screenPosition,
                second.delta);
        }

        public static float RotationDelta(
            Vector2 firstPosition,
            Vector2 firstDelta,
            Vector2 secondPosition,
            Vector2 secondDelta)
        {
            var currentDirection = secondPosition - firstPosition;
            var previousDirection = secondPosition - secondDelta - (firstPosition - firstDelta);
            if (currentDirection.sqrMagnitude < 0.01f || previousDirection.sqrMagnitude < 0.01f)
            {
                return 0;
            }

            return -Vector2.SignedAngle(previousDirection, currentDirection);
        }

        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> changes)
        {
            foreach (var plane in changes.added)
            {
                var line = plane.gameObject.AddComponent<LineRenderer>();
                line.loop = true;
                line.useWorldSpace = false;
                line.widthMultiplier = 0.01f;
                line.startColor = line.endColor = new Color(0.12f, 0.7f, 0.55f, 0.9f);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = _planeLineMaterial;
                plane.gameObject.AddComponent<ARPlaneMeshVisualizer>();
            }
        }

        private static bool IsPointerOverUI(int pointerId = -1)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId < 0
                ? EventSystem.current.IsPointerOverGameObject()
                : EventSystem.current.IsPointerOverGameObject(pointerId);
        }

#if UNITY_EDITOR
        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || IsPointerOverUI())
            {
                return;
            }

            var shiftPressed = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (shiftPressed && mouse.leftButton.isPressed && _placedObject != null)
            {
                _placedObject.transform.Rotate(
                    Vector3.up,
                    mouse.delta.ReadValue().x * EditorRotationSpeed,
                    Space.World);
            }
            else if (mouse.leftButton.wasPressedThisFrame && _placedObject == null)
            {
                Place(mouse.position.ReadValue());
            }
            else if (mouse.leftButton.isPressed && _placedObject != null)
            {
                Move(mouse.position.ReadValue());
            }
        }
#endif

        private void DestroyPlacedObject()
        {
            if (_placedObject != null)
            {
                Destroy(_placedObject);
                _placedObject = null;
            }
        }

        private void OnDestroy()
        {
            if (_planeManager != null)
            {
                _planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            }

            if (_ownsEnhancedTouch)
            {
                EnhancedTouchSupport.Disable();
            }

            Destroy(_planeLineMaterial);
        }
    }
}
