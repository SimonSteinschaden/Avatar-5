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
        private string _banner;
        private float _bannerUntil;
        private GUIStyle _style;

        private ElementKind _localElement;

        public void SetLocalPlayer(ICombatStateSource local)
        {
            _local = local;
        }

        private IProgressionSource _progress;

        public void SetLocalProgress(IProgressionSource progress)
        {
            _progress = progress;
        }

        public void SetLocalElement(ElementKind element)
        {
            _localElement = element;
        }

        private static string AbilityHint(ElementKind element)
        {
            switch (element)
            {
                case ElementKind.Water: return "LMB Eissplitter · RMB Wasserwelle";
                case ElementKind.Earth: return "LMB Steinspieß · RMB Beben";
                case ElementKind.Air: return "LMB Windklinge · RMB Böe";
                default: return "LMB Feuerball · RMB Flammenstoß";
            }
        }

        private void OnEnable()
        {
            PresentationBus.CombatEventRaised += OnCombatEvent;
            PresentationBus.WorldEventRaised += OnWorldEvent;
        }

        private void OnWorldEvent(WorldEvent e)
        {
            if (e.StateCode == WorldStateCodes.LevelCompletedCode) Show("Level geschafft!", 8f);
            else if (e.StateCode == WorldStateCodes.On) Show("Etwas hat sich geöffnet …", 2f);
        }

        private void Show(string text, float seconds)
        {
            _banner = text;
            _bannerUntil = Time.time + seconds;
        }

        private void OnDisable()
        {
            PresentationBus.CombatEventRaised -= OnCombatEvent;
            PresentationBus.WorldEventRaised -= OnWorldEvent;
        }

        private void OnCombatEvent(CombatEvent e)
        {
            switch ((CombatEventKind)e.ResultKind)
            {
                case CombatEventKind.DamageApplied:
                    Add(e.TargetActorId, Mathf.RoundToInt(e.Amount).ToString(), e.AbilityId == 0 ? new Color(1f, 0.55f, 0.1f) : Color.white);
                    break;
                case CombatEventKind.ReactionTriggered:
                    Add(e.TargetActorId, ReactionText((ReactionKind)e.EffectId), new Color(0.6f, 0.9f, 1f));
                    break;
                case CombatEventKind.ActorDefeated:
                    Add(e.TargetActorId, "Besiegt", Color.red);
                    break;
            }
        }

        private static string ReactionText(ReactionKind kind)
        {
            switch (kind)
            {
                case ReactionKind.Steam: return "Dampf!";
                case ReactionKind.Extinguish: return "Gelöscht!";
                case ReactionKind.Freeze: return "Eingefroren!";
                case ReactionKind.Flare: return "Aufgeflammt!";
                default: return "Reaktion!";
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

            if (!string.IsNullOrEmpty(_banner) && Time.time < _bannerUntil)
            {
                _style.normal.textColor = Color.white;
                GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 40f), _banner, _style);
            }

            if (_local == null) return;
            var s = _local.CombatState;
            GUILayout.BeginArea(new Rect(Screen.width - 300f, Screen.height - 150f, 280f, 130f), GUI.skin.box);
            GUILayout.Label("Leben: " + Mathf.CeilToInt(s.Health) + " / " + Mathf.RoundToInt(s.MaxHealth));
            GUILayout.Label("Energie: " + Mathf.FloorToInt(s.Energy) + " / " + Mathf.RoundToInt(s.MaxEnergy));
            if (_progress != null)
            {
                var p = _progress.Progress;
                if (p.Level > 0)
                {
                    GUILayout.Label("Stufe " + p.Level + " · XP " + p.Xp + " / " + p.XpForNextLevel
                        + (p.FreePoints > 0 ? " · " + p.FreePoints + " Talentpunkt(e)" : string.Empty));
                    GUILayout.Label("Talente: Kraft " + p.PowerRank + " · Fokus " + p.FocusRank + " · Zähigkeit " + p.VigorRank
                        + (p.FreePoints > 0 ? "  (Tasten 1/2/3)" : string.Empty));
                }
            }
            GUILayout.Label(AbilityHint(_localElement) + " · E Benutzen" + (s.Status != StatusFlags.None ? " · " + s.Status : string.Empty));
            GUILayout.EndArea();
        }
    }
}
