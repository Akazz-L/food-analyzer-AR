#!/bin/bash


# Download TensorFlowSharp plugin from Unity-ML-agents repo
# See README : https://github.com/miyamotok0105/unity-ml-agents/blob/master/docs/Using-TensorFlow-Sharp-in-Unity.md
# This package must be manually imported in Unity. Please follow the above README to properly install the package.
curl -O https://s3.amazonaws.com/unity-ml-agents/0.3/TFSharpPlugin.unitypackage
mv TFSharpPlugin.unitypackage Packages/


# Download Google ARCore from Google-AR repo
# See README : https://developers.google.com/ar/develop/unity/quickstart-android
# Google ARCore package is already included in the Food Analyzer repo but Unity Editor might need to be configured to enable AR features if you have never used it before. In that case please follow Google's guide and enable AR features in Unity.

# curl -O https://github.com/google-ar/arcore-unity-sdk/releases/download/v1.19.0/arcore-unity-sdk-1.19.0.unitypackage
# mv arcore-unity-sdk-1.19.0.unitypackage Packages/


