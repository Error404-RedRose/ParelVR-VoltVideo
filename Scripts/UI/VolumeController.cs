using ParelVR.Volt;
using ParelVR.Volt.Objects;
using ParelVR.Volt.UI;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/UI/Volume Controller")]
    public class VolumeController : VoltBehaviour
    {
        private VideoControlHandler controlHandler;

        [VoltEditorOnly]
        public UnityEngine.UI.Slider slider;

        [VoltEditorOnly]
        public GameObject muteIcon;
        [VoltEditorOnly]
        public GameObject zeroVolumeIcon;
        [VoltEditorOnly]
        public GameObject lowVolumeIcon;
        [VoltEditorOnly]
        public GameObject HighVolumeIcon;

        private bool _muted;

        private VoltSlider _slider;
        private VoltGameObject _muteIcon;
        private VoltGameObject _zeroVolumeIcon;
        private VoltGameObject _lowVolumeIcon;
        private VoltGameObject _highVolumeIcon;

        private bool _sliderValueChanging;
        private bool _bound;

        public override void OnStart()
        {
            BindUI();
            UpdateVolumeIcon();
        }

        private void BindUI()
        {
            if (_bound)
                return;

            _bound = true;

            VoltTransform sliderTransform = transform.Find("Slider");
            if (sliderTransform != null)
                _slider = sliderTransform.GetComponent<VoltSlider>();

            _muteIcon = ObjectAt("MuteButton/MuteIcon");
            _zeroVolumeIcon = ObjectAt("MuteButton/ZeroVolumeIcon");
            _lowVolumeIcon = ObjectAt("MuteButton/LowVolumeIcon");
            _highVolumeIcon = ObjectAt("MuteButton/HighVolumeIcon");
        }

        private VoltGameObject ObjectAt(string path)
        {
            VoltTransform t = transform.Find(path);
            return t != null ? t.GameObject : null;
        }

        public void SetControlHandler(VideoControlHandler handler)
        {
            controlHandler = handler;
        }

        private VideoControlHandler GetControlHandler()
        {
            if (controlHandler != null)
                return controlHandler;

            controlHandler = gameObject.GetComponentInParent<VideoControlHandler>();
            return controlHandler;
        }

        public void SetMuted(bool muted)
        {
            if (muted != _muted)
            {
                _muted = muted;
                UpdateVolumeIcon();
            }
        }

        public void SetVolume(float volume)
        {
            if (!_sliderValueChanging && _slider != null)
            {
                _slider.SetValueWithoutNotify(volume);
                UpdateVolumeIcon();
            }
        }

        public void OnSliderValueChanged()
        {
            if (_slider == null)
                return;

            _sliderValueChanging = true;

            VideoControlHandler handler = GetControlHandler();
            if (handler != null)
                handler.OnVolumeSliderChange(_slider.Value);

            _sliderValueChanging = false;

            UpdateVolumeIcon();
        }

        public void OnMutePressed()
        {
            VideoControlHandler handler = GetControlHandler();
            if (handler != null)
                handler.OnMutePress(!_muted);
        }

        private void UpdateVolumeIcon()
        {
            if (_slider == null)
                return;

            if (_muted)
            {
                SetActive(_muteIcon, true);
                SetActive(_zeroVolumeIcon, false);
                SetActive(_lowVolumeIcon, false);
                SetActive(_highVolumeIcon, false);
            }
            else if (_slider.Value > 0.5f)
            {
                SetActive(_muteIcon, false);
                SetActive(_zeroVolumeIcon, false);
                SetActive(_lowVolumeIcon, false);
                SetActive(_highVolumeIcon, true);
            }
            else if (_slider.Value > 0f)
            {
                SetActive(_muteIcon, false);
                SetActive(_zeroVolumeIcon, false);
                SetActive(_lowVolumeIcon, true);
                SetActive(_highVolumeIcon, false);
            }
            else
            {
                SetActive(_muteIcon, false);
                SetActive(_zeroVolumeIcon, true);
                SetActive(_lowVolumeIcon, false);
                SetActive(_highVolumeIcon, false);
            }
        }

        private void SetActive(VoltGameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
