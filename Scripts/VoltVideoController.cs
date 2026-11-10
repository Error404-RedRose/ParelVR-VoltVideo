// VoltVideo
// Converted from and heavily modified from the USharpVideo project.
//
// USharpVideo is distributed under the MIT License. The MIT License permits
// use, copying, modification, merger, publication, distribution, sublicensing,
// and/or sale, provided the original copyright and permission notice are
// retained. The original USharpVideo project and its authorship remain
// attributed to its respective authors.
//
// Approximately 90% of the backend has been remodeled and recoded by
// Twilight Studio for ParelVR. This version is built around ParelVR Volt APIs
// and will not work inside VRChat.
//
// Original project: https://github.com/MerlinVR/USharpVideo
// See LICENSE.txt and LICENSE-USharpVideo.txt in this asset folder for the license notices.
#define USE_SERVER_TIME_MS


using ParelVR.Volt;
using ParelVR.Volt.Instance;
using ParelVR.Volt.Interaction;
using ParelVR.Volt.Networking;
using ParelVR.Volt.Objects;
using ParelVR.Volt.Players;
using ParelVR.Volt.UI;
using ParelVR.Volt.Video;
using ParelVR.Volt.Web;
using ParelVR.Volt.Worlds;
using VoltVideo.Internal;
using UnityEngine;

namespace VoltVideo
{
    public class VRCUrl
    {
        public string url = "";

        public VRCUrl()
        {
        }

        public VRCUrl(string value)
        {
            url = value;
        }

        public string Get()
        {
            return url;
        }

        public static VRCUrl Empty
        {
            get { return new VRCUrl(""); }
        }

    }

    public class VRCPlayerApi
    {
        public bool isMaster;
        public bool isInstanceOwner;
        public bool isLocal;
        public string displayName = "";

        public VRCPlayerApi()
        {
        }

        public VRCPlayerApi(VoltPlayer player)
        {
            if (player == null) return;
            isMaster = player.IsMaster;
            isLocal = player.IsLocal;
            displayName = player.DisplayName;
        }

        public bool IsValid()
        {
            return true;
        }
    }

    public enum VideoError
    {
        None,
        InvalidURL,
        AccessDenied,
        RateLimited,
        PlayerError,
        Unknown
    }

    [AddComponentMenu("Volt Video/Volt Video Controller")]
    [VoltSync(VoltSyncMode.Manual)]
    public class VoltVideoController : VoltBehaviour
    {
        private VideoPlayerManager _videoPlayerManager;

        [Tooltip("Whether to allow video seeking with the progress bar on the video")]
        
        public bool allowSeeking = true;

        [Tooltip("If enabled defaults to unlocked so anyone can put in a URL")]
        [SerializeField]
        private bool defaultUnlocked = true;

        [Tooltip("If enabled allows the instance creator to always control the video player regardless of if they are master or not")]
        
        public bool allowInstanceCreatorControl = true;

        [Tooltip("How often the video player should check if it is more than Sync Threshold out of sync with the video time")]
        
        public float syncFrequency = 2.0f;

        [Tooltip("How many seconds desynced from the owner the client needs to be to trigger a resync")]
        
        public float syncThreshold = 0.35f;

        [Header("Adaptive playback")]
        [Tooltip("Tolerates a little more drift on slow connections while still forcing a catch-up when badly out of sync")]
        public bool adaptiveSync = true;

        [Tooltip("Ping in milliseconds above which a client gets the medium sync tolerance")]
        public int mediumBandwidthPing = 120;

        [Tooltip("Ping in milliseconds above which a client gets the low sync tolerance")]
        public int lowBandwidthPing = 220;

        [Range(0f, 1f)]
        [Tooltip("The default volume for the volume slider on the video player")]
        [SerializeField]
        private float defaultVolume = 0.5f;

#pragma warning disable CS0414
        [Tooltip("The max range of the audio sources on this video player")]
        [SerializeField]
        private float audioRange = 40f;
#pragma warning restore CS0414

        [System.NonSerialized]
        public float localSyncOffset;

        [Tooltip("URL to load when the world starts. Uses the same path as typing it into the URL box.")]
        [SerializeField]
        private string startUrl = "";

        [Tooltip("Requests the highest quality for YouTube video links; the original URL is retried if the hint fails.")]
        [SerializeField]
        private bool requestHighQualityVideo = true;

        [Tooltip("List of urls to play automatically when the world is loaded until someone puts in another URL")]
        public VRCUrl[] playlist = new VRCUrl[0];

        [Tooltip("Should default to the stream player? This is usually used when you want to put a live stream in the default playlist.")]
        [SerializeField]
        private bool defaultStreamMode;

        [Tooltip("If the default playlist should loop")]
        
        public bool loopPlaylist;

        [Tooltip("If the default playlist should be shuffled upon world load")]
        public bool shufflePlaylist;

        [VoltSynced]
        private string _syncedURL = "";

        [VoltSynced]
        private bool _syncedLiveStream;

        [VoltSynced]
        private int _syncedVideoIdx;
        private int _currentVideoIdx;

        [VoltSynced]
        private bool _isMasterOnly = false;

        [VoltSynced]
        private bool _controlsHidden = true;

        [VoltSynced]
        private string _syncedErrorText = "";

        [VoltSynced]
        private bool _syncedErrorVisible;

        [VoltSynced]
        private int _nextPlaylistIndex;

        [VoltSynced]
        private int _networkTimeVideoStart;
        private int _localNetworkTimeStart;

        [VoltSynced]
        private bool _ownerPlaying;

        [VoltSynced]
        private bool _ownerPaused;

        [VoltSynced]
        private float _ownerPausedAt;
        private bool _locallyPaused;

        [VoltSynced]
        private bool _loopVideo;
        private bool _localLoopVideo;

        [VoltSynced]
        private int _shuffleSeed;

        private float _lastVideoTime;

        private VideoControlHandler[] _registeredControlHandlers;
        private VideoScreenHandler[] _registeredScreenHandlers;
        private VoltBehaviour[] _registeredCallbackReceivers;

        private const int MAX_RETRY_COUNT = 4;
        private const float DEFAULT_RETRY_TIMEOUT = 35.0f;
        private const float RATE_LIMIT_RETRY_TIMEOUT = 5.5f;
        private const float VIDEO_ERROR_RETRY_TIMEOUT = 5f;
        private const float PLAYLIST_ERROR_RETRY_COUNT = 4;

        private bool _loadingVideo;
        private float _loadStartTime;
        private float _currentLoadingTime;
        private int _currentRetryCount;
        private float _videoTargetStartTime;
        private int _playlistErrorCount;

        private bool _waitForSync;
        private bool _endHandled;

        private bool _pendingVideoLoad;

        private const int PLAYER_MODE_UNITY = 0;
        private const int PLAYER_MODE_AVPRO = 1;

        [VoltSynced]
        private int currentPlayerMode = PLAYER_MODE_UNITY;
        private int _localPlayerMode = PLAYER_MODE_UNITY;

        private bool _videoSync = true;

        private bool _ranInit;

        private VoltVideoPlayer _screen;
        private bool _muted;
        private float _currentVolume = 1f;
        private VoltGameObject _errorCanvas;
        private VoltText _errorText;
        private VoltInteractable _errorInteractable;
        private VoltCollider _errorCollider;
        private string _lastErrorDetail = "";
        private string _lastErrorUrl = "";
        private VoltWebRequest _liveProbe;
        private string _liveProbeUrl = "";
        private bool _liveProbeChecked;

        private int _localQualityLevel;
        private int _qualityAttempts;
        private bool _adaptiveReloading;
        private string _localLoadUrlOverride = "";
        private float _bufferTimer;
        private float _stablePlaybackTime;
        private float _lastBufferResetTime;
        private float _lastQualityChangeTime;
        private int _qualityRecoveryAttempts;

