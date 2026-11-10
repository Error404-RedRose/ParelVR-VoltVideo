using ParelVR.Volt;
using ParelVR.Volt.Objects;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/Utilities/Video Screen Handler")]
    public class VideoScreenHandler : VoltBehaviour
    {
        [SerializeField]
        private VoltVideoController sourceVideoPlayer;

        [Header("Renderer")]
        [Tooltip("Name of parameter the shader on this renderer uses for the video texture")]
        public string texParam = "_MainTex";

        [Tooltip("Name of the parameter the shader on this renderer uses to determine if it should preform color and UV correction on the render texture")]
        public string avProToggleParam = "_IsAVProInput";

        [Tooltip("Sets render textures on the renderer's shared material which allows multiple screens to share the same texture and batch")]
        public bool useSharedMaterial = false;

        [Tooltip("The index of the renderer to set the video texture on")]
        public int rendererIndex = 0;

        [Tooltip("Texture that will be shown on the screen when the video player is not playing a video")]
        [VoltEditorOnly]
        public Texture standbyTexture;

        private VoltRenderer targetRenderer;
        private object lastRenderTexture;

        public override void OnStart()
        {
            targetRenderer = GetComponent<VoltRenderer>();
            SetSourceVideoPlayer(sourceVideoPlayer);
        }

        public override void OnEnabled()
        {
            SetSourceVideoPlayer(sourceVideoPlayer);
        }

        public override void OnDisabled()
        {
            if (sourceVideoPlayer != null)
                sourceVideoPlayer.UnregisterScreenHandler(this);
        }

        public object GetVideoTexture()
        {
            return lastRenderTexture;
        }

        public void UpdateVideoTexture(object renderTexture, bool isAVPro)
        {
            lastRenderTexture = renderTexture;
        }

        public void SetToStandby()
        {
            lastRenderTexture = null;
        }

        public void SetSourceVideoPlayer(VoltVideoController sourcePlayer)
        {
            if (sourcePlayer == null)
                return;

            if (sourceVideoPlayer != null)
                sourceVideoPlayer.UnregisterScreenHandler(this);

            sourceVideoPlayer = sourcePlayer;
            sourceVideoPlayer.RegisterScreenHandler(this);
        }
    }
}
