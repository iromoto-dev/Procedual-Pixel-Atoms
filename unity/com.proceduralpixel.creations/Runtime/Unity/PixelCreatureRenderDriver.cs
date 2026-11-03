// Procedural Pixel Creations for Unity - hidden driver that flushes the render queue after all Updates.

#nullable enable

using UnityEngine;

namespace PixelCreatures.Unity
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(10000)]
    internal sealed class PixelCreatureRenderDriver : MonoBehaviour
    {
        private void LateUpdate() => PixelCreatureRenderQueue.Flush();

        private void OnDestroy() => PixelCreatureRenderQueue.DriverDestroyed(this);
    }
}
