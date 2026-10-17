using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Foundation;
using UnityEngine;

namespace Avatar5.Presentation
{
    /// <summary>World-space health bar plus status tint for one actor. Reads only <see cref="ICombatStateSource"/>.</summary>
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private Renderer tintTarget;
        [SerializeField] private float height = 2.3f;

        private ICombatStateSource _source;
        private Transform _fill;
        private Transform _back;
        private Material _fillMaterial;
        private Material _tintMaterial;
        private Color _baseColor;

        private void Awake()
        {
            _source = GetComponent<ICombatStateSource>();
            _back = CreateBar("HealthBack", new Color(0.1f, 0.1f, 0.1f), out _);
            _fill = CreateBar("HealthFill", new Color(0.2f, 0.85f, 0.25f), out _fillMaterial);
            if (tintTarget != null)
            {
                _tintMaterial = new Material(tintTarget.sharedMaterial);
                tintTarget.sharedMaterial = _tintMaterial;
                _baseColor = _tintMaterial.color;
            }
        }

        /// <summary>Changes the untinted colour (e.g. class colour); status tints are applied on top.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
        }

        private void OnDestroy()
        {
            if (_fillMaterial != null) Destroy(_fillMaterial);
            if (_tintMaterial != null) Destroy(_tintMaterial);
        }

        private void LateUpdate()
        {
            if (_source == null) return;
            var s = _source.CombatState;
            var ratio = s.MaxHealth > 0f ? Mathf.Clamp01(s.Health / s.MaxHealth) : 0f;
            var cam = Camera.main;
            var rotation = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
            var anchor = transform.position + Vector3.up * height;
            _back.SetPositionAndRotation(anchor, rotation);
            _back.localScale = new Vector3(1.2f, 0.12f, 0.02f);
            _fill.SetPositionAndRotation(anchor - rotation * Vector3.right * (0.6f * (1f - ratio)) - rotation * Vector3.forward * 0.015f, rotation);
            _fill.localScale = new Vector3(1.2f * ratio, 0.1f, 0.02f);
            _fillMaterial.color = ratio > 0.35f ? new Color(0.2f, 0.85f, 0.25f) : new Color(0.9f, 0.25f, 0.2f);

            if (_tintMaterial == null) return;
            var tint = _baseColor;
            if ((s.Status & StatusFlags.Burning) != 0) tint = Color.Lerp(_baseColor, new Color(1f, 0.45f, 0.05f), 0.6f);
            else if ((s.Status & StatusFlags.Wet) != 0) tint = Color.Lerp(_baseColor, new Color(0.2f, 0.5f, 1f), 0.6f);
            if (s.Defeated) tint = Color.Lerp(_baseColor, Color.black, 0.7f);
            _tintMaterial.color = tint;
        }

        private Transform CreateBar(string name, Color color, out Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
