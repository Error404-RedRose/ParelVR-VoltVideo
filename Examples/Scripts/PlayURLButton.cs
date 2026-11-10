using ParelVR.Volt;
using ParelVR.Volt.UI;
using UnityEngine;

namespace VoltVideo.Examples
{
    [AddComponentMenu("Volt Video/Examples/Play URL Button")]
    public class PlayURLButton : VoltBehaviour
    {
        public VoltVideoController targetVideoPlayer;
        public VRCUrl url = new VRCUrl("");

        private VoltButton _button;

        public override void OnStart()
        {
            _button = GetComponentInChildren<VoltButton>();
            UpdateOwnership();

            if (targetVideoPlayer != null)
                targetVideoPlayer.RegisterCallbackReceiver(this);
        }

        public void OnButtonPress()
        {
            if (targetVideoPlayer != null)
                targetVideoPlayer.PlayVideo(url);
        }

        public void OnVoltVideoLockChange()
        {
            UpdateOwnership();
        }

        public void OnVoltVideoOwnershipChange()
        {
            UpdateOwnership();
        }

        private void UpdateOwnership()
        {
            if (_button != null && targetVideoPlayer != null)
                _button.Interactable = targetVideoPlayer.CanControlVideoPlayer();
        }
    }
}
