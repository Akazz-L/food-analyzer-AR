using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleARCore;


/// <summary>
/// Nutrition panel controller that attach the panel 
/// to a selected plane and move the panel depending on the user's view.
/// </summary>
public class NutritionPanelController : MonoBehaviour
{
	/// <summary>
    /// The first person camera used in the ARCore Session
    /// </summary>
	public Camera firstPersonCamera;

	/// <summary>
    /// Nutrition panel anchor located in the world cordinated space
    /// </summary>
	private Anchor anchor;
	/// <summary>
    /// Nutrition panel attached plane
    /// </summary>
	private DetectedPlane detectedPlane;
	/// <summary>
    /// Nutrition panel y-axis offset to display the panel above the detected plane
    /// </summary>
	private float yOffset;

    /// <summary>
    /// Unity start method. Disable the panel display when the app starts.
    /// </summary>
    void Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
	    {
	        r.enabled = false;
	    }
    }

   	/// <summary>
    /// Unity update method. Update the detected plane.
    /// </summary>
    void Update()
    {
        // The tracking state must be FrameTrackingState.Tracking
		// in order to access the Frame.
		if (Session.Status != SessionStatus.Tracking)
		{
		    return;
		}

		// If there is no plane, then return
		if (detectedPlane == null)
		{
		    return;
		}

		// Check for the plane being subsumed.
		// If the plane has been subsumed switch attachment to the subsuming plane.
		while (detectedPlane.SubsumedBy != null)
		{
		    detectedPlane = detectedPlane.SubsumedBy;
		}
		

   }


	/// <summary>
    /// Unity update method. Update the detected plane.
    /// </summary>
    public void SetSelectedPlane(DetectedPlane detectedPlane)
    {

	    this.detectedPlane = detectedPlane;
	    CreateAnchor();
	    
	    // Make the scoreboard face the viewer.
	    transform.LookAt (firstPersonCamera.transform); 

	    // Move the position to stay consistent with the plane.
	    transform.position = new Vector3(transform.position.x,
		    detectedPlane.CenterPose.position.y + yOffset, transform.position.z);

	}

	/// <summary>
    /// Create the position of the anchor by raycasting a point towards
	/// the top of the screen
    /// </summary>
	void CreateAnchor()
	{
	    
	    
	    Vector2 pos = new Vector2 (Screen.width * .5f, Screen.height * .90f);
	    Ray ray = firstPersonCamera.ScreenPointToRay (pos);
	    Vector3 anchorPosition = ray.GetPoint (5f);

	    // Create the anchor at that point.
	    if (anchor != null) {
	      DestroyObject (anchor);
	    }
	    
	    anchor = detectedPlane.CreateAnchor (
	        new Pose (anchorPosition, Quaternion.identity));

		// Attach the nutrition panel to the anchor.
	    transform.position = anchorPosition;
	    transform.SetParent(anchor.transform);
	    yOffset = transform.position.y - detectedPlane.CenterPose.position.y;

	    // Enable the renderers to display the nutrition panel.
	    foreach (Renderer r in GetComponentsInChildren<Renderer>())
	    {
	        r.enabled = true;
	    }
	}

}
