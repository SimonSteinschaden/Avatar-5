using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Foundation;
using UnityEngine;

namespace Avatar5.Presentation
{
    /// <summary>Telegraph: ground ring that fills during the announced time. Burst: short ring flash around the origin.</summary>
    public enum CueVisualKind { Projectile, Cone, Telegraph, Burst }

    [Serializable]
    public struct CueVisual
    {
        public int CueId;
        public CueVisualKind Kind;
        public Color Color;
        public float Speed;
        public float Range;
        public float ConeHalfAngleDegrees;
        /// <summary>Telegraph length in seconds (matches the enemy definition).</summary>
        public float Duration;
    }

    /// <summary>
    /// Greybox ability visuals driven only by host <see cref="PresentationCue"/>s: a sphere flies for projectile
    /// cues and vanishes on the matching end cue (negative id), a short flash marks cone casts. Purely local.
    /// </summary>
    public sealed class CombatCueView : MonoBehaviour
    {
        private const float CastHeight = 1f;

        [SerializeField] private CueVisual[] visuals = new CueVisual[0];

        private sealed class Flying
        {
            public int CueId;
            public Transform Body;
            public Vector3 Direction;
            public float Speed;
            public float Remaining;
        }

        private sealed class Fading
        {
            public Transform Body;
            public Material Material;
            public Color Color;
            public float Age;
            public float Lifetime = FadeSeconds;
            /// <summary>Telegraphs grow an inner disc to show when the hit lands.</summary>
            public Transform Fill;
            public float FullScale;
        }

        private const float FadeSeconds = 0.35f;
        private readonly List<Flying> _flying = new List<Flying>();
        private readonly List<Fading> _fading = new List<Fading>();

        public int ActiveProjectiles { get { return _flying.Count; } }

        public void Configure(CueVisual[] cueVisuals)
        {
            visuals = cueVisuals ?? new CueVisual[0];
        }

        private void OnEnable()
        {
            PresentationBus.CueRaised += OnCue;
        }

        private void OnDisable()
        {
            PresentationBus.CueRaised -= OnCue;
        }

        private void OnCue(PresentationCue cue)
        {
            var origin = new Vector3(cue.Position.X, CastHeight, cue.Position.Z);
            var direction = new Vector3(cue.Direction.X, 0f, cue.Direction.Z);
            if (cue.CueId < 0)
            {
                EndProjectile(-cue.CueId, origin);
                return;
            }
            CueVisual visual;
            if (!TryFind(cue.CueId, out visual)) return;
            if (visual.Kind == CueVisualKind.Projectile)
            {
                var body = CreatePrimitive(PrimitiveType.Sphere, visual.Color, out _);
                body.localScale = Vector3.one * 0.45f;
                body.position = origin;
                _flying.Add(new Flying { CueId = cue.CueId, Body = body, Direction = direction, Speed = visual.Speed, Remaining = visual.Range });
            }
            else if (visual.Kind == CueVisualKind.Telegraph || visual.Kind == CueVisualKind.Burst)
            {
                Material material;
                var body = CreatePrimitive(PrimitiveType.Cylinder, visual.Color, out material);
                var diameter = visual.Range * 2f;
                body.localScale = new Vector3(diameter, 0.02f, diameter);
                body.position = new Vector3(origin.x, 0.05f, origin.z);
                var fading = new Fading { Body = body, Material = material, Color = visual.Color };
                if (visual.Kind == CueVisualKind.Telegraph)
                {
                    Material fillMaterial;
                    var fill = CreatePrimitive(PrimitiveType.Cylinder, new Color(visual.Color.r, visual.Color.g * 0.3f, visual.Color.b * 0.3f, 1f), out fillMaterial);
                    fill.SetParent(body, false);
                    fill.localPosition = new Vector3(0f, 0.6f, 0f);
                    fill.localScale = new Vector3(0.01f, 1f, 0.01f);
                    fading.Fill = fill;
                    fading.Lifetime = Mathf.Max(0.3f, visual.Duration);
                }
                _fading.Add(fading);
            }
            else
            {
                Material material;
                var body = CreatePrimitive(PrimitiveType.Cylinder, visual.Color, out material);
                var width = 2f * visual.Range * Mathf.Sin(visual.ConeHalfAngleDegrees * Mathf.Deg2Rad);
                body.localScale = new Vector3(Mathf.Max(width, 0.5f), 0.05f, visual.Range);
                body.position = new Vector3(origin.x, 0.1f, origin.z) + direction * (visual.Range * 0.5f);
                if (direction.sqrMagnitude > 1e-4f) body.rotation = Quaternion.LookRotation(direction, Vector3.up);
                _fading.Add(new Fading { Body = body, Material = material, Color = visual.Color });
            }
        }

        private void EndProjectile(int cueId, Vector3 at)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _flying.Count; i++)
            {
                if (_flying[i].CueId != cueId) continue;
                var d = (_flying[i].Body.position - at).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            if (best < 0) return;
            Destroy(_flying[best].Body.gameObject);
            _flying.RemoveAt(best);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            for (var i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                var step = Mathf.Min(f.Speed * dt, f.Remaining);
                f.Body.position += f.Direction * step;
                f.Remaining -= step;
                // Safety: the end cue may arrive late or be lost when the host quits.
                if (f.Remaining <= 0f)
                {
                    Destroy(f.Body.gameObject);
                    _flying.RemoveAt(i);
                }
            }
            for (var i = _fading.Count - 1; i >= 0; i--)
            {
                var f = _fading[i];
                f.Age += dt;
                var progress = f.Age / f.Lifetime;
                if (f.Fill != null)
                {
                    // Telegraph stays solid and fills up; the hit lands when the inner disc reaches the rim.
                    var k = Mathf.Clamp01(progress);
                    f.Fill.localScale = new Vector3(k, 1f, k);
                }
                else
                {
                    var c = f.Color;
                    c.a = Mathf.Lerp(f.Color.a, 0f, progress);
                    f.Material.color = c;
                }
                if (f.Age >= f.Lifetime)
                {
                    Destroy(f.Material);
                    Destroy(f.Body.gameObject);
                    _fading.RemoveAt(i);
                }
            }
        }

        private bool TryFind(int cueId, out CueVisual visual)
        {
            foreach (var v in visuals)
            {
                if (v.CueId == cueId) { visual = v; return true; }
            }
            visual = default(CueVisual);
            return false;
        }

        private Transform CreatePrimitive(PrimitiveType type, Color color, out Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "Cue_" + type;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
            go.transform.SetParent(transform, true);
            return go.transform;
        }
    }
}
