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
        public bool followPermission;
        [Header("Follow Dynamic Smooth Angle")]
        public Vector3 offset;

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