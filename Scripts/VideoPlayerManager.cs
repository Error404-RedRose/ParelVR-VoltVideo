using ParelVR.Volt;
using ParelVR.Volt.Video;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/Internal/Video Player Manager")]
    public class VideoPlayerManager : VoltBehaviour
    {
        public VoltVideoController receiver;

        [VoltEditorOnly] public Object unityVideoPlayer;
        [VoltEditorOnly] public Object avProPlayer;
        [VoltEditorOnly] public Renderer unityTextureRenderer;
        [VoltEditorOnly] public Renderer avProTextureRenderer;
        [VoltEditorOnly] public AudioSource[] audioSources;

        private VoltVideoPlayer _currentPlayer;
        private bool _initialized;
        private bool _muted;
        private float _currentVolume = 1f;

        public override void OnStart()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            _currentPlayer = FindPlayer();
            _initialized = true;
        }

        private VoltVideoPlayer FindPlayer()
        {
            VoltVideoPlayer found = gameObject.GetComponentInChildren<VoltVideoPlayer>();
            if (found != null)
                return found;

            if (transform.Parent != null)
            {
                found = transform.Parent.GameObject.GetComponentInChildren<VoltVideoPlayer>();
                if (found != null)
                    return found;
            }

            return null;
        }

        private VoltVideoController FindReceiver()
        {
            if (receiver != null)
                return receiver;

            receiver = gameObject.GetComponentInParent<VoltVideoController>();
            if (receiver == null)
                receiver = gameObject.GetComponentInChildren<VoltVideoController>();

            return receiver;
        }

        public void OnVideoEnd()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.OnVideoEnd();
        }

        public void NotifyVideoError(VideoError videoError)
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target._OnVideoErrorCallback(videoError);
        }

        public void OnVideoLoop()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.OnVideoLoop();
        }

        public void OnVideoPause()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.OnVideoPause();
        }

        public void OnVideoPlay()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.OnVideoPlay();
        }

        public void NotifyVideoReady()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.NotifyVideoReady();
        }

        public void OnVideoStart()
        {
            VoltVideoController target = FindReceiver();
            if (target != null)
                target.OnVideoStart();
        }

        public void Play()
        {
            if (_currentPlayer != null)
                _currentPlayer.Play();
        }

        public void Pause()
        {
            if (_currentPlayer != null)
                _currentPlayer.Pause();
        }

        public void Stop()
        {
            if (_currentPlayer != null)
                _currentPlayer.Stop();
        }

        public float GetTime()
        {
            return _currentPlayer != null ? _currentPlayer.Time : 0f;
        }

        public float GetDuration()
        {
            return _currentPlayer != null ? _currentPlayer.Duration : 0f;
        }

        public bool IsPlaying()
        {
            return _currentPlayer != null && _currentPlayer.IsPlaying;
        }

        public void LoadURL(VRCUrl url)
        {
            if (_currentPlayer != null && url != null)
                _currentPlayer.LoadUrl(new ParelVR.Volt.Web.VoltUrl(url.Get()));
        }

        public void SetTime(float time)
        {
            if (_currentPlayer != null)
                _currentPlayer.Seek(time);
        }

        public void SetLooping(bool loop)
        {
            if (_currentPlayer != null)
                _currentPlayer.Loop = loop;
        }

        public void SetToStreamPlayerMode()
        {
        }

        public void SetToVideoPlayerMode()
        {
        }

        public object GetVideoTexture()
        {
            return null;
        }

        public float GetVolume()
        {
            return _currentVolume;
        }

        public bool IsMuted()
        {
            return _muted;
        }

        public void SetVolume(float volume)
        {
            if (!_muted && _currentPlayer != null)
                _currentPlayer.Volume = volume;

            _currentVolume = volume;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;

            if (_currentPlayer != null)
                _currentPlayer.Volume = muted ? 0f : _currentVolume;
        }
    }
}
