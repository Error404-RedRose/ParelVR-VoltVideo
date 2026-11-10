using ParelVR.Volt;
using ParelVR.Volt.Objects;
using UnityEngine;

namespace VoltVideo
{
    [AddComponentMenu("Volt Video/Utilities/Renderer GI Update")]
    public class RendererGIUpdate : VoltBehaviour
    {
        private VoltRenderer targetRenderer;

        public override void OnStart()
        {
            targetRenderer = GetComponent<VoltRenderer>();
        }

        public override void OnUpdate()
        {
        }
    }
}
