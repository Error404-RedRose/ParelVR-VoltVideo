using UnityEditor;
using UnityEngine;

namespace VoltVideo.Internal
{
    [CustomEditor(typeof(VoltVideoController))]
    internal class VoltVideoControllerInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Video playback uses ParelVR's VoltVideoPlayer. Add a VoltVideoPlayer component to the VideoScreen object, assign its Screen renderer and Audio Output, then enable the Video capability and allow the video host in ParelVR SDK > Volt > Settings.", MessageType.Info);
        }
    }
}
