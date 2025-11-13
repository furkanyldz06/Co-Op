# Shadow Meter - Light Detection
Shadow Meter - Light Detection is a powerful and easy-to-use Unity script that measures how much a player is illuminated by different light sources in your scene. The tool provides a real-time visual representation using a customizable UI slider, making it perfect for stealth, horror, or any game where lighting and shadows are critical elements.

## How to Set Up
1. Import the Package:
	- Download and import the Shadow Meter package into your Unity project.
2. Add the Script:
	- Attach the ShadowMeterScript to the player GameObject or any other object you want to track the light exposure for.
3. Configure the Settings:
	- In the Inspector, you will find various settings to tweak the behavior of the Shadow Meter:
		- Directional Lights: Toggle whether to include directional lights in the calculations.
		- Point Lights: Toggle whether to include point lights in the calculations.
		- Spot Lights: Toggle whether to include spotlights in the calculations.
		- Receptor Sensitivity: Adjust the sensitivity of the light detection.
		- Range: Set the range within which the lights will affect the Shadow Meter.
		- Frequency: Set how frequently the calculations should be updated.
		- Include Intensity: Toggle whether to include light intensity in the calculations.
		- Player Offset: Adjust the offset to fine-tune the player’s detection position.
		- Draw Gizmos: Toggle whether to draw gizmos for debugging in the Scene view.
4. UI Setup:
	- Create a UI Slider in your scene or use an existing one.
	- Assign the Slider to the mySlider field in the Inspector.
5. Invoke Raycasting:
	- The script automatically starts raycasting for light exposure upon starting the scene. No additional setup is required.


## Demo Setup
The included demo is rendered in the Universal Render Pipeline (URP). To run the demo correctly:
1. Import URP:
	- Ensure that your project is set up to use the Universal Render Pipeline. If not, you can import URP through Unity’s Package Manager.
2. Third Person Controller:
	- The demo scene requires the Third Person Controller package from Unity. You can import this package directly from the Unity Asset Store or the Unity Package Manager.
3. DOTWeen:
	- The demo scene requires the DOTWeen package. You can import this package directly from the Unity Asset Store.

## How to Use
1. Displaying Light Exposure:
	- The script will automatically calculate the light exposure and update the assigned Slider in real time. 	
	The Slider value will smoothly transition between values, indicating how much the player is illuminated.
2. Accessing Values Programmatically:
	- You can access the current Shadow Meter value using the getShadowMeterValue() method, which returns a normalized value between 0 and 1.
	- To check if the player is completely hidden, use the getShadowMeterBool() method.
3. Debugging:
	- Enable the Draw Gizmos option to visualize the light detection in the Scene view. This is useful for debugging and fine-tuning your settings.
4. Customizing the Display:
	- Adjust the mySliderSpeed to control how fast the slider updates its value. A lower value will create a smoother transition.

## Notes
- When importing to your own project, make sure the player is tagged with the tag "Player" and that it has a valid collider. Otherwise, the raycasts won't work.
- The script automatically detects all light sources in the scene upon startup. If you add or remove lights during runtime, consider refreshing the list of lights.
- Adjust the receptorSensitivity and range settings to match your game's specific lighting environment for more accurate results.
- The Shadow Meter can be used in a variety of genres, including stealth, horror, and simulation games where lighting is a key gameplay element.
- Feel free to modify the script to fit your specific needs, but remember to keep backup copies of the original script.
- Please Leave a Review on the Asset Store Page if this asset has helped you