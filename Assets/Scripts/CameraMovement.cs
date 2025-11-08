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

        void FixedUpdate()
        // void FixedUpdate()
        {
            if (cameraManager.followObj)
            {
                // if (!_characterState)
                // {
                //     _characterState = cameraManager.followObj.GetComponent<FixedScale>().characterState;
                // }

                // if (_characterState.ragdollControl.enableRagdoll) Look2();
                // else Look();
                
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
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.fixedDeltaTime);
        }
        
        void Look2()
        {
            Vector3 targetPos = cameraManager.followObj.position;
            Quaternion rotation = Quaternion.LookRotation(targetPos - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.fixedDeltaTime);
        }

        void Move()
        {
            Vector3 desiredPoisiton = cameraManager.followObj.position + cameraManager.offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPoisiton, cameraManager.smoothSpeed);
            transform.position = smoothedPosition;
        }

        void Sensitivity()
        {
            cameraManager.smoothSpeed = Mathf.Clamp(cameraManager.smoothSpeed + 0.02f * Time.deltaTime, 0.01f, 0.075f);
        }
    }
}
