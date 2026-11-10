using ParelVR.Volt;
using UnityEngine;

namespace VoltVideo.Examples
{
    [AddComponentMenu("Volt Video/Examples/Resync Button")]
    public class ResyncButton : VoltBehaviour
    {
        public VoltVideoController videoPlayer;

        public void OnResyncPressed()
        {
            if (videoPlayer != null)
                videoPlayer.ForceSyncVideo();
        }
    }
}
