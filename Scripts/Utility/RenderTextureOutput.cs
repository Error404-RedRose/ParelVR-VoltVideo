using ParelVR.Volt;
using ParelVR.Volt.Objects;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/Utilities/Render Texture Output")]
    public class RenderTextureOutput : VoltBehaviour
    {
#pragma warning disable CS0649
        [SerializeField]
        private VoltVideoController sourceVideoPlayer;

        private VideoPlayerManager videoPlayerManager;
#pragma warning restore CS0649

        [VoltEditorOnly]
        public CustomRenderTexture outputTexture;

#if UNITY_EDITOR
        [CustomEditor(typeof(RenderTextureOutput))]
        internal class RenderTextureOutputInspector : Editor
        {
            private SerializedProperty sourceVideoPlayerProperty;
            private SerializedProperty outputTextureProperty;

            private void OnEnable()
            {
                sourceVideoPlayerProperty = serializedObject.FindProperty("sourceVideoPlayer");
                outputTextureProperty = serializedObject.FindProperty("outputTexture");
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.PropertyField(sourceVideoPlayerProperty);
                EditorGUILayout.PropertyField(outputTextureProperty);
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif

        public override void OnStart()
        {
            if (sourceVideoPlayer != null)
                videoPlayerManager = sourceVideoPlayer.gameObject.GetComponentInChildren<VideoPlayerManager>();
        }

        public override void OnLateUpdate()
        {
        }
    }
}
