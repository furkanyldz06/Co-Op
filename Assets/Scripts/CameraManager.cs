using System;
using UnityEngine;

namespace GameOrganization
{
    public class CameraManager : MonoBehaviour
    {
        public Camera cam;
        public Transform followObj;
        public Transform lookObj;
        public float smoothSpeed;
        public float smoothSpeed2;
        public float moveSpeed;
        public float rotSpeed;

        public bool followPermission;

        [Header("Follow Dynamic Smooth Angle")]
        public Vector3 offset;

        [Header("Zoom Settings")]
        public float zoomSpeed = 2f;
        public float minZoomDistance = 3f;
        public float maxZoomDistance = 15f;
        public float currentZoomDistance = 8f;

        // public CameraAngleOfView cameraAngleOfView;

        #region Singleton
        public static CameraManager instance = null;
        private void Awake()
        {
            instance ??= this;
        }

        #endregion
    }
}