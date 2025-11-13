using System.Collections.Generic;
using UnityEngine;


namespace ShadowMeter
{
    public class ShadowMeterScript : MonoBehaviour
    {
        [SerializeField]
        private bool directionalLights = true;

        [SerializeField]
        private bool pointLights = true;

        [SerializeField]
        private bool spotLights = true;

        [SerializeField, Range(0, 10)]
        private int receptorSensitivity = 1;

        [SerializeField]
        private float range = 10f;

        [SerializeField, Range(1, 30)]
        private int frequency = 15;

        [SerializeField]
        private bool includeIntensity = false;

        [SerializeField]
        private UnityEngine.UI.Slider mySlider;

        [SerializeField]
        private float mySliderSpeed = 5f;

        [SerializeField]
        private bool printResult = true;

        [SerializeField]
        private bool drawGizmos = true;

        [SerializeField, Range(-10, 10)]
        private float playerOffset = 0;

        private List<Light> Lights = new List<Light>();
        private float shadowMeterFloatValue = 0;
        private float smoothedShadowMeterValue = 0;
        private float smoothDampVelocity = 0f;
        private bool isHidden;

        //Public function you can use to get the shadow meter value 
        public float getShadowMeterValue()
        {
            return smoothedShadowMeterValue;
        }

        //Public function you can use to get the shadow meter bool value
        public bool getShadowMeterBool()
        {
            return isHidden;
        }

        void Start()
        {
            range = Mathf.Clamp(range, 0, float.MaxValue);
            if (FindObjectOfType<Light>())
            {
                Lights.AddRange(FindObjectsOfType<Light>());
                InvokeRepeating("RaycastLights", 1f, 1f / frequency);
            }
        }

        //Main function that checks how lit up the player is
        void RaycastLights()
        {
            Vector3 currentPos = transform.position + new Vector3(0, playerOffset, 0);
            isHidden = true;
            float maxLightValue = 0;

            //Go through all lights in the scene
            foreach (Light light in Lights)
            {
                if (!light.isActiveAndEnabled)
                    continue;

                float lightRange = light.range * (receptorSensitivity * 0.1f + 1);
                float distance = Vector3.Distance(light.transform.position, currentPos);

                float lightValue = 0;
                bool isLit = false;

                //Different operations depending on the type of light
                switch (light.type)
                {
                    case LightType.Point:
                        if (!pointLights)
                            break;

                        //if the light is in range, and the player is in rang of the light's range
                        if (distance < range && distance < lightRange)
                        {
                            Ray ray = new Ray(light.transform.position, currentPos - light.transform.position);
                            if (drawGizmos)
                                Debug.DrawRay(light.transform.position, currentPos - light.transform.position, Color.yellow, 1f);

                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, lightRange))
                            {
                                if (hit.transform.CompareTag("Player"))
                                {
                                    isLit = true;
                                    lightValue = includeIntensity
                                        ? Mathf.Clamp01(1 - (distance / (lightRange * light.intensity)))
                                        : Mathf.Clamp01(1 - (distance / lightRange));
                                }
                            }
                        }
                        break;

                    case LightType.Spot:
                        if (!spotLights)
                            break;

                        Vector3 forward = light.transform.forward;
                        Vector3 toTarget = currentPos - light.transform.position;

                        if (Vector3.Angle(forward, toTarget) <= light.spotAngle / 2)  //Check if the player is inside the spotlight's Cone
                        {
                            if (distance < lightRange)
                            {
                                isLit = true;
                                lightValue = includeIntensity
                                    ? Mathf.Clamp01(1 - (distance / (lightRange * light.intensity)))
                                    : Mathf.Clamp01(1 - (distance / lightRange));
                            }
                        }
                        break;

                    case LightType.Directional:
                        if (!directionalLights)
                            break;

                        Vector3 rayDirection = -light.transform.forward;
                        if (drawGizmos)
                            Debug.DrawRay(currentPos, rayDirection, Color.yellow, 1f);

                        if (!Physics.Raycast(currentPos, rayDirection, Mathf.Infinity)) //Check if there is anything blocking the directional light from reaching the player
                        {
                            isLit = true;
                            lightValue = includeIntensity
                                ? Mathf.Clamp01(light.intensity * 5 * (receptorSensitivity * 0.1f + 1) / 10)
                                : Mathf.Clamp01(5 * (receptorSensitivity * 0.1f + 1) / 10);
                        }
                        break;

                    default:
                        break;
                }

                if (isLit)
                {
                    isHidden = false;
                    if (lightValue > maxLightValue)
                    {
                        maxLightValue = lightValue;
                    }
                }
            }

            shadowMeterFloatValue = Mathf.Clamp01(maxLightValue); 
        }

        void Update()
        {
            // Smooth the value using SmoothDamp
            smoothedShadowMeterValue = Mathf.SmoothDamp(smoothedShadowMeterValue, shadowMeterFloatValue, ref smoothDampVelocity, 1f / mySliderSpeed);

            DisplayShadowMeter();
        }

        //This function is reponsible for changing the values in the display slider and printing 
        void DisplayShadowMeter()
        {
            if (mySlider)
                mySlider.value = smoothedShadowMeterValue;
            
            // if (printResult)
            //     print("Shadow Meter = " + smoothedShadowMeterValue);
        }

        //This function is reponsible for drawing Gizmos in-Editor
        void OnDrawGizmosSelected()
        {
            if (drawGizmos)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(this.transform.position + new Vector3(0, playerOffset, 0), range);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(this.transform.position + new Vector3(0, playerOffset, 0), 0.2f);
            }
        }

        //Editor Only Function that resets the main function using the newly defined frequency if its value is changed
        private void OnValidate()
        {
            range = Mathf.Clamp(range, 0, float.MaxValue);
            CancelInvoke();
            InvokeRepeating("RaycastLights", 1f, 1f / frequency);
        }
    }
}