        public override void OnStart()
        {
            if (_ranInit)
                return;

            _ranInit = true;

            // The manager initializes itself in its own OnStart.
            _videoPlayerManager = GetVideoManager();

            _screen = FindScreen();

            if (_screen == null)
                LogError("No VoltVideoPlayer found. Add ParelVR.Volt.Video.VoltVideoPlayer to the VideoScreen object.");

            VoltTransform errorTransform = transform.Find("ErrorCanvas");
            if (errorTransform != null)
            {
                _errorCanvas = errorTransform.GameObject;

                VoltTransform errorTextTransform = errorTransform.Find("ErrorBox/ErrorText");
                if (errorTextTransform != null)
                    _errorText = errorTextTransform.GetComponent<VoltText>();

                _errorInteractable = errorTransform.GameObject.GetComponent<VoltInteractable>();
                _errorCollider = errorTransform.GameObject.GetComponent<VoltCollider>();
            }

            HideErrorOverlay();

            if (_registeredControlHandlers == null)
                _registeredControlHandlers = new VideoControlHandler[0];

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new VoltBehaviour[0];

            if (!IsOwner && Owner == null && IsAuthority())
                TakeOwnership();

            if (IsOwner)
            {
                _isMasterOnly = !defaultUnlocked;

                if (defaultStreamMode)
                {
                    currentPlayerMode = PLAYER_MODE_AVPRO;
                    _localPlayerMode = PLAYER_MODE_AVPRO;
                }

                ObjectToggle initialControls = _screen != null ? _screen.gameObject.GetComponent<ObjectToggle>() : null;
                if (initialControls != null)
                    _controlsHidden = initialControls.StartsHidden();

                _shuffleSeed = UnityEngine.Random.Range(0, 10000);
            }
            else if (!VoltNetworking.IsStateReady)
            {
                _isMasterOnly = !defaultUnlocked;
            }

            SetLocalPlayerMode(currentPlayerMode);
            SetUILocked(_isMasterOnly);
            SetVolume(defaultVolume);

            // Only the owner resets the shared clock; a late joiner may already have received it.
            if (IsOwner || IsAuthority())
            {
                _networkTimeVideoStart = 0;
                _localNetworkTimeStart = 0;

                if (!string.IsNullOrEmpty(startUrl))
                    PlayVideo(new VRCUrl(startUrl));
                else if (IsOwner)
                    PlayNextVideoFromPlaylist();
            }

            // A late joiner can receive the synced state before OnStart; load it now that the screen exists.
            if (_pendingVideoLoad)
            {
                _pendingVideoLoad = false;
                StartVideoLoad(new VRCUrl(_syncedURL));
            }

            QueueSerialize();

            LogMessage("VoltVideo v2.0.0 Initialized");
        }

        public override void OnUpdate()
        {
            if (_screen == null)
            {
                _screen = FindScreen();
                if (_screen == null)
                    return;
            }

            if (_errorCanvas != null && _errorCanvas.ActiveSelf && _screen.IsReady && _screen.IsPlaying)
                HideErrorOverlay();

            if (_pendingVideoLoad)
            {
                _pendingVideoLoad = false;
                StartVideoLoad(new VRCUrl(_syncedURL));
            }

            if (_loadingVideo)
            {
                UpdateVideoLoad();

                if (_loadingVideo && _screen.IsReady && VoltTime.RealTime - _loadStartTime > 0.75f)
                {
                    NotifyVideoReady();
                    return;
                }
            }

            float screenTime = _screen.Time;

            if (!_locallyPaused)
            {
                if (_localLoopVideo && !_loadingVideo && screenTime + 0.5f < _lastCurrentTime &&
                    VoltTime.RealTime - _lastSeekTime > 1.0f)
                {
                    _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(screenTime * 1000f);
                    _localNetworkTimeStart = _networkTimeVideoStart;
                    _endHandled = false;
                    _bufferTimer = 0f;
                    _stablePlaybackTime = 0f;
                    _lastBufferResetTime = VoltTime.RealTime;

                    if (IsOwner)
                        QueueSerialize();
                }

                if (_ownerPlaying && !_loadingVideo && !_adaptiveReloading &&
                    VoltTime.RealTime - _lastBufferResetTime > 3f)
                {
                    if (_screen.IsPlaying && screenTime > _lastCurrentTime + 0.01f)
                    {
                        _bufferTimer = 0f;
                        _stablePlaybackTime += VoltTime.DeltaTime;

                        if (_stablePlaybackTime > 30f)
                        {
                            _stablePlaybackTime = 0f;
                            TryRecoverQuality();
                        }
                    }
                    else if (VoltTime.RealTime - _lastSeekTime > 2f)
                    {
                        _stablePlaybackTime = 0f;
                        _bufferTimer += VoltTime.DeltaTime;

                        if (_bufferTimer > 1.5f)
                        {
                            _bufferTimer = 0f;
                            TryAdaptiveQuality();
                        }
                    }
                }

                _lastCurrentTime = screenTime;
            }

            if (IsOwner)
            {
                if (_ownerPlaying && !_locallyPaused)
                {
                    if (!_endHandled && !_localLoopVideo && _screen.Duration > 0f && !_screen.IsPlaying &&
                        _screen.Time >= _screen.Duration - 0.25f)
                    {
                        OnVideoEnd();
                        return;
                    }

                    SyncVideoIfTime();
                }

                return;
            }

            if (_waitForSync)
            {
                if (_ownerPlaying)
                {
                    _screen.Play();
                    _waitForSync = false;
                    SyncVideo();
                }
                else if (_ownerPaused)
                {
                    _screen.Pause();
                    _waitForSync = false;
                }

                return;
            }

            if (_ownerPlaying || _ownerPaused)
            {
                SyncVideoIfTime();
            }
            else if (_screen.IsPlaying)
            {
                _screen.Pause();
            }
        }

        public override void OnDeserialization()
        {
            if (IsOwner)
                return;

            int previousStart = _localNetworkTimeStart;
            _localNetworkTimeStart = _networkTimeVideoStart;

            SetUILocked(_isMasterOnly);
            SetLoopingInternal(_loopVideo);
            ApplyLocalControlsHidden(_controlsHidden);

            if (_syncedErrorVisible && !string.IsNullOrEmpty(_syncedErrorText))
                ShowErrorOverlayLocal(_syncedErrorText);
            else
                HideErrorOverlayLocal();

            if (_localPlayerMode != currentPlayerMode)
                SetLocalPlayerMode(currentPlayerMode);

            if (_screen == null)
            {
                if (!string.IsNullOrEmpty(_syncedURL))
                    _pendingVideoLoad = true;

                SendCallback("OnVoltVideoDeserialization");
                return;
            }

            if (_currentVideoIdx != _syncedVideoIdx)
            {
                _currentVideoIdx = _syncedVideoIdx;

                _screen.Stop();

                if (!string.IsNullOrEmpty(_syncedURL))
                    StartVideoLoad(new VRCUrl(_syncedURL));
            }
            else if (!_screen.IsReady && !_loadingVideo && !string.IsNullOrEmpty(_syncedURL))
            {
                StartVideoLoad(new VRCUrl(_syncedURL));
            }
            else
            {
                ApplyRemotePlaybackState();

                if (Mathf.Abs((float)(previousStart - _networkTimeVideoStart)) >= 500f)
                    ForceSyncVideo();
            }

            SendCallback("OnVoltVideoDeserialization");
        }

        private void ApplyRemotePlaybackState()
        {
            if (_screen == null || _loadingVideo)
                return;

            if (_ownerPaused)
            {
                _locallyPaused = true;
                _waitForSync = false;

                _screen.Pause();

                if (_ownerPausedAt > 0f && _screen.Duration > 0f)
                    _screen.Seek(_ownerPausedAt);

                SetUIPaused(true);
            }
            else if (_ownerPlaying)
            {
                _locallyPaused = false;
                _waitForSync = false;

                if (!_screen.IsPlaying)
                    _screen.Play();

                SetUIPaused(false);
                SyncVideo();
            }
            else
            {
                _locallyPaused = false;
                _waitForSync = false;

                if (_screen.IsPlaying)
                    _screen.Pause();

                SetUIPaused(false);
            }
        }

        public override void OnVideoReady(VoltVideoPlayer player)
        {
            NotifyVideoReady();
        }

        public void NotifyVideoReady()
        {
            ResetVideoLoad();
            _playlistErrorCount = 0;

            HideErrorOverlay();

            _adaptiveReloading = false;
            _bufferTimer = 0f;
            _stablePlaybackTime = 0f;
            _lastBufferResetTime = VoltTime.RealTime;

            if (_screen == null)
            {
                _pendingVideoLoad = true;
                return;
            }

            _videoSync = _screen.Duration > 0f;

            if (!_videoSync && IsYouTubeUrl(_syncedURL) && !IsStreamUrl(_syncedURL))
            {
                _syncedLiveStream = true;

                if (IsOwner)
                {
                    currentPlayerMode = PLAYER_MODE_AVPRO;
                    QueueSerialize();
                }
            }

            if (_screen != null && IsLiveFormatUrl(_screen.Url))
            {
                _syncedLiveStream = true;
                _videoSync = false;

                if (IsOwner)
                {
                    currentPlayerMode = PLAYER_MODE_AVPRO;
                    QueueSerialize();
                }

                if (_localPlayerMode != PLAYER_MODE_AVPRO)
                    SetLocalPlayerMode(PLAYER_MODE_AVPRO);
            }

            if (!_videoSync && _localPlayerMode == PLAYER_MODE_UNITY)
            {
                if (IsOwner)
                    currentPlayerMode = PLAYER_MODE_AVPRO;

                SetLocalPlayerMode(PLAYER_MODE_AVPRO);
            }

            if (IsOwner && !_syncedLiveStream && !_liveProbeChecked && !IsStreamUrl(_syncedURL))
                StartLiveProbe(_syncedURL);

            _waitForSync = false;
            _endHandled = false;
            SetStatusText("");

            if (IsOwner)
            {
                _ownerPlaying = true;

                if (_networkTimeVideoStart == 0)
                    _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(_videoTargetStartTime * 1000f);

                _locallyPaused = _ownerPaused;

                if (_ownerPaused)
                {
                    _screen.Pause();
                    if (_ownerPausedAt > 0f && _screen.Duration > 0f)
                        _screen.Seek(_ownerPausedAt);
                }
                else
                {
                    _screen.Play();
                    SyncVideo();
                }

                SetUIPaused(_locallyPaused);
                QueueSerialize();
                return;
            }

            if (_ownerPaused)
            {
                _locallyPaused = true;
                _screen.Pause();

                if (_ownerPausedAt > 0f && _screen.Duration > 0f)
                    _screen.Seek(_ownerPausedAt);
            }
            else if (_ownerPlaying)
            {
                _locallyPaused = false;
                _screen.Play();
                SyncVideo();
            }
            else
            {
                _locallyPaused = false;
                _screen.Pause();
            }

            SetUIPaused(_locallyPaused);
        }

