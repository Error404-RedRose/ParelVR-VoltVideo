using ParelVR.Volt;
using ParelVR.Volt.Networking;
using ParelVR.Volt.Video;
using ParelVR.Volt.Web;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/Examples/Example Video Player")]
    public class ExampleVideoPlayer : VoltBehaviour
    {
        [VoltEditorOnly]
        public Object inputField;

        [Header("Video Player References")]
        [VoltEditorOnly]
        public Object unityVideo;
        [VoltEditorOnly]
        public Object avProVideo;

        [Header("Sync parameters")]
        public float syncFrequency = 5.0f;
        public float syncThreshold = 1f;

        [VoltSynced]
        private string _syncedURL = "";

        [VoltSynced]
        private int _videoNumber;
        private int _loadedVideoNumber;

        private VoltVideoPlayer _currentPlayer;

        [VoltSynced]
        private bool _ownerPlaying;

        [VoltSynced]
        private int _videoStartNetworkTime;

        private bool _waitForSync;
        private float _lastSyncTime;

        public override void OnStart()
        {
            _currentPlayer = gameObject.GetComponentInChildren<VoltVideoPlayer>();

            if (_currentPlayer != null)
                _currentPlayer.Loop = false;
        }

        public void HandleURLInput()
        {
            if (!IsOwner)
                return;

            _videoNumber++;

            _loadedVideoNumber = _videoNumber;

            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.LoadUrl(new VoltUrl(_syncedURL));
            }

            _ownerPlaying = false;
            _videoStartNetworkTime = 0;

            VoltDebug.Log("Video URL Changed to " + _syncedURL);
        }

        public void StopVideo()
        {
            if (!IsOwner)
                return;

            _videoStartNetworkTime = 0;
            _ownerPlaying = false;

            if (_currentPlayer != null)
                _currentPlayer.Stop();

            _syncedURL = "";
        }

        public override void OnVideoReady(VoltVideoPlayer player)
        {
            if (IsOwner)
            {
                player.Play();
            }
            else
            {
                if (_ownerPlaying)
                {
                    player.Play();
                    SyncVideo();
                }
                else
                {
                    _waitForSync = true;
                }
            }
        }

        public override void OnVideoStarted(VoltVideoPlayer player)
        {
            if (IsOwner)
            {
                _videoStartNetworkTime = VoltNetworking.ServerTimeMs;
                _ownerPlaying = true;
            }
            else if (!_ownerPlaying)
            {
                player.Pause();
                _waitForSync = true;
            }
        }

        public override void OnVideoEnded(VoltVideoPlayer player)
        {
            if (IsOwner)
            {
                _videoStartNetworkTime = 0;
                _ownerPlaying = false;
            }
        }

        public override void OnVideoError(VoltVideoPlayer player, string reason)
        {
            if (_currentPlayer != null)
                _currentPlayer.Stop();

            VoltDebug.LogError("Video failed: " + _syncedURL + " " + reason);
        }

        public override void OnDeserialization()
        {
            if (IsOwner)
                return;

            if (_videoNumber == _loadedVideoNumber)
                return;

            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.LoadUrl(new VoltUrl(_syncedURL));
            }

            SyncVideo();

            _loadedVideoNumber = _videoNumber;

            VoltDebug.Log("Playing synced " + _syncedURL);
        }

        public override void OnUpdate()
        {
            if (IsOwner || !_waitForSync)
            {
                SyncVideoIfTime();
                return;
            }

            if (!_ownerPlaying)
                return;

            if (_currentPlayer != null)
                _currentPlayer.Play();

            _waitForSync = false;

            SyncVideo();
        }

        private void SyncVideoIfTime()
        {
            if (VoltTime.RealTime - _lastSyncTime > syncFrequency)
            {
                _lastSyncTime = VoltTime.RealTime;
                SyncVideo();
            }
        }

        private void SyncVideo()
        {
            if (_currentPlayer == null)
                return;

            float offsetTime = Mathf.Clamp((VoltNetworking.ServerTimeMs - _videoStartNetworkTime) / 1000f, 0f, _currentPlayer.Duration);

            if (Mathf.Abs(_currentPlayer.Time - offsetTime) > syncThreshold)
            {
                _currentPlayer.Seek(offsetTime);
                VoltDebug.Log("Syncing Video to " + offsetTime);
            }
        }
    }
}
