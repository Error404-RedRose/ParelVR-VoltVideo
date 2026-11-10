using ParelVR.Volt;
using ParelVR.Volt.Animation;
using ParelVR.Volt.Objects;
using ParelVR.Volt.UI;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/UI/Sync Mode Controller")]
    public class SyncModeController : VoltBehaviour
    {
        public VideoControlHandler videoPlayerControls;

        [VoltEditorOnly]
        public RectTransform sliderTransform;

        private VoltAnimator _animator;
        private VoltText _sliderText;
        private VoltGameObject _videoDisabledText;
        private VoltGameObject _streamDisabledText;
        private int _mode;

        public override void OnStart()
        {
            _animator = GetComponent<VoltAnimator>();

            VoltTransform textTransform = transform.Find("ModeSlider/Text");
            if (textTransform != null)
                _sliderText = textTransform.GetComponent<VoltText>();

            VoltTransform videoText = transform.Find("VideoDisabledText");
            if (videoText != null)
                _videoDisabledText = videoText.GameObject;

            VoltTransform streamText = transform.Find("StreamDisabledText");
            if (streamText != null)
                _streamDisabledText = streamText.GameObject;

            ApplyModeVisibility();
        }

        private void ApplyModeVisibility()
        {
            if (_videoDisabledText != null)
                _videoDisabledText.SetActive(_mode == 1);

            if (_streamDisabledText != null)
                _streamDisabledText.SetActive(_mode == 0);
        }

        private VideoControlHandler GetControls()
        {
            if (videoPlayerControls != null)
                return videoPlayerControls;

            videoPlayerControls = gameObject.GetComponentInParent<VideoControlHandler>();
            return videoPlayerControls;
        }

        public void SetVideoVisual()
        {
            _mode = 0;

            if (_animator != null)
                _animator.SetInteger("Target", 0);

            if (_sliderText != null)
                _sliderText.Text = "Video";

            ApplyModeVisibility();
        }

        public void SetStreamVisual()
        {
            _mode = 1;

            if (_animator != null)
                _animator.SetInteger("Target", 1);

            if (_sliderText != null)
                _sliderText.Text = "Live";

            ApplyModeVisibility();
        }

        public void ClickVideoToggle()
        {
            VideoControlHandler controls = GetControls();
            if (controls != null)
                controls.OnVideoPlayerModeButtonPressed();
        }

        public void ClickStreamToggle()
        {
            VideoControlHandler controls = GetControls();
            if (controls != null)
                controls.OnStreamPlayerModeButtonPressed();
        }
    }
}
