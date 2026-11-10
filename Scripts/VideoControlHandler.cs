
using ParelVR.Volt;
using ParelVR.Volt.Networking;
using ParelVR.Volt.Objects;
using ParelVR.Volt.Players;
using ParelVR.Volt.UI;
using ParelVR.Volt.Worlds;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/UI/Video Control Handler")]
    public class VideoControlHandler : VoltBehaviour
    {
        
        public VoltVideoController targetVideoPlayer;

#pragma warning disable CS0649
        [SerializeField, VoltEditorOnly]
        private Object urlField;

        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Text urlFieldPlaceholderText;

        [Header("Status text")]
        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Text statusTextField;

        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Text statusTextDropShadow;

        [Header("Video progress bar")]
        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Slider progressSlider;

        [Header("Lock button")]
        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Graphic lockGraphic;

        [SerializeField, VoltEditorOnly]
        private GameObject masterLockedIcon, masterUnlockedIcon;

        [Header("Info panel fields")]
        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Text masterField;

        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Text ownerField;

        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.InputField currentURLField, previousURLField;

        [Header("Play/Pause/Stop buttons")]
        [SerializeField, VoltEditorOnly]
        private GameObject pauseStopObject;

        [SerializeField, VoltEditorOnly]
        private GameObject playObject;

        [SerializeField, VoltEditorOnly]
        private GameObject pauseIcon, stopIcon;

        [Header("Loop button")]
        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Graphic loopButtonBackground;

        [SerializeField, VoltEditorOnly]
        private UnityEngine.UI.Graphic loopButtonIcon;

        [Header("Video/Stream controls")]
        [SerializeField]
        private SyncModeController syncController;

        [Header("Volume")]
        [SerializeField]
        private VolumeController volumeController;

        [Header("Style Colors")]
        [Tooltip("Overrides the scene's button and icon colors at runtime. Leave off to keep the colors you designed.")]
        public bool applyStyleColors = false;
        public Color redGraphicColor = new Color(0.632f, 0.19f, 0.19f);
        public Color whiteGraphicColor = new Color(0.9433f, 0.9433f, 0.9433f);
        public Color buttonBackgroundColor = new Color(1f, 1f, 1f, 1f);
        public Color buttonActivatedColor = new Color(1f, 1f, 1f, 1f);
        public Color iconInvertedColor = new Color(1f, 1f, 1f, 1f);
#pragma warning restore CS0649

        private VoltInputField _urlField;
        private VoltText _urlFieldPlaceholderText;
        private VoltText _statusTextField;
        private VoltText _statusTextDropShadow;
        private VoltSlider _progressSlider;
        private VoltImage _lockGraphic;
        private VoltGameObject _masterLockedIcon;
        private VoltGameObject _masterUnlockedIcon;
        private VoltText _masterField;
        private VoltText _ownerField;
        private VoltInputField _currentURLField;
        private VoltInputField _previousURLField;
        private VoltGameObject _pauseStopObject;
        private VoltGameObject _playObject;
        private VoltGameObject _pauseIcon;
        private VoltGameObject _stopIcon;
        private VoltImage _loopButtonBackground;
        private VoltImage _loopButtonIcon;
        private VoltGameObject _loopIconOff;
        private VoltGameObject _loopIconOn;

        private const string STREAM_STATUS = "ACTIVE STREAM SYNC";

        private bool _bound;
        private int _lastTime = int.MaxValue;
        private string _currentStatusText = "";
        private bool _statusIsMessage;
        private bool _draggingSlider;
        private bool _settingSliderValue;
        private string _lastURLFieldText = "";
        private bool _loneHClearPending;

        public override void OnEnabled()
        {
            BindUI();

            VoltVideoController target = GetTarget();
            if (target != null)
                target.RegisterControlHandler(this);

            UpdateMaster();
            UpdateVideoOwner();

            if (volumeController != null)
                volumeController.SetControlHandler(this);
        }

        public override void OnDisabled()
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.UnregisterControlHandler(this);
        }

        public override void OnUpdate()
        {
            UpdateURLFieldGuard();
            RunUIUpdate();
        }

        private void UpdateURLFieldGuard()
        {
            if (_urlField == null)
                return;

            string text = _urlField.Text;
            if (text == null)
                text = "";

            if (text == _lastURLFieldText)
                return;

            _lastURLFieldText = text;

            if ((text == "h" || text == "H") && !_loneHClearPending)
            {
                _loneHClearPending = true;
                SendCustomEventDelayedSeconds(nameof(_ClearLoneH), 0.45f);
            }
        }

        public void _ClearLoneH()
        {
            _loneHClearPending = false;

            if (_urlField == null)
                return;

            string text = _urlField.Text;

            if (text == "h" || text == "H")
            {
                _urlField.Text = "";
                _lastURLFieldText = "";
            }
        }

        public override void OnPlayerLeft(VoltPlayer player)
        {
            UpdateMaster();
        }

        public override bool OnOwnershipRequest(VoltPlayer requester, VoltPlayer newOwner)
        {
            return false;
        }

        public void OnVideoPlayerOwnerTransferred()
        {
            UpdateVideoOwner();
        }

        private VoltVideoController GetTarget()
        {
            if (targetVideoPlayer != null)
                return targetVideoPlayer;

            targetVideoPlayer = gameObject.GetComponentInParent<VoltVideoController>();
            return targetVideoPlayer;
        }

        private void BindUI()
        {
            if (_bound)
                return;

            _bound = true;

            _urlField = InputAt("InputField");
            _urlFieldPlaceholderText = TextAt("InputField/TextMask/Placeholder");
            _statusTextField = TextAt("Progress/StatusText");
            _statusTextDropShadow = TextAt("Progress/DropShadow");
            _progressSlider = SliderAt("Progress/Slider");
            _lockGraphic = ImageAt("InputField/MasterLockButton/LockIcon/Icon");
            _masterLockedIcon = ObjectAt("InputField/MasterLockButton/LockIcon");
            _masterUnlockedIcon = ObjectAt("InputField/MasterLockButton/UnlockIcon");
            _masterField = TextAt("InfoCanvas/InfoFields/MasterReadout/MasterName");
            _ownerField = TextAt("InfoCanvas/InfoFields/OwnerReadout/OwnerName");
            _currentURLField = InputAt("InfoCanvas/InfoFields/CurrentVideo/URL");
            _previousURLField = InputAt("InfoCanvas/InfoFields/LastVideo/URL");
            _pauseStopObject = ObjectAt("Progress/PlayButton/PauseStopIcon");
            _playObject = ObjectAt("Progress/PlayButton/PlayIcon");
            _pauseIcon = ObjectAt("Progress/PlayButton/PauseStopIcon/PauseIcon");
            _stopIcon = ObjectAt("Progress/PlayButton/PauseStopIcon/StopIcon");
            _loopButtonBackground = ImageAt("LoopButton");
            _loopButtonIcon = ImageAt("LoopButton/LoopIcon/Icon");
            _loopIconOff = ObjectAt("LoopButton/LoopIcon/Icon");
            _loopIconOn = ObjectAt("LoopButton/LoopIcon/Icon (ON)");

            if (syncController == null)
            {
                VoltTransform syncTransform = transform.Find("SyncModeToggle");
                if (syncTransform != null)
                    syncController = syncTransform.GetComponent<SyncModeController>();
            }

            if (volumeController == null)
            {
                VoltTransform volumeTransform = transform.Find("Volume");
                if (volumeTransform != null)
                    volumeController = volumeTransform.GetComponent<VolumeController>();
            }
        }

        private VoltTransform Child(string path)
        {
            return transform.Find(path);
        }

        private VoltText TextAt(string path)
        {
            VoltTransform t = Child(path);
            return t != null ? t.GetComponent<VoltText>() : null;
        }

        private VoltInputField InputAt(string path)
        {
            VoltTransform t = Child(path);
            return t != null ? t.GetComponent<VoltInputField>() : null;
        }

        private VoltSlider SliderAt(string path)
        {
            VoltTransform t = Child(path);
            return t != null ? t.GetComponent<VoltSlider>() : null;
        }

        private VoltImage ImageAt(string path)
        {
            VoltTransform t = Child(path);
            return t != null ? t.GetComponent<VoltImage>() : null;
        }

        private VoltGameObject ObjectAt(string path)
        {
            VoltTransform t = Child(path);
            return t != null ? t.GameObject : null;
        }

        private void UpdateMaster()
        {
            if (_masterField == null)
                return;

            VoltPlayer master = VoltNetworking.Master;
            if (master != null)
                _masterField.Text = master.DisplayName;
        }

        private void UpdateVideoOwner()
        {
            VoltVideoController target = GetTarget();

            if (_ownerField != null && target != null)
            {
                VoltPlayer owner = VoltNetworking.GetOwner(target.gameObject);
                if (owner != null)
                    _ownerField.Text = owner.DisplayName;
            }

            if (target != null)
                SetLocked(target.IsLocked());
        }

        
        public void SetControlledVideoPlayer(VoltVideoController newPlayer)
        {
            if (newPlayer == targetVideoPlayer)
                return;

            VoltVideoController old = GetTarget();
            if (old != null)
                old.UnregisterControlHandler(this);

            targetVideoPlayer = newPlayer;

            if (targetVideoPlayer != null)
                targetVideoPlayer.RegisterControlHandler(this);

            UpdateVideoOwner();

            SetStatusText("");
            _draggingSlider = false;
        }

        public void SetStatusText(string newStatus)
        {
            _statusIsMessage = !string.IsNullOrEmpty(newStatus);
            SetTimeStatus(newStatus);
            _lastTime = int.MaxValue;
        }

        private void SetTimeStatus(string newStatus)
        {
            _currentStatusText = newStatus;
            if (_statusTextField != null) _statusTextField.Text = _currentStatusText;
            if (_statusTextDropShadow != null) _statusTextDropShadow.Text = _currentStatusText;
        }

        public void _ClearStatusText()
        {
            SetStatusText("");
        }

        public void SetLocked(bool locked)
        {
            VoltVideoController target = GetTarget();

            if (_urlField != null && target != null)
                _urlField.Interactable = target.CanControlVideoPlayer();

            if (locked)
            {
                if (_masterLockedIcon != null) _masterLockedIcon.SetActive(true);
                if (_masterUnlockedIcon != null) _masterUnlockedIcon.SetActive(false);

                if (target != null && (target.IsOwner || target.CanControlVideoPlayer()))
                {
                    if (applyStyleColors && _lockGraphic != null) _lockGraphic.Color = whiteGraphicColor;
                    if (_urlFieldPlaceholderText != null) _urlFieldPlaceholderText.Text = "Enter Video URL...";
                }
                else
                {
                    if (applyStyleColors && _lockGraphic != null) _lockGraphic.Color = redGraphicColor;

                    if (_urlFieldPlaceholderText != null && target != null)
                    {
                        VoltPlayer owner = VoltNetworking.GetOwner(target.gameObject);
                        string ownerName = owner != null ? owner.DisplayName : "master";
                        _urlFieldPlaceholderText.Text = "Only the master " + ownerName + " may add URLs";
                    }
                }
            }
            else
            {
                if (_masterLockedIcon != null) _masterLockedIcon.SetActive(false);
                if (_masterUnlockedIcon != null) _masterUnlockedIcon.SetActive(true);
                if (applyStyleColors && _lockGraphic != null) _lockGraphic.Color = whiteGraphicColor;
                if (_urlFieldPlaceholderText != null) _urlFieldPlaceholderText.Text = "Enter Video URL... (anyone)";
            }
        }

        public void SetPaused(bool paused)
        {
            if (_pauseIcon != null) _pauseIcon.SetActive(true);
            if (_stopIcon != null) _stopIcon.SetActive(false);

            if (_playObject != null) _playObject.SetActive(paused);
            if (_pauseStopObject != null) _pauseStopObject.SetActive(!paused);
        }

        public void SetLooping(bool looping)
        {
            if (_loopIconOn != null) _loopIconOn.SetActive(looping);
            if (_loopIconOff != null) _loopIconOff.SetActive(!looping);

            if (!applyStyleColors)
                return;

            if (looping)
            {
                if (_loopButtonBackground != null) _loopButtonBackground.Color = buttonActivatedColor;
                if (_loopButtonIcon != null) _loopButtonIcon.Color = iconInvertedColor;
            }
            else
            {
                if (_loopButtonBackground != null) _loopButtonBackground.Color = buttonBackgroundColor;
                if (_loopButtonIcon != null) _loopButtonIcon.Color = whiteGraphicColor;
            }
        }

        public void SetVolume(float volume)
        {
            if (volumeController != null)
                volumeController.SetVolume(volume);
        }

        public void SetMuted(bool muted)
        {
            if (volumeController != null)
                volumeController.SetMuted(muted);
        }

        public void OnVolumeSliderChange(float volume)
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.SetVolume(volume);
        }

        public void OnMutePress(bool muted)
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.SetMuted(muted);
        }

        public void AddURLToHistory(VRCUrl url)
        {
            if (_currentURLField == null || url == null)
                return;

            if (_previousURLField != null)
                _previousURLField.Text = _currentURLField.Text;

            _currentURLField.Text = url.Get();
        }

        public void SetToVideoPlayerMode()
        {
            if (syncController != null)
                syncController.SetVideoVisual();
        }

        public void SetToStreamPlayerMode()
        {
            if (syncController != null)
                syncController.SetStreamVisual();
        }

        private void RunUIUpdate()
        {
            VoltVideoController target = GetTarget();
            if (target == null || !target.IsInVideoMode())
            {
                if (_progressSlider != null)
                    _progressSlider.GameObject.SetActive(false);

                if (target != null && target.IsInStreamMode())
                {
                    if (!_statusIsMessage && _currentStatusText != STREAM_STATUS)
                        SetStatusText(STREAM_STATUS);
                }
                else if (_currentStatusText == STREAM_STATUS)
                {
                    SetStatusText("");
                }
                else if (!_statusIsMessage)
                {
                    SetStatusText("");
                }

                return;
            }

            if (_currentStatusText == STREAM_STATUS)
                SetStatusText("");

            float duration = target.GetPlaybackDuration();

            if (_draggingSlider && _progressSlider != null)
            {
                float currentProgress = _progressSlider.Value;
                float currentTime = duration * currentProgress;

                target.SeekTo(currentProgress);

                SetTimeStatus(GetFormattedTime(currentTime));
            }
            else
            {
                float currentTime = target.GetPlaybackTime();

                if (_progressSlider != null)
                {
                    if (duration > 0f)
                    {
                        _progressSlider.GameObject.SetActive(true);
                        _settingSliderValue = true;
                        _progressSlider.Value = Mathf.Clamp01(currentTime / duration);
                        _settingSliderValue = false;
                    }
                    else
                    {
                        _progressSlider.GameObject.SetActive(false);
                    }
                }

                int currentTimeInt = Mathf.RoundToInt(currentTime);
                if (currentTimeInt != _lastTime)
                {
                    _lastTime = currentTimeInt;

                    if (duration > 0f && !_statusIsMessage)
                        SetTimeStatus(GetFormattedTime(currentTime) + "/" + GetFormattedTime(duration));
                }
            }
        }

        private string GetFormattedTime(float seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;

            return Pad2(hours) + ":" + Pad2(minutes) + ":" + Pad2(secs);
        }

        private string Pad2(int value)
        {
            return value < 10 ? "0" + value : "" + value;
        }

        public void OnURLInput()
        {
            VoltVideoController target = GetTarget();
            if (target == null || _urlField == null)
                return;

            string text = _urlField.Text;
            if (text != null)
                text = text.Trim();

            if (string.IsNullOrEmpty(text))
                return;

            if (text.Length <= 2 && text.IndexOf(".") < 0)
            {
                _urlField.Text = "";
                _lastURLFieldText = "";
                SetStatusText("Enter a full video URL");
                SendCustomEventDelayedSeconds(nameof(_ClearStatusText), 2f);
                return;
            }

            if (!target.CanControlVideoPlayer())
            {
                SetStatusText("Only the master can change the video URL");
                return;
            }

            target.PlayVideo(new VRCUrl(text));
            _urlField.Text = "";
            _lastURLFieldText = "";
        }

        public void OnPlayButtonPress()
        {
            VoltVideoController target = GetTarget();
            if (target == null)
                return;

            // With a link typed in the box, Play means "play this link".
            if (_urlField != null)
            {
                string text = _urlField.Text;
                if (text != null && text.Trim().Length > 0)
                {
                    OnURLInput();
                    return;
                }
            }

            target.TakeOwnership();
            target.SetPaused(!target.IsPaused());
        }

        public void OnLockButtonPress()
        {
            VoltVideoController target = GetTarget();
            if (target == null)
                return;

            if (target.CanControlVideoPlayer() || target.IsOwner)
            {
                target.TakeOwnership();
                target.SetLocked(!target.IsLocked());
            }
            else
            {
                SetStatusText("Only the master can unlock the player");
            }
        }

        public void OnReloadButtonPressed()
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.Reload();
        }

        public void OnLoopButtonPressed()
        {
            VoltVideoController target = GetTarget();
            if (target == null)
                return;

            target.TakeOwnership();
            target.SetLooping(!target.IsLooping());
        }

        public void OnVideoPlayerModeButtonPressed()
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.SetToUnityPlayer();
        }

        public void OnStreamPlayerModeButtonPressed()
        {
            VoltVideoController target = GetTarget();
            if (target != null)
                target.SetToAVProPlayer();
        }

        public void OnSeekSliderChanged()
        {
            if (_settingSliderValue || _progressSlider == null)
                return;

            VoltVideoController target = GetTarget();
            if (target != null)
            {
                target.TakeOwnership();
                target.SeekTo(_progressSlider.Value);
            }
        }

        public void OnSeekSliderBeginDrag()
        {
            VoltVideoController target = GetTarget();
            if (target == null)
                return;

            target.TakeOwnership();
            if (target.IsOwner)
                _draggingSlider = true;
        }

        public void OnSeekSliderEndDrag()
        {
            _draggingSlider = false;
        }
    }
}
