using ParelVR.Volt;
using ParelVR.Volt.UI;
using UnityEngine;

namespace VoltVideo.Examples
{
    [AddComponentMenu("Volt Video/Examples/Sync Delay Manager")]
    public class SyncDelayManager : VoltBehaviour
    {
        public VoltVideoController videoPlayer;

        public float maxDelay = 2f;

        [VoltEditorOnly]
        public UnityEngine.UI.Slider delaySlider;
        [VoltEditorOnly]
        public UnityEngine.UI.Text delayReadout;

        private VoltSlider _delaySlider;
        private VoltText _delayReadout;

        public override void OnStart()
        {
            _delaySlider = gameObject.GetComponentInChildren<VoltSlider>();
            _delayReadout = gameObject.GetComponentInChildren<VoltText>();

            if (videoPlayer == null)
                VoltDebug.LogError("SyncDelayManager must have a video player reference!");
        }

        public void OnSliderChange()
        {
            if (_delaySlider == null)
                return;

            float sliderVal = _delaySlider.Value;
            float timeValue = sliderVal * maxDelay;

            if (_delayReadout != null)
                _delayReadout.Text = ((int)(timeValue * 1000f)) + " ms";

            if (videoPlayer != null)
            {
                videoPlayer.localSyncOffset = -timeValue;
                videoPlayer.ForceSyncVideo();
            }
        }

        public void OnResyncPress()
        {
            if (videoPlayer != null)
                videoPlayer.ForceSyncVideo();
        }
    }
}
