using ParelVR.Volt;
using UnityEngine;

namespace VoltVideo.Examples
{
    [AddComponentMenu("Volt Video/Examples/Reload Button")]
    public class ReloadButton : VoltBehaviour
    {
        public VoltVideoController targetVideoPlayer;

        public void OnResyncButtonPress()
        {
            if (targetVideoPlayer != null)
                targetVideoPlayer.Reload();
            else
                VoltDebug.LogError("Resync button does not have a valid target video player!");
        }
    }
}
