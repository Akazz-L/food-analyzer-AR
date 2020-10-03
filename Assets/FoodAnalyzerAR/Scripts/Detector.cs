using System;
using UnityEngine;
using System.Collections.Generic;
using TensorFlow;
using System.Linq;
using GoogleARCore.Examples.ComputerVision;
using System.Threading.Tasks;

public class Detector : MonoBehaviour
{

	[Header("Constants")] 
	private const int INPUT_SIZE = 300;
	private const int IMAGE_MEAN = 117;
	private const float IMAGE_STD = 1;
	private const string INPUT_TENSOR = "input";
	private const string OUTPUT_TENSOR = "output";

    [Header("Inspector Stuff")]
	public ComputerVisionController camFeed;
    public TextAsset labelMap;
    public TextAsset model;

    private static float MINIMUM_CONFIDENCE = 0.4f;
	private TFGraph graph;
	private TFSession session;
	private string [] labels;
	
	
	/// <summary>
	/// The Unity Start() method.
	/// </summary>
	void Start() {
#if UNITY_ANDROID
        TensorFlowSharp.Android.NativeBinding.Init();
#endif
		// Load labels into string array
		labels = labelMap.ToString ().Split ('\n');
		
		//load graph
		graph = new TFGraph ();
		graph.Import (model.bytes);
		session = new TFSession(graph);
	}
	
	
	

	/// <summary>
	/// The Unity Update() method.
	/// </summary>
	private void Update () {
		
	}



	/// <summary>
	/// Forward the byte array image into the model and return the boxes results 
	/// </summary>
	public List<BoxOutline> ProcessImage(Color32[] input){

		
		
		var tensor = TransformInput (input, 300, 300);
		
		var runner = session.GetRunner();
		runner.AddInput(this.graph["image_tensor"][0], tensor)
			.Fetch(this.graph["detection_boxes"][0],
				this.graph["detection_scores"][0],
				this.graph["detection_classes"][0],
				this.graph["num_detections"][0]);
		
		// Evaluate operations in the graph and output the previous fetches
		var output = runner.Run();

		var boxes = (float[,,])output[0].GetValue(jagged: false);
		var scores = (float[,])output[1].GetValue(jagged: false);
		var classes = (float[,])output[2].GetValue(jagged: false);
		var num = (float [])output [3].GetValue (jagged: false);



		foreach(var ts in output)
		{
			ts.Dispose();
		}

		return GetBoxes(boxes, scores, classes, MINIMUM_CONFIDENCE);

	}

	/// <summary>
	/// Asynchronous call of ProcessImage function.
	/// </summary>
	public Task<List<BoxOutline>> ProcessImageAsync(Color32[] input){
		return Task.Run(() => ProcessImage(input));
	}

	/// <summary>
	/// Convert 32-bytes array (rgba) into TFTensor
	/// Reference : from https://github.com/Syn-McJ/TFClassify-Unity
	/// </summary>
	public static TFTensor TransformInput (Color32 [] pic, int width, int height) {
		byte [] floatValues = new byte [width * height * 3];

		for (int i = 0; i < pic.Length; ++i) {
			var color = pic [i];

			floatValues [i * 3 + 0] = (byte)((color.r - IMAGE_MEAN) / IMAGE_STD);
			floatValues [i * 3 + 1] = (byte)((color.g - IMAGE_MEAN) / IMAGE_STD);
			floatValues [i * 3 + 2] = (byte)((color.b - IMAGE_MEAN) / IMAGE_STD);
		}

		TFShape shape = new TFShape (1, width, height, 3);

		return TFTensor.FromBuffer (shape, floatValues, 0, floatValues.Length);
	}
	


	/// <summary>
	/// Get the boxes outlines from separated boxes, scores, classes arrays
	/// </summary>
	private List<BoxOutline> GetBoxes(float[,,] boxes, float[,] scores, float[,] classes, double minScore)
	{
		var x = boxes.GetLength(0);
		var y = boxes.GetLength(1);
		var z = boxes.GetLength(2);

		Debug.Log("highest class : " + classes[0, 0] + " highest score : " + scores[0, 0]);	    
		    
		float ymin = 0, xmin = 0, ymax = 0, xmax = 0;
		var results = new List<BoxOutline>();

		for (int i = 0; i < x; i++) 
		{
			for (int j = 0; j < y; j++) 
			{    
				    
				if (scores [i, j] < minScore) continue;

				for (int k = 0; k < z; k++) 
				{
					var box = boxes [i, j, k];
					switch (k) {
						case 0:
							ymin = box;
							break;
						case 1:
							xmin = box;
							break;
						case 2:
							ymax = box;
							break;
						case 3:
							xmax = box;
							break;
					}
				}
				    
				int value = Convert.ToInt32(classes[i, j]);
				var label = this.labels[value];

				    
				Debug.Log("recognized objects\n - value : " + value + " label : " + label + " score : " + scores[i,j]);
				    
				    
				    
				var boxOutline = new BoxOutline
				{
					YMin = ymin,
					XMin = xmin,
					YMax = ymax,
					XMax = xmax,
					Label = label,
					Score = scores[i, j],
				};

				results.Add(boxOutline);
			}
		}
		    
		return results;
	}
	
	

}
