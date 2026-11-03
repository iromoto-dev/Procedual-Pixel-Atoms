// Procedural Pixel Creations for Unity - keeps edit mode previews out of player builds.
//
// In edit mode every PixelCreatureActor shows a preview sprite that is created at runtime (HideFlags.DontSave).
// A scene processed for a player build must not reference it, so the previews are released before the scene
// is written into the build; in the player the actors render their own sprites as usual.

#nullable enable

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace PixelCreatures.Unity.Editor
{
    internal sealed class PixelCreaturesSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // also called when entering play mode; only player builds need the cleanup
            if (!BuildPipeline.isBuildingPlayer) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var actor in root.GetComponentsInChildren<PixelCreatureActor>(true))
                    actor.ReleasePreview();
        }
    }
}
