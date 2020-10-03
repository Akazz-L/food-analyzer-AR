#!/bin/bash


# Download TensorFlowSharp plugin from Unity-ML-agents repo
# See README : https://github.com/miyamotok0105/unity-ml-agents/blob/master/docs/Using-TensorFlow-Sharp-in-Unity.md
# This package must be manually imported in Unity.
curl -O https://s3.amazonaws.com/unity-ml-agents/0.3/TFSharpPlugin.unitypackage
mv TFSharpPlugin.unitypackage Packages/

