//-----------------------------------------------------------------------
// <copyright file="ComputerVisionController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.ComputerVision
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using GoogleARCore;
    using UnityEngine;
    using UnityEngine.UI;
    using TensorFlow;
    using TFClassify;
    using System.Text.RegularExpressions;
    using System.Linq;
    using System.Threading.Tasks;


    #if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
    #endif  // UNITY_EDITOR

    /// <summary>
    /// Controller for the ComputerVision example that accesses the CPU camera image (i.e. image
    /// bytes), performs edge detection on the image, and renders an overlay to the screen.
    /// </summary>
    public class ComputerVisionController : MonoBehaviour
    {
        /// <summary>
        /// The ARCoreSession monobehavior that manages the ARCore session.
        /// </summary>
        public ARCoreSession ARSessionManager;

        /// <summary>
        /// The frame rate update interval.
        /// </summary>
        private static float s_FrameRateUpdateInterval = 2.0f;
        private bool m_IsQuitting = false;
        private bool m_UseHighResCPUTexture = false;
        private ARCoreSession.OnChooseCameraConfigurationDelegate m_OnChoseCameraConfiguration =
            null;

        private int m_HighestResolutionConfigIndex = 0;
        private int m_LowestResolutionConfigIndex = 0;
        private bool m_Resolutioninitialized = false;
        private float m_RenderingFrameRate = 0f;
        private float m_RenderingFrameTime = 0f;
        private int m_FrameCounter = 0;
        private float m_FramePassedTime = 0.0f;


        // TextureReader Component variables
        private TextureReader TextureReaderComponent;

        public Texture2D m_TextureToRender;

        private byte[] m_OutputImage = null;
        private int m_ImageWidth;
        private int m_ImageHeight;
        
         public Color32[] rotated;

         // Detector variables
   
         public Detector detector;
         private const int detectorInputSize = 300;
       

         // Boxes GUI variables

        
         private List<BoxOutline> boxOutlines;
         private static Texture2D boxOutlineTexture;
         private static GUIStyle labelStyle;
         
         // Nutrition panel variables
         public Camera firstPersonCamera;
         public NutritionPanelController nutritionPanel;
         private TextMesh foodName;
         private TextMesh foodInfo;

         private Material caloriesInd;
         private Material saturedFatInd;
         private Material transFatInd;
         private Material sodiumInd;
         private Material proteinInd;
         
         
         // Dictionnary of detected food nutrition facts (100g)
         IDictionary<string,float[]> foodDict = new Dictionary<string, float[]>()
         {
             {"banana",new float[] {89,0.2f,0,1,1.1f}},
             {"pizza", new float[] {266,10,0.2f,598,11}},
             {"donut", new float[] {452,15,9,326,4.9f}}
         };
         
         
         
        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Lock screen to portrait.
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.Portrait;

            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;

            // Register the callback to set camera config before arcore session is enabled.
            m_OnChoseCameraConfiguration = _ChooseCameraConfiguration;
            ARSessionManager.RegisterChooseCameraConfigurationCallback(
                m_OnChoseCameraConfiguration);
        }

        /// <summary>
        /// The Unity Start() method.
        /// </summary>
        public void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Enable Camera auto focus
            var config = ARSessionManager.SessionConfig;
            if (config != null)
            {
                config.CameraFocusMode = CameraFocusMode.AutoFocus;
            }

            // Texture Reader Handle
            // Add a callback on each frame retrieved by CPU
            TextureReaderComponent = GetComponent<TextureReader> ();
            TextureReaderComponent.OnImageAvailableCallback += OnImageAvailable;
            
            // Food name (label) and food info text mesh
            foodName = GameObject.FindWithTag("FoodNameTag").GetComponent<TextMesh>();
            foodInfo = GameObject.FindWithTag("InfoTag").GetComponent<TextMesh>();
            // Color Indicators
            caloriesInd = GameObject.Find("CaloriesInd").GetComponent<Renderer>().material;
            saturedFatInd = GameObject.Find("SaturedFatInd").GetComponent<Renderer>().material;
            transFatInd = GameObject.Find("TransFatInd").GetComponent<Renderer>().material;
            sodiumInd = GameObject.Find("SodiumInd").GetComponent<Renderer>().material;
            proteinInd = GameObject.Find("ProteinInd").GetComponent<Renderer>().material;
        }
        
        /// <summary>
        /// Unity OnGUI method called several time per frame to handle graphics code (layout).
        /// Draw box outline on the GUI in testing environement only (camera view).
        /// </summary>
        public void OnGUI()
        {
            
            if (this.boxOutlines != null && this.boxOutlines.Any())
            {
                foreach (var outline in this.boxOutlines)
                {
                    //Uncomment for testing, developing
                    //DrawBoxOutline(outline);
                }
            }
        }

        /// <summary>
        /// The Unity Update() method. (called once per frame)
        /// Call the detector if the device touchscreen was pressed and then update the panel.
        /// </summary>

        public async void Update()
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            _QuitOnConnectionErrors();
            _UpdateFrameRate();


            if (!Session.Status.IsValid())
            {
                return;
            }

            // Google ARCore, pose tracking
            // The session status must be Tracking in order to access the Frame.
            if (Session.Status != SessionStatus.Tracking)
            {
                int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
                return;
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;


            Touch touch;
            if (Input.touchCount != 1 ||
                (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }

            // Process image on click or touch (Crop, Scale, Rotate)
            ScaleCropRotate();

            // Forward the input image into the model 
            boxOutlines = await detector.ProcessImageAsync(rotated);

            Debug.Log("Box outlines length : " + boxOutlines.Count);
            if (boxOutlines.Count > 0)
            {
                Debug.Log("Update, score  : " + boxOutlines[0].Score + " classe : " + boxOutlines[0].Label);
                UpdatePanel();
            }     
            
            
            // Select plane attached to the nutrition panel according to the touch position using Raycast
            TrackableHit hit;
            TrackableHitFlags raycastFilter =
                TrackableHitFlags.PlaneWithinBounds |
                TrackableHitFlags.PlaneWithinPolygon;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                Debug.Log(" Touch position x : " + touch.position.x + "y : " + touch.position.y);
                SetSelectedPlane(hit.Trackable as DetectedPlane);
                
            }
            
            
        }
        
        /// <summary>
        /// Crop, Scale and Rotate the rendered image
        /// </summary>
        void ScaleCropRotate()
        {
            var snap = TakeTextureSnap();
            SaveToFile(snap, "snap.png");
            
            var scaled = Scale(snap, detectorInputSize);
            SaveToFile(scaled, "scaled.png");


            rotated = RotateAsync(scaled.GetPixels32(), scaled.width, scaled.height);
            // Convert into texture and save the image file as .png
            Texture2D rotatedTexture = new Texture2D( (int)scaled.width, (int)scaled.height);
            rotatedTexture.SetPixels32(rotated);
            rotatedTexture.Apply();
            SaveToFile(rotatedTexture, "rotated.png");


            Destroy(snap);
            Destroy(scaled);

        }


        /// <summary>
        /// Crop the rendered image to have a squared image using the smallest image dimension
        /// </summary>
        private Texture2D TakeTextureSnap()
        {
            var smallestDimension = Math.Min(m_ImageWidth, m_ImageHeight);
            var snap = TextureTools.CropWithRect(m_TextureToRender,
                new Rect(0, 0, smallestDimension, smallestDimension),
                TextureTools.RectOptions.Center, 0, 0);
            return snap;
        }
        

        /// <summary>
        /// Resize the image into smaller dimension for the detector
        /// </summary>
        private Texture2D Scale(Texture2D texture, int imageSize)
        {
            var scaled = TextureTools.scaled(texture, imageSize, imageSize, FilterMode.Bilinear);
            return scaled;
        }
        
        /// <summary>
        /// Rotate the image by -90°. By default the image is retrieved in landscape mode.
        /// </summary>
        private Color32[] RotateAsync(Color32[] pixels, int width, int height)
        {
            
            return TextureTools.RotateImageMatrix(
                    pixels, width, height, -90);
           
        }
        

        /// <summary>
        /// Update the panel using the latest boxOutlines given by the detector.
        /// Only the HIGHEST score food class prediction is displayed in the panel
        /// </summary>
        void UpdatePanel()
        {
            
            // Retrieve the HIGHEST accuracy food class label
            var label = boxOutlines[0].Label;
            if (string.Equals(label, "banana") || string.Equals(label, "pizza") || string.Equals(label, "donut"))
            {
                Debug.Log("Label exists in DB ");
            }
            else
            {
                // For demo, assume the default detected object displays the banana's nutrition facts
                label = "banana";
            }
            
            // Update the panel color indicators (Must use a nutrition score. To be done)
            switch(label)
            {
                case "banana":
                    saturedFatInd.SetColor("_Color", Color.green);
                    transFatInd.SetColor("_Color", Color.green);
                    sodiumInd.SetColor("_Color", Color.green);
                    break;
                case "pizza":
                    saturedFatInd.SetColor("_Color", Color.red);
                    transFatInd.SetColor("_Color", Color.red);
                    sodiumInd.SetColor("_Color", Color.red);
                    break;
                case "donut":
                    saturedFatInd.SetColor("_Color", Color.red);
                    transFatInd.SetColor("_Color", Color.red);
                    sodiumInd.SetColor("_Color", Color.green);
                    break;
                default:
                    break;
            }

            
            
            // Update the panel text
            var score = (int) (boxOutlines[0].Score * 100);
            foodName.text = label + " : (" + score + "%)";
            foodInfo.text = "Calories     " + (foodDict[label][0]).ToString() + "\n" +
                        "Satured Fat   " + (foodDict[label][1]).ToString() + "g\n" +
                        "Trans Fat   " + (foodDict[label][2]).ToString() + "g\n" +
                        "Sodium      " + (foodDict[label][3]).ToString() + "mg\n" +
                        "Protein  " + (foodDict[label][4]).ToString() + "g\n";
            
        }
        

        /// <summary>
        /// Select the plane attached to the nutrition panel 
        /// </summary>
        void SetSelectedPlane(DetectedPlane selectedPlane)
        {
            // Call for nutrition panel controller : Anchor scoreboard to the selected plane
            nutritionPanel.SetSelectedPlane(selectedPlane);

        }
        
        private void _UpdateFrameRate()
        {
            m_FrameCounter++;
            m_FramePassedTime += Time.deltaTime;
            if (m_FramePassedTime > s_FrameRateUpdateInterval)
            {
                m_RenderingFrameTime = 1000 * m_FramePassedTime / m_FrameCounter;
                m_RenderingFrameRate = 1000 / m_RenderingFrameTime;
                m_FramePassedTime = 0f;
                m_FrameCounter = 0;
            }
        }




        /// <summary>
        /// Handles a new CPU image.
        /// </summary>
        /// <param name="format">The format of the image.</param>
        /// <param name="width">Width of the image, in pixels.</param>
        /// <param name="height">Height of the image, in pixels.</param>
        /// <param name="pixelBuffer">Pointer to raw image buffer.</param>
        /// <param name="bufferSize">The size of the image buffer, in bytes.</param>
        private void OnImageAvailable(TextureReaderApi.ImageFormatType format, int width, int height, IntPtr pixelBuffer, int bufferSize)
        {

            // Initialize texture, output image (bytes array) and output width/height
            if (m_TextureToRender == null || m_OutputImage == null || m_ImageWidth != width || m_ImageHeight != height)
            {
                m_TextureToRender = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                m_OutputImage = new byte[width * height * 4];
                m_ImageWidth = width;
                m_ImageHeight = height;
            }
            
            // Copy the pixelBuffer from the TextureReaderAPI stored value to m_OutputImage : 1920*1080*4 byte array
            System.Runtime.InteropServices.Marshal.Copy(pixelBuffer, m_OutputImage, 0, bufferSize);

            // Update the rendering texture
            m_TextureToRender.LoadRawTextureData(m_OutputImage);
            m_TextureToRender.Apply();
      

        }
        
        
        /// <summary>
        /// Save the given texture into .png image file
        /// </summary>
        public void SaveToFile(Texture2D texture, string filename)
        {
            File.WriteAllBytes(
                Application.persistentDataPath + "/" +
                filename, texture.EncodeToPNG());
        }

        /// <summary>
        /// Quit the application if there was a connection error for the ARCore session.
        /// </summary>
        private void _QuitOnConnectionErrors()
        {
            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to
            // appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status == SessionStatus.FatalError)
            {
                _ShowAndroidToastMessage(
                    "ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject =
                        toastClass.CallStatic<AndroidJavaObject>(
                            "makeText", unityActivity, message, 0);
                    toastObject.Call("show");
                }));
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }


        /// <summary>
        /// Select the desired camera configuration.
        /// If high resolution toggle is checked, select the camera configuration
        /// with highest cpu image and highest FPS.
        /// If low resolution toggle is checked, select the camera configuration
        /// with lowest CPU image and highest FPS.
        /// </summary>
        /// <param name="supportedConfigurations">A list of all supported camera
        /// configuration.</param>
        /// <returns>The desired configuration index.</returns>
        private int _ChooseCameraConfiguration(List<CameraConfig> supportedConfigurations)
        {
            if (!m_Resolutioninitialized)
            {
                m_HighestResolutionConfigIndex = 0;
                m_LowestResolutionConfigIndex = 0;
                CameraConfig maximalConfig = supportedConfigurations[0];
                CameraConfig minimalConfig = supportedConfigurations[0];
                for (int index = 1; index < supportedConfigurations.Count; index++)
                {
                    CameraConfig config = supportedConfigurations[index];
                    if ((config.ImageSize.x > maximalConfig.ImageSize.x &&
                         config.ImageSize.y > maximalConfig.ImageSize.y) ||
                        (config.ImageSize.x == maximalConfig.ImageSize.x &&
                         config.ImageSize.y == maximalConfig.ImageSize.y &&
                         config.MaxFPS > maximalConfig.MaxFPS))
                    {
                        m_HighestResolutionConfigIndex = index;
                        maximalConfig = config;
                    }

                    if ((config.ImageSize.x < minimalConfig.ImageSize.x &&
                         config.ImageSize.y < minimalConfig.ImageSize.y) ||
                        (config.ImageSize.x == minimalConfig.ImageSize.x &&
                         config.ImageSize.y == minimalConfig.ImageSize.y &&
                         config.MaxFPS > minimalConfig.MaxFPS))
                    {
                        m_LowestResolutionConfigIndex = index;
                        minimalConfig = config;
                    }
                }

                m_Resolutioninitialized = true;
            }

            if (m_UseHighResCPUTexture)
            {
                return m_HighestResolutionConfigIndex;
            }

            return m_LowestResolutionConfigIndex;
        }
        
        
	    
	    
	    /// <summary>
        /// Draw boxes outlines on screen.
        /// </summary>
	    private void DrawBoxOutline(BoxOutline outline)
	    {
		    var xMin = outline.XMin * Screen.width;
		    var xMax = outline.XMax * Screen.width;
		    var yMin = outline.YMin * Screen.height;
		    var yMax = outline.YMax * Screen.height;
      
		    DrawRectangle(new Rect(xMin, yMin, xMax - xMin, yMax - yMin), 4, Color.red);
            DrawLabel(new Rect(xMin + 10, yMin + 10, 200, 20), $"{outline.Label}: {(int)(outline.Score * 100)}%");
	    }
	    
        /// <summary>
        /// Draw box rectangle on screen.
        /// </summary>
	    public static void DrawRectangle(Rect area, int frameWidth, Color color)
	    {
		    // Create a one pixel texture with the right color
		    if (boxOutlineTexture == null)
		    {
			    var texture = new Texture2D(1, 1);
			    texture.SetPixel(0, 0, color);
			    texture.Apply();
			    boxOutlineTexture = texture;
		    }
            
		    Rect lineArea = area;
		    lineArea.height = frameWidth;
		    GUI.DrawTexture(lineArea, boxOutlineTexture); // Top line

		    lineArea.y = area.yMax - frameWidth; 
		    GUI.DrawTexture(lineArea, boxOutlineTexture); // Bottom line

		    lineArea = area;
		    lineArea.width = frameWidth;
		    GUI.DrawTexture(lineArea, boxOutlineTexture); // Left line

		    lineArea.x = area.xMax - frameWidth;
		    GUI.DrawTexture(lineArea, boxOutlineTexture); // Right line
	    }

        /// <summary>
        /// Draw box label on screen.
        /// </summary>
	    private static void DrawLabel(Rect position, string text)
	    {
		    if (labelStyle == null)
		    {
			    var style = new GUIStyle();
			    style.fontSize = 50;
			    style.normal.textColor = Color.red;
			    labelStyle = style;
		    }

		    GUI.Label(position, text, labelStyle);
	    }
    }
    

    public class BoxOutline
    {
        public float YMin { get; set; } = 0;
        public float XMin { get; set; } = 0;
        public float YMax { get; set; } = 0;
        public float XMax { get; set; } = 0;
        public string Label { get; set; }
        public float Score { get; set; }
    }
    
}
