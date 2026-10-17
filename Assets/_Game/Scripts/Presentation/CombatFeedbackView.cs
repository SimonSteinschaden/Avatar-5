using System;
using System.Collections.Generic;
using Avatar5.Contracts;
using Avatar5.Foundation;
using UnityEngine;

namespace Avatar5.Presentation
{
    /// <summary>Floating damage/reaction text and the local player's combat HUD (debug OnGUI until P07 UI).</summary>
    public sealed class CombatFeedbackView : MonoBehaviour
    {
        private sealed class Popup
        {
            public ulong ActorId;
            public string Text;
            public Color Color;
            public float Age;
        }

        private const float PopupSeconds = 1.1f;
        private readonly List<Popup> _popups = new List<Popup>();
        private ICombatStateSource _local;
        private GUIStyle _style;

        public void SetLocalPlayer(ICombatStateSource local)
        {
            _local = local;
        }

        private void OnEnable()
        {
            PresentationBus.CombatEventRaised += OnCombatEvent;
        }

        private void OnDisable()
        {
            PresentationBus.CombatEventRaised -= OnCombatEvent;
        }

        private void OnCombatEvent(CombatEvent e)
        {
            switch ((CombatEventKind)e.ResultKind)
            {
                case CombatEventKind.DamageApplied:
                    Add(e.TargetActorId, Mathf.RoundToInt(e.Amount).ToString(), e.AbilityId == 0 ? new Color(1f, 0.55f, 0.1f) : Color.white);
                    break;
                case CombatEventKind.ReactionTriggered:
                    Add(e.TargetActorId, e.EffectId == (int)ReactionKind.Steam ? "Dampf!" : "Gelöscht!", new Color(0.6f, 0.9f, 1f));
                    break;
                case CombatEventKind.ActorDefeated:
                    Add(e.TargetActorId, "Besiegt", Color.red);
                    break;
            }
        }

        private void Add(ulong actorId, string text, Color color)
        {
            _popups.Add(new Popup { ActorId = actorId, Text = text, Color = color });
            if (_popups.Count > 40) _popups.RemoveAt(0);
        }

        private void Update()
        {
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                _popups[i].Age += Time.deltaTime;
                if (_popups[i].Age > PopupSeconds) _popups.RemoveAt(i);
            }
        }

        private void OnGUI()
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var popup in _popups)
                {
                    Transform actor;
                    if (!ActorRegistry.TryGet(popup.ActorId, out actor)) continue;
                    var screen = cam.WorldToScreenPoint(actor.position + Vector3.up * (2.2f + popup.Age));
                    if (screen.z < 0f) continue;
                    _style.normal.textColor = popup.Color;
                    GUI.Label(new Rect(screen.x - 60f, Screen.height - screen.y - 12f, 120f, 24f), popup.Text, _style);
                }
            }

            if (_local == null) return;
            var s = _local.CombatState;
            GUILayout.BeginArea(new Rect(Screen.width - 260f, Screen.height - 110f, 240f, 90f), GUI.skin.box);
            GUILayout.Label("Leben: " + Mathf.CeilToInt(s.Health) + " / " + Mathf.RoundToInt(s.MaxHealth));
            GUILayout.Label("Energie: " + Mathf.FloorToInt(s.Energy) + " / " + Mathf.RoundToInt(s.MaxEnergy));
            GUILayout.Label("LMB Feuerball · RMB Wasserwelle" + (s.Status != StatusFlags.None ? " · " + s.Status : string.Empty));
            GUILayout.EndArea();
        }
    }
}
