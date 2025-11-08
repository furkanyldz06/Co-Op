using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameOrganization
{
    public class CameraMovement : MonoBehaviour
    {
        public CameraManager cameraManager;
        // private CharacterState _characterState;
        public void firstLook()
        {
            if (cameraManager.followObj)
            {
                transform.position = new Vector3(cameraManager.followObj.transform.position.x + 5,
                    cameraManager.followObj.transform.position.y + 6.5f, cameraManager.followObj.transform.position.z);

                // _characterState = cameraManager.followObj.GetComponent<FixedScale>().characterState;
            }
        }

        void LateUpdate()
        {
            if (cameraManager.followObj)
            {
                Look();
                Move();
                if(cameraManager.followPermission)
                    Sensitivity();
            }
        }

        void Look()
        {
            Vector3 targetPos = cameraManager.lookObj.position;
            Quaternion rotation = Quaternion.LookRotation(targetPos - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * cameraManager.rotSpeed);
        }

        void Look2()
        {
            Vector3 targetPos = cameraManager.followObj.position;
            Quaternion rotation = Quaternion.LookRotation(targetPos - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * cameraManager.rotSpeed);
        }

        void Move()
        {
            Vector3 desiredPosition = cameraManager.followObj.position + cameraManager.offset;
            // Time.deltaTime ile frame-independent smooth movement
            float smoothFactor = cameraManager.smoothSpeed * Time.deltaTime * cameraManager.moveSpeed;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothFactor);
            transform.position = smoothedPosition;
        }

        void Sensitivity()
        {
            cameraManager.smoothSpeed = Mathf.Clamp(cameraManager.smoothSpeed + 0.02f * Time.deltaTime, 0.01f, cameraManager.smoothSpeed2);
        }
    }
}