        public override void OnVideoStarted(VoltVideoPlayer player)
        {
            OnVideoStart();
        }

        public void OnVideoStart()
        {
            if (_screen == null)
                return;

            _endHandled = false;
            _adaptiveReloading = false;
            _bufferTimer = 0f;
            _stablePlaybackTime = 0f;
            _lastBufferResetTime = VoltTime.RealTime;
            HideErrorOverlay();

            if (IsOwner)
            {
                // Keep the start time chosen when the URL was set: load time is skipped so everyone meets
                // at the same point in the video (the same trick as the working example script).
                if (_networkTimeVideoStart == 0)
                    _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(_videoTargetStartTime * 1000f);

                _ownerPlaying = true;
                _locallyPaused = _ownerPaused;
                _waitForSync = false;
                _videoTargetStartTime = 0f;

                if (_ownerPaused)
                    _screen.Pause();
                else
                    SyncVideo();

                QueueSerialize();

                LogMessage("Started video: " + _syncedURL);
            }
            else if (_ownerPaused)
            {
                _locallyPaused = true;
                _screen.Pause();

                if (_ownerPausedAt > 0f && _screen.Duration > 0f)
                    _screen.Seek(_ownerPausedAt);
            }
            else if (!_ownerPlaying)
            {
                _screen.Pause();
                _waitForSync = true;
            }
            else
            {
                _locallyPaused = false;
                _screen.Play();
                SyncVideo();
                LogMessage("Started video: " + _syncedURL);
            }

            _videoTargetStartTime = 0f;

            SetUIPaused(_locallyPaused);

            SendCallback("OnVoltVideoPlay");
        }

        public override void OnVideoEnded(VoltVideoPlayer player)
        {
            OnVideoEnd();
        }

        public void OnVideoEnd()
        {
            HideErrorOverlay();

            if ((_loopVideo || _localLoopVideo) && !_endHandled)
            {
                if (_screen != null && _screen.Duration > 0f && _screen.Time >= _screen.Duration - 0.5f)
                    _screen.Seek(0f);

                OnVideoLoop();
                return;
            }

            if (_endHandled)
                return;

            _endHandled = true;

            if (IsOwner)
            {
                _ownerPlaying = false;
                _ownerPaused = false;
                _ownerPausedAt = 0f;
                _locallyPaused = false;
                _networkTimeVideoStart = 0;
                _videoTargetStartTime = 0f;
                _waitForSync = false;

                SetStatusText("");
                SetUIPaused(false);

                PlayNextVideoFromPlaylist();
                QueueSerialize();
            }
            else
            {
                _ownerPlaying = false;
                _ownerPaused = false;
                _ownerPausedAt = 0f;
                _locallyPaused = false;
                _networkTimeVideoStart = 0;
                _videoTargetStartTime = 0f;
                _waitForSync = false;

                SetStatusText("");
                SetUIPaused(false);
            }

            SendCallback("OnVoltVideoEnd");
        }

        public override void OnVideoPaused(VoltVideoPlayer player)
        {
            OnVideoPause();
        }

        public void OnVideoPause()
        {
        }

        public void OnVideoPlay()
        {
        }

        public void OnVideoLoop()
        {
            _adaptiveReloading = false;
            _bufferTimer = 0f;
            _stablePlaybackTime = 0f;
            _lastBufferResetTime = VoltTime.RealTime;

            if (IsOwner || CanControlVideoPlayer())
            {
                _ownerPlaying = true;
                _ownerPaused = false;
                _ownerPausedAt = 0f;
                _locallyPaused = false;
                _endHandled = false;

                _networkTimeVideoStart = VoltNetworking.ServerTimeMs;
                _localNetworkTimeStart = _networkTimeVideoStart;

                if (_screen != null && !_screen.IsPlaying)
                    _screen.Play();
            }

            SetUIPaused(false);
            QueueSerialize();
        }

        public override void OnVideoError(VoltVideoPlayer player, string reason)
        {
            _lastErrorDetail = reason;
            NotifyVideoError(VideoError.Unknown);
        }

        private void StartLiveProbe(string url)
        {
            if (_liveProbeChecked || _liveProbe != null)
                return;

            string videoId = GetYouTubeVideoId(url);

            if (string.IsNullOrEmpty(videoId))
            {
                _liveProbeChecked = true;
                return;
            }

            _liveProbeChecked = true;

            string endpoint = "https://www.youtube.com/youtubei/v1/player?prettyPrint=false";

            if (!VoltWeb.IsAllowed(new VoltUrl(endpoint)))
            {
                LogWarning("Live check is blocked; add youtube.com to the world's web domains.");
                return;
            }

            string body = "{\"context\":{\"client\":{\"clientName\":\"ANDROID\",\"clientVersion\":\"21.26.364\",\"androidSdkVersion\":30,\"osName\":\"Android\",\"osVersion\":\"11\",\"hl\":\"en\",\"timeZone\":\"UTC\",\"utcOffsetMinutes\":0}},\"videoId\":\"" + videoId + "\",\"playbackContext\":{\"contentPlaybackContext\":{\"html5Preference\":\"HTML5_PREF_WANTS\"}},\"contentCheckOk\":true,\"racyCheckOk\":true}";

            _liveProbeUrl = url;
            _liveProbe = VoltWeb.Post(new VoltUrl(endpoint), body, "application/json");
        }

        public override void OnWebResponse(VoltWebRequest request)
        {
            if (request == null || _liveProbe == null)
                return;

            if (request != _liveProbe && request.Url != _liveProbe.Url)
                return;

            string url = _liveProbeUrl;
            _liveProbe = null;
            _liveProbeUrl = "";

            if (!request.Success || string.IsNullOrEmpty(url) || url != _syncedURL)
            {
                if (!request.Success)
                    LogWarning("Live check failed: " + request.Error);

                return;
            }

            string text = request.Text;

            if (string.IsNullOrEmpty(text))
                return;

            bool live = text.IndexOf("\"isLiveNow\":true") >= 0 ||
                        text.IndexOf("\"isLive\":true") >= 0 ||
                        text.IndexOf("hlsManifestUrl") >= 0;

            if (live)
            {
                LogMessage("YouTube link is live; switching to Live mode.");
                ApplyDetectedLiveMode();
            }
        }

        private void ApplyDetectedLiveMode()
        {
            _syncedLiveStream = true;
            _videoSync = false;
            currentPlayerMode = PLAYER_MODE_AVPRO;

            if (_localPlayerMode != PLAYER_MODE_AVPRO)
                SetLocalPlayerMode(PLAYER_MODE_AVPRO);

            if (IsOwner)
                QueueSerialize();
        }

        private static string GetYouTubeVideoId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            int queryStart = url.IndexOf("?");
            string baseUrl = queryStart >= 0 ? url.Substring(0, queryStart) : url;
            string query = queryStart >= 0 ? url.Substring(queryStart + 1) : "";
            string lower = baseUrl.ToLowerInvariant();

            int marker = lower.IndexOf("youtu.be/");
            if (marker >= 0)
                return TakeVideoId(baseUrl, marker + 8);

            marker = lower.IndexOf("/watch");
            if (marker >= 0)
            {
                int v = query.IndexOf("v=");
                if (v >= 0)
                    return TakeVideoId(query, v + 2);
            }

            marker = lower.IndexOf("/live/");
            if (marker >= 0)
                return TakeVideoId(baseUrl, marker + 6);

            marker = lower.IndexOf("/shorts/");
            if (marker >= 0)
                return TakeVideoId(baseUrl, marker + 8);

            marker = lower.IndexOf("/embed/");
            if (marker >= 0)
                return TakeVideoId(baseUrl, marker + 7);

            marker = lower.IndexOf("/v/");
            if (marker >= 0)
                return TakeVideoId(baseUrl, marker + 3);

            return "";
        }

        private static string TakeVideoId(string text, int start)
        {
            if (start < 0 || start + 11 > text.Length)
                return "";

            string id = text.Substring(start, 11);

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') || c == '-' || c == '_';

                if (!valid)
                    return "";
            }

