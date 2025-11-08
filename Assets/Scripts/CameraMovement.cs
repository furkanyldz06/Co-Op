using UnityEngine;
using UnityEngine.InputSystem;

namespace GameOrganization
{
    public class CameraMovement : MonoBehaviour
    {
        public CameraManager cameraManager;

        // Cached references for performance
        private Mouse _mouse;
        private Transform _followTransform;
        private Transform _lookTransform;

        public void firstLook()
        {
            if (cameraManager.followObj)
            {
                Vector3 followPos = cameraManager.followObj.position;
                transform.position = new Vector3(followPos.x + 5f, followPos.y + 6.5f, followPos.z);
            }
        }

        private void Start()
        {
            // Cache mouse reference
            _mouse = Mouse.current;

            // Cache transform references
            if (cameraManager != null)
            {
                _followTransform = cameraManager.followObj;
                _lookTransform = cameraManager.lookObj;
            }
        }

        void LateUpdate()
        {
            if (_followTransform == null) return;

            HandleZoom();
            Look();
            Move();

            if (cameraManager.followPermission)
                Sensitivity();
        }

        void HandleZoom()
        {
            if (_mouse == null) return;

            float scrollInput = _mouse.scroll.ReadValue().y / 120f;

            if (scrollInput != 0f)
            {
                cameraManager.currentZoomDistance -= scrollInput * cameraManager.zoomSpeed;
                cameraManager.currentZoomDistance = Mathf.Clamp(
                    cameraManager.currentZoomDistance,
                    cameraManager.minZoomDistance,
                    cameraManager.maxZoomDistance
                );

                Vector3 offsetDirection = cameraManager.offset.normalized;
                cameraManager.offset = offsetDirection * cameraManager.currentZoomDistance;
            }
        }

        void Look()
        {
            if (_lookTransform == null) return;

            Vector3 targetPos = _lookTransform.position;
            Vector3 direction = targetPos - transform.position;

            if (direction.sqrMagnitude > 0.001f) // Avoid zero-length vector
            {
                Quaternion rotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * cameraManager.rotSpeed);
            }
        }

        void Move()
        {
            Vector3 desiredPosition = _followTransform.position + cameraManager.offset;
            float smoothFactor = cameraManager.smoothSpeed * Time.deltaTime * cameraManager.moveSpeed;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothFactor);
        }

        void Sensitivity()
        {
            cameraManager.smoothSpeed = Mathf.Clamp(
                cameraManager.smoothSpeed + 0.02f * Time.deltaTime,
                0.01f,
                cameraManager.smoothSpeed2
            );
        }
    }
}
