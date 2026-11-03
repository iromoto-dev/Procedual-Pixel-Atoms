// Procedural Pixel Creations for Unity - inspector of PixelCreatureActor.

#nullable enable

using System;
using System.Linq;
using PixelCreatures.Core;
using UnityEditor;
using UnityEngine;

namespace PixelCreatures.Unity.Editor
{
    [CustomEditor(typeof(PixelCreatureActor))]
    [CanEditMultipleObjects]
    public sealed class PixelCreatureActorEditor : UnityEditor.Editor
    {
        private static readonly System.Random SeedRandom = new System.Random();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var familyProp = serializedObject.FindProperty("familyId");
            var seedProp = serializedObject.FindProperty("seed");
            var presetProp = serializedObject.FindProperty("preset");

            EditorGUILayout.PropertyField(presetProp);
            using (new EditorGUI.DisabledScope(presetProp.objectReferenceValue != null))
            {
                var families = PixelCreatureRuntime.Families;
                var labels = families.Select(PixelCreatureRuntime.FamilyLabel).ToArray();
                int index = -1;
                for (int i = 0; i < families.Count; i++) if (families[i].Id == familyProp.stringValue) index = i;
                EditorGUI.showMixedValue = familyProp.hasMultipleDifferentValues;
                int chosen = EditorGUILayout.Popup("Family", index, labels);
                EditorGUI.showMixedValue = false;
                if (chosen != index && chosen >= 0) familyProp.stringValue = families[chosen].Id;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(seedProp);
                    if (GUILayout.Button("Reroll", GUILayout.Width(64)))
                        seedProp.stringValue = SeedRandom.Next(1, int.MaxValue).ToString();
                }
            }
            DrawPropertiesExcluding(serializedObject, "m_Script", "familyId", "seed", "preset");
            serializedObject.ApplyModifiedProperties();

            if (targets.Length != 1) return;
            var actor = (PixelCreatureActor)target;
            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(actor.LastError)) EditorGUILayout.HelpBox(actor.LastError, MessageType.Warning);
            var model = actor.Model;
            if (model != null)
            {
                var g = model.Genome;
                var family = model.Family;
                string arch = g.Schema.Contains("form") ? g.GetChoice("form") : "-";
                string archLabel = family.Archetypes.FirstOrDefault(a => a.Id == arch)?.Label ?? arch;
                var c = model.Anatomy.Canvas.Scaled(actor.EffectiveDensity);
                EditorGUILayout.HelpBox(
                    $"{g.Name}: {archLabel} ({PixelCreatureRuntime.FamilyLabel(family)})\n" +
                    $"{model.Anatomy.Primitives.Count} primitives, {model.Anatomy.Skeleton.Count} bones, sprite {c.Width}x{c.Height} px at density {actor.EffectiveDensity:0.#}" +
                    (Application.isPlaying ? $"\nState: {actor.State}" : string.Empty),
                    MessageType.None);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open in Creation Studio") && model != null) CreationStudioWindow.Open(model.Genome);
                if (GUILayout.Button("Save Preset...") && model != null) SavePreset(actor);
            }
            if (Application.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Action")) actor.TriggerAction();
                    if (GUILayout.Button("Hit")) actor.TriggerHit(Vector2.right);
                    if (GUILayout.Button(actor.Resting ? "Wake" : "Rest")) actor.Resting = !actor.Resting;
                }
                Repaint();
            }
        }

        [MenuItem("GameObject/Procedural Pixel Creations/Pixel Creature Actor", false, 10)]
        private static void CreateActorMenu(MenuCommand command)
        {
            var go = new GameObject("Pixel Creature", typeof(SpriteRenderer));
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            go.AddComponent<PixelCreatureActor>().Seed = SeedRandom.Next(1, int.MaxValue).ToString();
            Undo.RegisterCreatedObjectUndo(go, "Create Pixel Creature");
            Selection.activeGameObject = go;
        }

        private static void SavePreset(PixelCreatureActor actor)
        {
            var genome = actor.Genome;
            if (genome == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Save creation preset", PixelCreatureBaker.SafeName(genome.Name), "json", "ppc.creature preset (JSON)");
            if (string.IsNullOrEmpty(path)) return;
            System.IO.File.WriteAllText(path, PixelCreatureRuntime.SavePreset(genome));
            AssetDatabase.ImportAsset(path);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) return;
            Undo.RecordObject(actor, "Assign creation preset");
            actor.Preset = asset;
            EditorUtility.SetDirty(actor);
        }
    }
}