            return id;
        }

        private bool IsCurrentLive()
        {
            return _syncedLiveStream || IsLiveUrl(_syncedURL);
        }

        public void _OnVideoErrorCallback(VideoError videoError)
        {
            _lastErrorDetail = "";
            NotifyVideoError(videoError);
        }

        public void NotifyVideoError(VideoError videoError)
        {
            if (_adaptiveReloading)
            {
                _adaptiveReloading = false;
                _localLoadUrlOverride = "";
                _localQualityLevel = 0;
                _qualityAttempts = 3;
                _bufferTimer = 0f;
                _stablePlaybackTime = 0f;
                _lastQualityChangeTime = VoltTime.RealTime;
                _loadingVideo = true;
                _loadStartTime = VoltTime.RealTime;

                if (_screen != null)
                    _screen.LoadUrl(new VoltUrl(_syncedURL));

                return;
            }

            string errorFallback = "The video could not be loaded or played.";
            string knownType = "";

            if (videoError == VideoError.RateLimited)
            {
                errorFallback = "The video service is rate limiting this link. Please try again shortly.";
                knownType = "RateLimited";
            }
            else if (videoError == VideoError.PlayerError)
            {
                errorFallback = "This video format is not available, or the video could not be played.";
                knownType = "PlayerError";
            }
            else if (videoError == VideoError.InvalidURL)
            {
                errorFallback = "This link is not a valid video URL.";
                knownType = "InvalidUrl";
            }
            else if (videoError == VideoError.AccessDenied)
            {
                errorFallback = "This web service is not allowed here, or the video is blocked in your region.";
                knownType = "DomainNotAllowed";
            }

            ShowPlaybackError(errorFallback, knownType);

            if (videoError == VideoError.RateLimited)
            {
                SetStatusText("Rate limited, retrying...");
                LogWarning("Rate limited, retrying...");
                _currentLoadingTime = RATE_LIMIT_RETRY_TIMEOUT;
                return;
            }

            if (videoError == VideoError.PlayerError)
            {
                SetStatusText("Video error, retrying...");
                LogError("Video player error when trying to load " + _syncedURL);
                _loadingVideo = true;
                _currentLoadingTime = VIDEO_ERROR_RETRY_TIMEOUT;
                return;
            }

            ResetVideoLoad();
            _videoTargetStartTime = 0f;

            if (_screen != null)
                _screen.Stop();

            LogError("Video '" + _syncedURL + "' failed to play with error " + videoError);

            switch (videoError)
            {
                case VideoError.InvalidURL:
                    SetStatusText("Invalid URL");
                    break;
                case VideoError.AccessDenied:
                    SetStatusText("Video blocked, enable untrusted URLs");
                    break;
                default:
                    SetStatusText("Failed to load video");
                    break;
            }

            ++_playlistErrorCount;
            PlayNextVideoFromPlaylist();

            SendCallback("OnVoltVideoError");
        }

        public override void OnOwnershipTransferred(VoltPlayer newOwner)
        {
            if (newOwner != null && newOwner.IsLocal)
            {
                SetUILocked(_isMasterOnly);
                QueueSerialize();
            }

            NotifyOwnershipTransferred(new VRCPlayerApi(newOwner));
        }

        public void NotifyOwnershipTransferred(VRCPlayerApi player)
        {
            SendUIOwnerUpdate();

            SendCallback("OnVoltVideoOwnershipChange");
        }

        public override void OnPlayerJoined(VoltPlayer player)
        {
            NotifyPlayerJoined(new VRCPlayerApi(player));
        }

        public void NotifyPlayerJoined(VRCPlayerApi player)
        {
            if (!player.isLocal)
                QueueSerialize();
        }

        public override bool OnOwnershipRequest(VoltPlayer requester, VoltPlayer newOwner)
        {
            return CheckOwnershipRequest(new VRCPlayerApi(requester), new VRCPlayerApi(newOwner));
        }

        public bool CheckOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return !_isMasterOnly || IsPrivilegedUser(requestedOwner);
        }

        public void StopVideo()
        {
            if (!IsOwner && !CanControlVideoPlayer())
                return;

            HideErrorOverlay();

            _liveProbe = null;
            _liveProbeUrl = "";
            _liveProbeChecked = false;

            _networkTimeVideoStart = 0;
            _ownerPlaying = false;
            _locallyPaused = false;
            _ownerPaused = false;
            _ownerPausedAt = 0f;
            _videoTargetStartTime = 0f;
            _lastCurrentTime = 0f;
            _waitForSync = false;
            _endHandled = true;

            if (_screen != null)
                _screen.Stop();

            SetUIPaused(false);
            ResetVideoLoad();

            QueueSerialize();

            SendCallback("OnVoltVideoStop");
        }

        
        public void PlayVideo(VRCUrl url)
        {
            PlayVideoInternal(url, true);
        }

        
        public VRCUrl GetCurrentURL()
        {
            return new VRCUrl(_syncedURL);
        }

        private void PlayVideoInternal(VRCUrl url, bool stopPlaylist)
        {
            if (!CanControlVideoPlayer())
            {
                SetStatusText("Only the master can change the video URL");
                return;
            }

            if (url == null)
                return;

            string urlStr = NormalizeUrl(url.Get());
            _lastErrorUrl = urlStr;

            if (!ValidateURL(urlStr))
                return;

            TakeOwnership();

            if (stopPlaylist)
                _nextPlaylistIndex = -1;

            StopVideo();
            ResetQualityState();

            _syncedURL = urlStr;

            ++_syncedVideoIdx;
            _currentVideoIdx = _syncedVideoIdx;

            bool isStream = IsStreamUrl(urlStr);
            _syncedLiveStream = IsLiveUrl(urlStr);
            currentPlayerMode = isStream ? PLAYER_MODE_AVPRO : PLAYER_MODE_UNITY;
            _videoSync = !isStream;
            SetLocalPlayerMode(currentPlayerMode);

            _liveProbe = null;
            _liveProbeUrl = "";
            _liveProbeChecked = false;

            if (!isStream)
                StartLiveProbe(urlStr);

            // Start the shared clock as soon as the link is set, like the working example: the time spent
            // loading is skipped and a late joiner seeks to the same place.
            _networkTimeVideoStart = VoltNetworking.ServerTimeMs;
            _ownerPlaying = false;
            _ownerPaused = false;
            _ownerPausedAt = 0f;
            _videoTargetStartTime = GetVideoStartTime(urlStr);
            _pendingVideoLoad = false;
            _waitForSync = false;
            _endHandled = false;

            StartVideoLoad(new VRCUrl(urlStr));
            AddUIUrlHistory(new VRCUrl(urlStr));

            QueueSerialize();

            SendCallback("OnVoltVideoLoadStart");
        }

        private void ResetVideoLoad()
        {
            _loadingVideo = false;
            _currentRetryCount = 0;
            _currentLoadingTime = DEFAULT_RETRY_TIMEOUT;
        }

        private void UpdateVideoLoad()
        {
            _currentLoadingTime -= VoltTime.DeltaTime;

            if (_currentLoadingTime <= 0f)
            {
                _currentLoadingTime = DEFAULT_RETRY_TIMEOUT;

                if (++_currentRetryCount > MAX_RETRY_COUNT)
                {
                    NotifyVideoError(VideoError.Unknown);
                }
                else
                {
                    LogMessage("Retrying load");

                    SetStatusText("Retrying load...");
                    if (_screen != null)
                        _screen.LoadUrl(new VoltUrl(_syncedURL));
                }
            }
        }

        private float _lastSyncTime;
        private float _lastDriftCheckTime;
        private float _lastSeekTime;
        private const float MIN_SEEK_INTERVAL = 0.75f;
        private const float DRIFT_CHECK_INTERVAL = 0.5f;

        private void SyncVideoIfTime()
        {
            float timeSinceStartup = VoltTime.RealTime;
            float checkInterval = Mathf.Min(DRIFT_CHECK_INTERVAL, syncFrequency);

            if (checkInterval < 0.1f)
                checkInterval = 0.1f;

            if (timeSinceStartup - _lastDriftCheckTime < checkInterval)
                return;

            _lastDriftCheckTime = timeSinceStartup;

            SyncVideo();
        }

        
        public void SyncVideo()
        {
            if (_screen == null)
                return;

            if (_locallyPaused)
            {
                if (_screen.IsPlaying)
                    _screen.Pause();

                if (_ownerPausedAt > 0f && _screen.Duration > 0f && Mathf.Abs(_screen.Time - _ownerPausedAt) > 0.15f)
                    _screen.Seek(_ownerPausedAt);

                return;
            }

            if (!_ownerPlaying)
            {
                if (_screen.IsPlaying)
                    _screen.Pause();

                return;
            }

            if (_screen.Duration <= 0f || IsCurrentLive())
            {
                if (!_screen.IsPlaying)
                    _screen.Play();

                return;
            }

            if (_networkTimeVideoStart == 0)
                return;

            float offsetTime = Mathf.Clamp((VoltNetworking.ServerTimeMs - _networkTimeVideoStart) / 1000f + localSyncOffset, 0f, _screen.Duration);
            float drift = Mathf.Abs(_screen.Time - offsetTime);
            float tolerance = GetSyncTolerance();

            bool severeDrift = drift > tolerance * 3f;
            bool intervalPassed = VoltTime.RealTime - _lastSeekTime >= GetSeekInterval();
            bool severeAllowed = severeDrift && VoltTime.RealTime - _lastSeekTime >= MIN_SEEK_INTERVAL;

            if (drift > tolerance && (severeAllowed || intervalPassed))
            {
                _lastSeekTime = VoltTime.RealTime;
                _screen.Seek(offsetTime);
                LogMessage("Syncing video to " + offsetTime);
            }

            if (!_screen.IsPlaying)
                _screen.Play();
        }

        public void ForceSyncVideo()
        {
            if (_screen == null || _screen.Duration <= 0f || IsCurrentLive())
                return;

            if (_networkTimeVideoStart == 0)
                return;

            float offsetTime = Mathf.Clamp((VoltNetworking.ServerTimeMs - _networkTimeVideoStart) / 1000f + localSyncOffset, 0f, _screen.Duration);

            _lastSeekTime = VoltTime.RealTime;
            _screen.Seek(offsetTime);

            if (!_locallyPaused && _ownerPlaying && !_screen.IsPlaying)
                _screen.Play();

            LogMessage("Forcing video sync to " + offsetTime);
        }

        private float GetSyncTolerance()
        {
            float tolerance = Mathf.Max(0.15f, syncThreshold);

            if (!adaptiveSync)
                return tolerance;

            int ping = VoltNetworking.Ping;

            if (ping >= lowBandwidthPing)
                tolerance += 0.35f;
            else if (ping >= mediumBandwidthPing)
                tolerance += 0.15f;

            return tolerance;
        }

        private float GetSeekInterval()
        {
            if (!adaptiveSync)
                return MIN_SEEK_INTERVAL;

            int ping = VoltNetworking.Ping;

            if (ping >= lowBandwidthPing)
                return MIN_SEEK_INTERVAL * 2f;

            if (ping >= mediumBandwidthPing)
                return MIN_SEEK_INTERVAL * 1.5f;

            return MIN_SEEK_INTERVAL;
        }

        private void TryRecoverQuality()
        {
            if (IsCurrentLive() || _localQualityLevel <= 0 || _qualityRecoveryAttempts >= 2)
                return;

            ++_qualityRecoveryAttempts;

            int nextLevel = _localQualityLevel > 1 ? _localQualityLevel - 1 : 0;
            _localQualityLevel = nextLevel;
            _localLoadUrlOverride = GetQualityUrl(_syncedURL, nextLevel);
            _lastQualityChangeTime = VoltTime.RealTime;
            _adaptiveReloading = true;

            if (_screen != null)
                _screen.Stop();

            StartVideoLoad(new VRCUrl(_syncedURL));
        }

        private void TryAdaptiveQuality()
        {
            if (VoltTime.RealTime - _lastQualityChangeTime < 6f)
                return;

            if (_qualityAttempts >= 3)
            {
                ForceSyncVideo();
                return;
            }

            int nextLevel = GetNextQualityLevel();

            if (nextLevel == _localQualityLevel)
            {
                ForceSyncVideo();
                return;
            }

            string qualityUrl = GetQualityUrl(_syncedURL, nextLevel);
            _localQualityLevel = nextLevel;
            _lastQualityChangeTime = VoltTime.RealTime;

            if (qualityUrl == _syncedURL)
            {
                if (IsCurrentLive())
                {
                    ++_qualityAttempts;
                    _adaptiveReloading = true;

                    if (_screen != null)
                        _screen.Stop();

                    StartVideoLoad(new VRCUrl(_syncedURL));
                    return;
                }

                _qualityAttempts = 3;
                ForceSyncVideo();
                return;
            }

            ++_qualityAttempts;
            _localLoadUrlOverride = qualityUrl;
            _adaptiveReloading = true;

            if (_screen != null)
                _screen.Stop();

            StartVideoLoad(new VRCUrl(_syncedURL));
        }

        private int GetNextQualityLevel()
        {
            if (_localQualityLevel <= 0)
            {
                int ping = VoltNetworking.Ping;

                if (ping >= lowBandwidthPing)
                    return 1;

                if (ping >= mediumBandwidthPing)
                    return 2;

                return 3;
            }

            if (_localQualityLevel > 1)
                return _localQualityLevel - 1;

            return 1;
        }

        private static string GetQualityUrl(string url, int level)
        {
            if (string.IsNullOrEmpty(url) || level <= 0)
                return url;

            if (IsYouTubeUrl(url))
            {
                string quality = level == 1 ? "medium" :
                    level == 2 ? "large" :
                    level == 3 ? "hd720" :
                    level == 4 ? "hd1080" :
                    level == 5 ? "hd1440" : "highres";

                return AppendQuery(url, "vq", quality);
            }

            if (IsTwitchUrl(url))
            {
                string quality = level == 1 ? "360p" :
                    level == 2 ? "480p" :
                    level == 3 ? "720p" :
                    level == 4 ? "1080p" :
                    level == 5 ? "1440p" : "2160p";

                return AppendQuery(url, "quality", quality);
            }

            return url;
        }

        private static string AppendQuery(string url, string key, string value)
        {
            if (url.IndexOf(key + "=") >= 0)
                return url;

            string separator = url.IndexOf("?") >= 0 ? "&" : "?";
            return url + separator + key + "=" + value;
        }

        private void ResetQualityState()
        {
            _localQualityLevel = 0;
            _qualityAttempts = 0;
            _adaptiveReloading = false;
            _localLoadUrlOverride = "";
            _bufferTimer = 0f;
            _stablePlaybackTime = 0f;
            _lastQualityChangeTime = 0f;
            _qualityRecoveryAttempts = 0;
        }

        private string GetInitialLoadUrl(string url)
        {
            if (!requestHighQualityVideo || string.IsNullOrEmpty(url) || IsCurrentLive())
                return url;

            if (IsYouTubeUrl(url) && !IsStreamUrl(url))
                return AppendQuery(url, "vq", "highres");

            return url;
        }

        private void StartVideoLoad(VRCUrl url)
        {
            SetStatusText("Loading video...");
            ResetVideoLoad();
            _loadingVideo = true;
            _loadStartTime = VoltTime.RealTime;

            if (_screen != null)
            {
                _pendingVideoLoad = false;

                string loadUrl = _localLoadUrlOverride;
                _localLoadUrlOverride = "";

                if (string.IsNullOrEmpty(loadUrl))
                    loadUrl = GetInitialLoadUrl(url.Get());

                _screen.LoadUrl(new VoltUrl(loadUrl));
            }
            else
            {
                // OnStart/OnUpdate will load it as soon as the screen can be found.
                _pendingVideoLoad = true;
                _currentLoadingTime = 0.5f;
            }
        }

        private void SetPausedInternal(bool paused, bool updatePauseTime)
        {
            if (IsOwner || CanControlVideoPlayer())
            {
                _ownerPaused = paused;

                // Only a real pause/unpause moves the shared clock; OnVideoStart also calls this
                // with updatePauseTime=false and must keep the start time chosen before loading.
                if (updatePauseTime)
                {
                    if (paused)
                    {
                        float at = _screen != null ? _screen.Time : _lastCurrentTime;
                        _ownerPausedAt = at;
                        _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(at * 1000f);
                        _lastCurrentTime = at;
                    }
                    else
                    {
                        _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(_ownerPausedAt * 1000f);
                        _lastCurrentTime = _ownerPausedAt;
                    }
                }
            }

            if (_locallyPaused != paused)
            {
                _locallyPaused = paused;

                if (_screen != null)
                {
                    if (paused)
                    {
                        _screen.Pause();

                        if (_ownerPausedAt > 0f && _screen.Duration > 0f && Mathf.Abs(_screen.Time - _ownerPausedAt) > 0.15f)
                            _screen.Seek(_ownerPausedAt);
                    }
                    else
                    {
                        if (_ownerPausedAt > 0f && _screen.Duration > 0f)
                            _screen.Seek(_ownerPausedAt);

                        _screen.Play();
                    }
                }

                SetUIPaused(paused);

                if (_locallyPaused)
                    SendCallback("OnVoltVideoPause");
                else
                    SendCallback("OnVoltVideoUnpause");
            }

            QueueRateLimitedSerialize();
        }

        
        public void SetPaused(bool paused)
        {
            if (!CanControlVideoPlayer())
            {
                SetStatusText("Only the master can pause the video");
                return;
            }

            if (IsOwner || CanControlVideoPlayer())
            {
                if (!paused && _screen != null && !_screen.IsReady && !_loadingVideo && !string.IsNullOrEmpty(_syncedURL))
                {
                    StartVideoLoad(new VRCUrl(_syncedURL));
                    return;
                }

                SetPausedInternal(paused, true);
            }
        }

        
        public bool IsPaused()
        {
            return _ownerPaused;
        }

        private void SetLoopingInternal(bool loop)
        {
            if (loop == _localLoopVideo)
                return;

            if (IsOwner || CanControlVideoPlayer())
                _loopVideo = loop;

            _localLoopVideo = loop;

            if (_screen != null)
            {
                _screen.Loop = loop;

                if (IsOwner && _screen.Duration > 0f)
                {
                    float at = _locallyPaused ? _ownerPausedAt : _screen.Time;
                    _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(at * 1000f);
                    _localNetworkTimeStart = _networkTimeVideoStart;
                }
            }

            if (loop)
                _endHandled = false;

            SetUILooping(loop);

            QueueRateLimitedSerialize();
        }

        
        public void SetLooping(bool loop)
        {
            if (IsOwner || CanControlVideoPlayer())
                SetLoopingInternal(loop);
        }

        
        public bool IsLooping()
        {
            return _localLoopVideo;
        }

        
        public float GetVolume()
        {
            return _currentVolume;
        }

        
        public void SetVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);

            _currentVolume = volume;
            _muted = false;

            if (_screen != null)
                _screen.Volume = volume;

            SetUIVolume(volume);
            SetUIMuted(false);
        }

        
        public bool IsMuted()
        {
            return _muted;
        }

        
        public void SetMuted(bool muted)
        {
            _muted = muted;

            if (_screen != null)
                _screen.Volume = muted ? 0f : _currentVolume;

            SetUIMuted(muted);
        }

        
        public void SeekTo(float progress)
        {
            if (!allowSeeking || (!IsOwner && !CanControlVideoPlayer()))
                return;

            if (_screen == null || _screen.Duration <= 0f || IsCurrentLive())
                return;

            progress = Mathf.Clamp01(progress);

            float newTargetTime = _screen.Duration * progress;
            _lastVideoTime = newTargetTime;
            _lastCurrentTime = newTargetTime;

            _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(newTargetTime * 1000f);
            _localNetworkTimeStart = _networkTimeVideoStart;

            if (_ownerPaused)
                _ownerPausedAt = newTargetTime;

            _lastSeekTime = VoltTime.RealTime;
            _bufferTimer = 0f;
            _stablePlaybackTime = 0f;
            _lastBufferResetTime = VoltTime.RealTime;
            _screen.Seek(newTargetTime);

            if (!_locallyPaused && !_screen.IsPlaying)
                _screen.Play();

            QueueRateLimitedSerialize();
        }

        private bool _delayedSyncAllowed = true;
        private int _finalSyncCounter;

        private void QueueRateLimitedSerialize()
        {
            if (_delayedSyncAllowed)
            {
                QueueSerialize();
                _delayedSyncAllowed = false;
                SendCustomEventDelayedSeconds(nameof(_UnlockDelayedSync), 0.5f);
            }

            ++_finalSyncCounter;
            SendCustomEventDelayedSeconds(nameof(_SendFinalSync), 0.8f);
        }

        public void _UnlockDelayedSync()
        {
            _delayedSyncAllowed = true;
        }

        public void _SendFinalSync()
        {
            if (--_finalSyncCounter == 0)
                QueueSerialize();
        }

        
        private static bool IsAuthority()
        {
            if (VoltNetworking.IsMaster)
                return true;

            VoltPlayer authority = VoltInstance.Authority;
            return authority != null && authority.IsLocal;
        }

        public bool CanControlVideoPlayer()
        {
            if (!_isMasterOnly)
                return true;

            if (IsAuthority())
                return true;

            return IsPrivilegedUser(VoltNetworking.LocalPlayer);
        }

        
        public bool IsPrivilegedUser(VRCPlayerApi player)
        {
            if (player == null)
                return false;

            return player.isMaster || (allowInstanceCreatorControl && player.isInstanceOwner);
        }

        public bool IsPrivilegedUser(VoltPlayer player)
        {
            if (player == null)
                return false;

            if (player.IsMaster)
                return true;

            if (player.IsLocal)
                return IsAuthority();

            return false;
        }

        
        public new void TakeOwnership()
        {
            if (IsOwner)
                return;

            if (CanControlVideoPlayer())
                SetOwner(VoltNetworking.LocalPlayer);
        }

        
        public void QueueSerialize()
        {
            if (!IsOwner)
                return;

            RequestSerialization();
        }

        private bool _shuffled;

        
        public void PlayNextVideoFromPlaylist()
        {
            if (_nextPlaylistIndex == -1 || playlist == null || playlist.Length == 0 || !IsOwner)
                return;

            if (loopPlaylist && _playlistErrorCount > PLAYLIST_ERROR_RETRY_COUNT)
            {
                LogError("Maximum number of retries for playlist video looping hit. Stopping playlist playback.");
                _nextPlaylistIndex = -1;
                return;
            }

            int currentIdx = _nextPlaylistIndex++;

            if (currentIdx >= playlist.Length)
            {
                if (loopPlaylist)
                {
                    _nextPlaylistIndex = 1;
                    currentIdx = 0;
                }
                else
                {
                    _nextPlaylistIndex = -1;
                    return;
                }
            }

            VRCUrl nextUrl = playlist[currentIdx];
            if (nextUrl == null || string.IsNullOrWhiteSpace(nextUrl.Get()))
            {
                _nextPlaylistIndex = -1;
                return;
            }

            PlayVideoInternal(nextUrl, false);
        }

        
        public int GetPlaylistIndex()
        {
            return _nextPlaylistIndex > 0 ? _nextPlaylistIndex - 1 : -1;
        }

        
        public void SetNextPlaylistVideo(int nextPlaylistIdx)
        {
            _nextPlaylistIndex = nextPlaylistIdx;
        }

        
        public void SetLocked(bool locked)
        {
            if (IsOwner || CanControlVideoPlayer())
                SetLockedInternal(locked);
        }

        private void SetLockedInternal(bool locked)
        {
            _isMasterOnly = locked;
            _lastMasterLocked = _isMasterOnly;

            SetUILocked(locked);

            QueueSerialize();

            SendCallback("OnVoltVideoLockChange");
        }

        
        public bool IsLocked()
        {
            return _isMasterOnly;
        }

        
        public void SetToUnityPlayer()
        {
            SetPlayerMode(PLAYER_MODE_UNITY);
        }

        
        public void SetToAVProPlayer()
        {
            SetPlayerMode(PLAYER_MODE_AVPRO);
        }

        private void SetPlayerMode(int newPlayerMode)
        {
            if (!CanControlVideoPlayer())
            {
                SetStatusText("Only the master can change the player mode");
                return;
            }

            TakeOwnership();

            if (currentPlayerMode == newPlayerMode)
            {
                SetLocalPlayerMode(newPlayerMode);
                return;
            }

            StopVideo();
            ResetQualityState();

            _syncedURL = "";
            _nextPlaylistIndex = -1;
            _videoSync = false;
            ++_syncedVideoIdx;
            _currentVideoIdx = _syncedVideoIdx;

            SetStatusText("");
            currentPlayerMode = newPlayerMode;
            SetLocalPlayerMode(newPlayerMode);

            QueueSerialize();

            SendCallback("OnVoltVideoModeChange");
        }

        private void SetLocalPlayerMode(int newPlayerMode)
        {
            _localPlayerMode = newPlayerMode;

            if (IsUsingUnityPlayer())
                SetUIToVideoMode();
            else
                SetUIToStreamMode();

            SetUIPaused(_locallyPaused);
        }

        
        public bool IsInVideoMode()
        {
            return _localPlayerMode == PLAYER_MODE_UNITY && _videoSync;
        }

        
        public bool IsInStreamMode()
        {
            if (_localPlayerMode == PLAYER_MODE_AVPRO)
                return true;

            return !string.IsNullOrEmpty(_syncedURL) && !_videoSync;
        }

        
        public bool IsUsingUnityPlayer()
        {
            return _localPlayerMode == PLAYER_MODE_UNITY;
        }

        
        public bool IsUsingAVProPlayer()
        {
            return _localPlayerMode == PLAYER_MODE_AVPRO;
        }

        
        public void Reload()
        {
            if (_loadingVideo)
                return;

            if (string.IsNullOrEmpty(_syncedURL))
                return;

            if (!CanControlVideoPlayer())
            {
                SetStatusText("Only the master can reload the video");
                return;
            }

            TakeOwnership();

            float at = _ownerPaused
                ? _ownerPausedAt
                : (_screen != null ? _screen.Time : _lastCurrentTime);

            _videoTargetStartTime = at;
            _lastCurrentTime = at;
            _networkTimeVideoStart = VoltNetworking.ServerTimeMs - (int)(at * 1000f);
            _localNetworkTimeStart = _networkTimeVideoStart;

            if (_ownerPaused)
                _ownerPausedAt = at;

            ++_syncedVideoIdx;
            _currentVideoIdx = _syncedVideoIdx;

            _waitForSync = false;
            _endHandled = true;

            if (_screen != null)
                _screen.Stop();

            _endHandled = false;

            ResetQualityState();
            HideErrorOverlay();
            StartVideoLoad(new VRCUrl(_syncedURL));

            QueueSerialize();

            SendCallback("OnVoltVideoReload");
        }

        public float GetPlaybackTime()
        {
            if (_screen == null)
                return 0f;

            if (_locallyPaused && _ownerPausedAt > 0f)
                return _ownerPausedAt;

            float time = _screen.Time;

            if (time <= 0.01f && _networkTimeVideoStart != 0)
                time = Mathf.Clamp((VoltNetworking.ServerTimeMs - _networkTimeVideoStart) / 1000f + localSyncOffset, 0f, _screen.Duration);

            return time;
        }

        public float GetPlaybackDuration()
        {
            return _screen != null ? _screen.Duration : 0f;
        }

        public bool AreControlsHidden()
        {
            return _controlsHidden;
        }

        public void SetControlsHidden(bool hidden)
        {
            ApplyLocalControlsHidden(hidden);

            if (!IsOwner && CanControlVideoPlayer())
                TakeOwnership();

            if (IsOwner)
            {
                _controlsHidden = hidden;
                QueueSerialize();
            }
        }

        private void ApplyLocalControlsHidden(bool hidden)
        {
            if (_screen == null)
                return;

            ObjectToggle controls = _screen.gameObject.GetComponent<ObjectToggle>();
            if (controls != null)
                controls.ApplyHiddenState(hidden);

            if (_errorInteractable != null)
                _errorInteractable.Interactable = hidden;

            if (_errorCollider != null)
                _errorCollider.Enabled = hidden;

            if (!hidden && _errorCanvas != null && _errorCanvas.ActiveSelf)
                _errorCanvas.SetActive(false);
        }

        public VideoPlayerManager GetVideoManager()
        {
            if (_videoPlayerManager != null)
                return _videoPlayerManager;

            _videoPlayerManager = gameObject.GetComponentInChildren<VideoPlayerManager>();
            if (_videoPlayerManager == null)
                LogError("Video Player Manager not found, make sure you have a manager setup properly");

            return _videoPlayerManager;
        }

        private VoltVideoPlayer FindScreen()
        {
            VoltVideoPlayer found = gameObject.GetComponentInChildren<VoltVideoPlayer>();
            if (found != null)
                return found;

            VoltTransform screenTransform = transform.Find("VideoScreen");
            if (screenTransform != null)
                return screenTransform.GetComponent<VoltVideoPlayer>();

            return null;
        }

        private void HideControlsForError()
        {
            ApplyLocalControlsHidden(true);

            if (!IsOwner && IsAuthority() && CanControlVideoPlayer())
                TakeOwnership();

            if (IsOwner)
            {
                _controlsHidden = true;
                QueueSerialize();
            }
        }

        private void ShowErrorOverlay(string message)
        {
            HideControlsForError();
            ShowErrorOverlayLocal(message);

            if (IsOwner)
            {
                _syncedErrorText = message;
                _syncedErrorVisible = true;
                QueueSerialize();
            }
        }

        private void ShowErrorOverlayLocal(string message)
        {
            if (_errorCanvas != null)
                _errorCanvas.SetActive(true);

            if (_errorText != null)
                _errorText.Text = message;
        }

        private void HideErrorOverlay()
        {
            HideErrorOverlayLocal();

            if (IsOwner)
            {
                _syncedErrorText = "";
                _syncedErrorVisible = false;
                QueueSerialize();
            }
        }

        private void HideErrorOverlayLocal()
        {
            if (_errorCanvas != null)
                _errorCanvas.SetActive(false);
        }

        private void ShowPlaybackError(string fallback)
        {
            ShowPlaybackError(fallback, "");
        }

        private void ShowPlaybackError(string fallback, string knownType)
        {
            string detail = _lastErrorDetail;
            _lastErrorDetail = "";

            if (string.IsNullOrEmpty(detail))
                detail = fallback;

            string type = knownType;

            if (string.IsNullOrEmpty(type))
            {
                VoltVideoError error = _screen != null ? _screen.Error : VoltVideoError.Unknown;

                if (error == VoltVideoError.InvalidUrl)
                    type = "InvalidUrl";
                else if (error == VoltVideoError.DomainNotAllowed)
                    type = "DomainNotAllowed";
                else if (error == VoltVideoError.RateLimited)
                    type = "RateLimited";
                else if (error == VoltVideoError.PlayerError)
                    type = "PlayerError";
                else if (error == VoltVideoError.Unknown)
                    type = "Unknown";
                else
                    type = "None";
            }

            string hint;

            if (type == "InvalidUrl")
                hint = "Check the URL, the https scheme, and that it points to a supported video or stream.";
            else if (type == "DomainNotAllowed")
                hint = "Add this host to ParelVR SDK > Volt > Settings > Web Domains.";
            else if (type == "RateLimited")
                hint = "The host is limiting requests. Wait and retry.";
            else if (type == "PlayerError")
                hint = "The player rejected the media. The format or codec may be unsupported, or the provider may be blocking playback.";
            else
                hint = "No specific SDK reason was returned.";

            string displayUrl = string.IsNullOrEmpty(_lastErrorUrl) ? _syncedURL : _lastErrorUrl;
            string host = displayUrl;
            int schemeEnd = host.IndexOf("://");

            if (schemeEnd >= 0)
                host = host.Substring(schemeEnd + 3);

            int pathStart = host.IndexOf("/");
            if (pathStart >= 0)
                host = host.Substring(0, pathStart);

            string mode = (IsCurrentLive() || IsLiveUrl(displayUrl)) ? "Live" : (IsStreamUrl(displayUrl) ? "Stream" : "Video");

            string message = "PLAYBACK ERROR\n\n";
            message += "Type: " + type + "\n";

            if (!string.IsNullOrEmpty(host))
                message += "Host: " + host + "\n";

            message += "Mode: " + mode + "\n";
            message += "Detail: " + detail + "\n";
            message += "Hint: " + hint;

            if (!string.IsNullOrEmpty(displayUrl))
                message += "\n\nURL: " + displayUrl;

            ShowErrorOverlay(message);
        }

        private bool _lastMasterLocked;
        private float _lastCurrentTime;

        private float GetVideoStartTime(string url)
        {
            return 0f;
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            url = url.Trim();

            string leading = url.ToLowerInvariant();
            if (leading.StartsWith("hhttp://") || leading.StartsWith("hhttps://") ||
                leading.StartsWith("hyoutu") || leading.StartsWith("htwitch") ||
                leading.StartsWith("hwww.") || leading.StartsWith("hm.") ||
                leading.StartsWith("hplayer."))
                url = url.Substring(1);

            if (url.IndexOf("://") < 0)
                url = "https://" + url;

            string lower = url.ToLowerInvariant();

            if (lower.StartsWith("https://youtu.be/"))
                return url;

            if (lower.StartsWith("https://youtube.com/"))
                return "https://www.youtube.com/" + url.Substring("https://youtube.com/".Length);

            if (lower.StartsWith("https://m.youtube.com/"))
                return "https://www.youtube.com/" + url.Substring("https://m.youtube.com/".Length);

            if (lower.StartsWith("https://twitch.tv/"))
                return "https://www.twitch.tv/" + url.Substring("https://twitch.tv/".Length);

            if (lower.StartsWith("https://m.twitch.tv/"))
                return "https://www.twitch.tv/" + url.Substring("https://m.twitch.tv/".Length);

            return url;
        }

        private static bool IsYouTubeUrl(string url)
        {
            string lower = url.ToLowerInvariant();
            return lower.IndexOf("youtube.com") >= 0 || lower.IndexOf("youtu.be") >= 0 || lower.IndexOf("youtube-nocookie.com") >= 0;
        }

        private static bool IsTwitchUrl(string url)
        {
            string lower = url.ToLowerInvariant();
            return lower.IndexOf("twitch.tv") >= 0 || lower.IndexOf("ttvnw.net") >= 0 || lower.IndexOf("twitchcdn.net") >= 0;
        }

        private static bool IsStreamUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            string lower = url.ToLowerInvariant();

            if (IsTwitchUrl(url))
                return true;

            if (lower.IndexOf(".m3u8") >= 0 || lower.IndexOf(".mpd") >= 0 || lower.IndexOf(".flv") >= 0)
                return true;

            if (IsYouTubeUrl(url) && lower.IndexOf("/live") >= 0)
                return true;

            if (lower.IndexOf("live=1") >= 0 || lower.IndexOf("live_stream=1") >= 0)
                return true;

            return false;
        }

        private static bool IsLiveFormatUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            string lower = url.ToLowerInvariant();
            return lower.IndexOf(".m3u8") >= 0 ||
                   lower.IndexOf(".mpd") >= 0 ||
                   lower.IndexOf("hls_playlist") >= 0;
        }

        private static bool IsLiveUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            string lower = url.ToLowerInvariant();

            if (IsTwitchUrl(url))
            {
                if (lower.IndexOf("/videos/") >= 0 || lower.IndexOf("clips.twitch.tv") >= 0)
                    return false;

                return true;
            }

            if (IsYouTubeUrl(url) && (lower.IndexOf("/live") >= 0 || lower.IndexOf("live=1") >= 0))
                return true;

            if (lower.IndexOf("live=1") >= 0 || lower.IndexOf("live_stream=1") >= 0)
                return true;

            return false;
        }

        private bool ValidateURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            int idx = url.IndexOf("://");
            if (idx < 1 || idx > 8)
            {
                LogError("Invalid URL '" + url + "' provided");
                SetStatusText("Invalid URL");
                _lastErrorDetail = "";
                ShowPlaybackError("This link is not a valid video URL.", "InvalidUrl");
                SendCustomEventDelayedSeconds(nameof(_LateClearStatusInternal), 2f);
                return false;
            }

            if (url.Length > 4096)
            {
                LogError("Video URL is too long! url: '" + url + "'");
                SetStatusText("Invalid URL");
                _lastErrorDetail = "";
                ShowPlaybackError("This link is too long to be a video URL.", "InvalidUrl");
                SendCustomEventDelayedSeconds(nameof(_LateClearStatusInternal), 2f);
                return false;
            }

            if (!url.ToLowerInvariant().StartsWith("https://"))
            {
                LogError("Only https video URLs are allowed: '" + url + "'");
                SetStatusText("Only https links are allowed");
                _lastErrorDetail = "";
                ShowPlaybackError("Only secure https video links are allowed.", "InvalidUrl");
                SendCustomEventDelayedSeconds(nameof(_LateClearStatusInternal), 2f);
                return false;
            }

            return true;
        }

        public void _LateClearStatusInternal()
        {
            if (_screen != null && _screen.IsPlaying && !_loadingVideo)
                SetStatusText("");
        }

        private void LogMessage(string message)
        {
            VoltDebug.Log("[VoltVideo] " + message);
        }

        private void LogWarning(string message)
        {
            VoltDebug.LogWarning("[VoltVideo] " + message);
        }

        private void LogError(string message)
        {
            VoltDebug.LogError("[VoltVideo] " + message);
        }

        public void RegisterControlHandler(VideoControlHandler newControlHandler)
        {
            if (newControlHandler == null)
                return;

            if (_registeredControlHandlers == null)
                _registeredControlHandlers = new VideoControlHandler[0];

            for (int i = 0; i < _registeredControlHandlers.Length; i++)
            {
                if (newControlHandler == _registeredControlHandlers[i])
                    return;
            }

            VideoControlHandler[] newControlHandlers = new VideoControlHandler[_registeredControlHandlers.Length + 1];
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                newControlHandlers[i] = _registeredControlHandlers[i];
            newControlHandlers[newControlHandlers.Length - 1] = newControlHandler;
            _registeredControlHandlers = newControlHandlers;

            newControlHandler.SetLocked(_isMasterOnly);
            newControlHandler.SetLooping(_localLoopVideo);
            newControlHandler.SetPaused(_locallyPaused);
            newControlHandler.SetStatusText(_lastStatusText);
            newControlHandler.SetVolume(_screen != null ? _screen.Volume : _currentVolume);
            newControlHandler.SetMuted(_muted);

            if (IsUsingUnityPlayer())
                newControlHandler.SetToVideoPlayerMode();
            else
                newControlHandler.SetToStreamPlayerMode();
        }

        public void UnregisterControlHandler(VideoControlHandler controlHandler)
        {
            if (_registeredControlHandlers == null || controlHandler == null)
                return;

            int handlerCount = _registeredControlHandlers.Length;
            for (int i = 0; i < handlerCount; i++)
            {
                if (controlHandler == _registeredControlHandlers[i])
                {
                    VideoControlHandler[] newControlHandlers = new VideoControlHandler[handlerCount - 1];

                    int write = 0;
                    for (int j = 0; j < handlerCount; j++)
                    {
                        if (j == i) continue;
                        newControlHandlers[write] = _registeredControlHandlers[j];
                        ++write;
                    }

                    _registeredControlHandlers = newControlHandlers;
                    return;
                }
            }
        }

        private string _lastStatusText = "";

        private void SetStatusText(string statusText)
        {
            if (_registeredControlHandlers == null) return;
            if (statusText == _lastStatusText)
                return;

            _lastStatusText = statusText;

            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetStatusText(statusText);
        }

        private void SetUIPaused(bool paused)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetPaused(paused);
        }

        private void SetUILocked(bool locked)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetLocked(locked);
        }

        private void AddUIUrlHistory(VRCUrl url)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].AddURLToHistory(url);
        }

        private void SetUIToVideoMode()
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetToVideoPlayerMode();
        }

        private void SetUIToStreamMode()
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetToStreamPlayerMode();
        }

        private void SendUIOwnerUpdate()
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].OnVideoPlayerOwnerTransferred();
        }

        private void SetUILooping(bool looping)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetLooping(looping);
        }

        private void SetUIVolume(float volume)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetVolume(volume);
        }

        private void SetUIMuted(bool muted)
        {
            if (_registeredControlHandlers == null) return;
            for (int i = 0; i < _registeredControlHandlers.Length; i++)
                _registeredControlHandlers[i].SetMuted(muted);
        }

        public void RegisterScreenHandler(VideoScreenHandler newScreenHandler)
        {
            if (newScreenHandler == null)
                return;

            if (_registeredScreenHandlers == null)
                _registeredScreenHandlers = new VideoScreenHandler[0];

            for (int i = 0; i < _registeredScreenHandlers.Length; i++)
            {
                if (newScreenHandler == _registeredScreenHandlers[i])
                    return;
            }

            VideoScreenHandler[] newScreenHandlers = new VideoScreenHandler[_registeredScreenHandlers.Length + 1];
            for (int i = 0; i < _registeredScreenHandlers.Length; i++)
                newScreenHandlers[i] = _registeredScreenHandlers[i];
            newScreenHandlers[newScreenHandlers.Length - 1] = newScreenHandler;
            _registeredScreenHandlers = newScreenHandlers;
        }

        public void UnregisterScreenHandler(VideoScreenHandler screenHandler)
        {
            if (_registeredScreenHandlers == null || screenHandler == null)
                return;

            int handlerCount = _registeredScreenHandlers.Length;
            for (int i = 0; i < handlerCount; i++)
            {
                if (screenHandler == _registeredScreenHandlers[i])
                {
                    VideoScreenHandler[] newScreenHandlers = new VideoScreenHandler[handlerCount - 1];

                    int write = 0;
                    for (int j = 0; j < handlerCount; j++)
                    {
                        if (j == i) continue;
                        newScreenHandlers[write] = _registeredScreenHandlers[j];
                        ++write;
                    }

                    _registeredScreenHandlers = newScreenHandlers;
                    return;
                }
            }
        }

        private void UpdateRenderTexture()
        {
            SendCallback("OnVoltVideoRenderTextureChange");
        }

        
        public void RegisterCallbackReceiver(VoltBehaviour callbackReceiver)
        {
            if (callbackReceiver == null)
                return;

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new VoltBehaviour[0];

            for (int i = 0; i < _registeredCallbackReceivers.Length; i++)
            {
                if (callbackReceiver == _registeredCallbackReceivers[i])
                    return;
            }

            VoltBehaviour[] newReceivers = new VoltBehaviour[_registeredCallbackReceivers.Length + 1];
            for (int i = 0; i < _registeredCallbackReceivers.Length; i++)
                newReceivers[i] = _registeredCallbackReceivers[i];
            newReceivers[newReceivers.Length - 1] = callbackReceiver;
            _registeredCallbackReceivers = newReceivers;
        }

        
        public void UnregisterCallbackReceiver(VoltBehaviour callbackReceiver)
        {
            if (_registeredCallbackReceivers == null || callbackReceiver == null)
                return;

            int receiverCount = _registeredCallbackReceivers.Length;
            for (int i = 0; i < receiverCount; i++)
            {
                if (callbackReceiver == _registeredCallbackReceivers[i])
                {
                    VoltBehaviour[] newReceivers = new VoltBehaviour[receiverCount - 1];

                    int write = 0;
                    for (int j = 0; j < receiverCount; j++)
                    {
                        if (j == i) continue;
                        newReceivers[write] = _registeredCallbackReceivers[j];
                        ++write;
                    }

                    _registeredCallbackReceivers = newReceivers;
                    return;
                }
            }
        }

        private void SendCallback(string callbackName)
        {
            if (_registeredCallbackReceivers == null) return;
            for (int i = 0; i < _registeredCallbackReceivers.Length; i++)
            {
                VoltBehaviour receiver = _registeredCallbackReceivers[i];
                if (receiver != null)
                    receiver.SendCustomEvent(callbackName);
            }
        }
    }
}
