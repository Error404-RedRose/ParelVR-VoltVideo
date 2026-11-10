using ParelVR.Volt;
using ParelVR.Volt.Interaction;
using ParelVR.Volt.Objects;
using ParelVR.Volt.UI;
using UnityEngine;

namespace VoltVideo.Internal
{
    [AddComponentMenu("Volt Video/Internal/Object Toggle")]
    public class ObjectToggle : VoltBehaviour
    {
#pragma warning disable CS0649
        [SerializeField, VoltEditorOnly]
        private GameObject[] toggleObjects;
#pragma warning restore CS0649

        public bool isVideoControlsController;
        public float fadeDuration = 0.6f;
        public bool startHidden = false;

        private VoltGameObject _controls;
        private VoltImage[] _images;
        private VoltText[] _texts;
        private float[] _imageAlphas;
        private float[] _textAlphas;
        private VoltInteractable _screenInteractable;

        private bool _ready;
        private float _alpha = 1f;
        private int _targetAlpha = 1;
        private VoltVideoController _player;

        public override void OnStart()
        {
            if (!isVideoControlsController)
                return;

            VoltTransform parent = transform.Parent;

            if (parent != null)
            {
                VoltTransform controlsTransform = parent.Find("ControlsUI");
                if (controlsTransform != null)
                    _controls = controlsTransform.GameObject;
            }

            if (_controls == null)
            {
                VoltDebug.LogWarning("[ObjectToggle] Could not find ControlsUI under the video root on " + name + ".");
                return;
            }

            _images = _controls.GetComponentsInChildren<VoltImage>();
            _texts = _controls.GetComponentsInChildren<VoltText>();

            if (_images == null)
                _images = new VoltImage[0];

            if (_texts == null)
                _texts = new VoltText[0];

            _imageAlphas = new float[_images.Length];
            _textAlphas = new float[_texts.Length];

            for (int i = 0; i < _images.Length; i++)
                _imageAlphas[i] = _images[i] != null ? _images[i].Color.a : 1f;

            for (int i = 0; i < _texts.Length; i++)
                _textAlphas[i] = _texts[i] != null ? _texts[i].Color.a : 1f;

            _screenInteractable = gameObject.GetComponent<VoltInteractable>();

            _ready = true;
            _alpha = 1f;
            _targetAlpha = 1;

            SetScreenInteraction(false);

            bool hidden = startHidden;

            VoltVideoController player = GetPlayer();
            if (player != null && player.AreControlsHidden())
                hidden = true;

            if (hidden)
            {
                _alpha = 0f;
                _targetAlpha = 0;
                ApplyAlpha(0f);
                _controls.SetActive(false);
                SetScreenInteraction(true);
            }
        }

        public bool StartsHidden()
        {
            return startHidden;
        }

        public void ApplyHiddenState(bool hidden)
        {
            if (hidden)
                HideControls();
            else
                ShowControls();
        }

        private VoltVideoController GetPlayer()
        {
            if (_player != null)
                return _player;

            _player = gameObject.GetComponentInParent<VoltVideoController>();
            return _player;
        }

        public override void OnUpdate()
        {
            if (!isVideoControlsController || !_ready || _controls == null)
                return;

            if (_alpha == _targetAlpha)
                return;

            float speed = fadeDuration > 0.01f ? 1f / fadeDuration : 1000f;
            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, speed * VoltTime.DeltaTime);

            ApplyAlpha(_alpha);

            if (_targetAlpha == 0 && _alpha <= 0.001f)
            {
                _alpha = 0f;
                ApplyAlpha(0f);
                _controls.SetActive(false);
                SetScreenInteraction(true);
            }
            else if (_targetAlpha == 1 && _alpha >= 0.999f)
            {
                _alpha = 1f;
                ApplyAlpha(1f);
                SetScreenInteraction(false);
            }
        }

        public void OnToggle()
        {
            if (isVideoControlsController)
            {
                ToggleControls();
                return;
            }

            VoltTransform controls = transform.Parent;
            if (controls == null)
                return;

            VoltTransform videoRoot = controls.Parent;
            if (videoRoot == null)
                return;

            VoltTransform screen = videoRoot.Find("VideoScreen");
            if (screen == null)
                return;

            ObjectToggle controller = screen.GetComponent<ObjectToggle>();
            if (controller != null)
                controller.ToggleControls();
        }

        public void OnScreenClicked()
        {
            if (!isVideoControlsController)
                return;

            if (_controls == null)
                return;

            if (!_controls.ActiveSelf || _alpha <= 0.01f)
            {
                VoltVideoController player = GetPlayer();
                if (player != null)
                    player.SetControlsHidden(false);
                else
                    ShowControls();
            }
        }

        public void ToggleControls()
        {
            if (!isVideoControlsController)
                return;

            VoltVideoController player = GetPlayer();
            if (player != null)
            {
                player.SetControlsHidden(_targetAlpha == 1);
                return;
            }

            if (_targetAlpha == 1)
                HideControls();
            else
                ShowControls();
        }

        public void HideControls()
        {
            if (!isVideoControlsController || _controls == null)
                return;

            if (_targetAlpha == 0 && _alpha <= 0.001f)
            {
                SetScreenInteraction(true);
                return;
            }

            _targetAlpha = 0;
            SetScreenInteraction(false);

            if (fadeDuration <= 0.01f)
            {
                _alpha = 0f;
                ApplyAlpha(0f);
                _controls.SetActive(false);
                SetScreenInteraction(true);
            }
        }

        public void ShowControls()
        {
            if (!isVideoControlsController || _controls == null)
                return;

            if (!_controls.ActiveSelf)
                _controls.SetActive(true);

            _targetAlpha = 1;
            SetScreenInteraction(false);

            if (fadeDuration <= 0.01f)
            {
                _alpha = 1f;
                ApplyAlpha(1f);
                SetScreenInteraction(false);
            }
        }

        private void SetScreenInteraction(bool interactable)
        {
            if (_screenInteractable != null)
                _screenInteractable.Interactable = interactable;
        }

        private void ApplyAlpha(float alpha)
        {
            for (int i = 0; i < _images.Length; i++)
            {
                VoltImage image = _images[i];
                if (image == null)
                    continue;

                Color color = image.Color;
                color.a = _imageAlphas[i] * alpha;
                image.Color = color;
            }

            for (int i = 0; i < _texts.Length; i++)
            {
                VoltText text = _texts[i];
                if (text == null)
                    continue;

                Color color = text.Color;
                color.a = _textAlphas[i] * alpha;
                text.Color = color;
            }
        }
    }
}
