using Avatar5.Contracts;
using Avatar5.Gameplay.Combat;
using UnityEngine;

namespace Avatar5.Combat
{
    /// <summary>Authoring asset for one ability. All balancing lives here; the host converts it to a validated runtime definition.</summary>
    [CreateAssetMenu(menuName = "Avatar5/Ability Definition", fileName = "Ability")]
    public sealed class AbilityDefinitionAsset : ScriptableObject
    {
        [SerializeField] private int id;
        [SerializeField] private string key;
        [SerializeField] private ElementKind element;
        [SerializeField] private AbilityTargeting targeting;
        [SerializeField, Min(0f)] private float energyCost;
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField, Min(0.1f)] private float range = 10f;
        [SerializeField, Range(1f, 180f)] private float coneHalfAngleDegrees = 45f;
        [SerializeField, Min(0f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0f)] private float projectileRadius = 0.3f;
        [SerializeField, Min(0f)] private float damage;
        [SerializeField, Min(0f)] private float knockback;
        [SerializeField] private StatusFlags appliesStatus;
        [SerializeField, Min(0f)] private float statusDuration;
        [SerializeField, Min(0f)] private float statusDamagePerSecond;
        [SerializeField] private int cueId;

        public int Id { get { return id; } }

        public AbilityDefinition ToRuntime()
        {
            return new AbilityDefinition
            {
                Id = id, Key = key, Element = element, Targeting = targeting, EnergyCost = energyCost, Cooldown = cooldown,
                Range = range, ConeHalfAngleDegrees = coneHalfAngleDegrees, ProjectileSpeed = projectileSpeed,
                ProjectileRadius = projectileRadius, Damage = damage, Knockback = knockback, AppliesStatus = appliesStatus,
                StatusDuration = statusDuration, StatusDamagePerSecond = statusDamagePerSecond, CueId = cueId
            };
        }

        /// <summary>Editor/setup helper: copies a runtime definition into this asset.</summary>
        public void CopyFrom(AbilityDefinition source)
        {
            id = source.Id; key = source.Key; element = source.Element; targeting = source.Targeting;
            energyCost = source.EnergyCost; cooldown = source.Cooldown; range = source.Range;
            coneHalfAngleDegrees = source.ConeHalfAngleDegrees > 0f ? source.ConeHalfAngleDegrees : 45f;
            projectileSpeed = source.ProjectileSpeed; projectileRadius = source.ProjectileRadius; damage = source.Damage;
            knockback = source.Knockback; appliesStatus = source.AppliesStatus; statusDuration = source.StatusDuration;
            statusDamagePerSecond = source.StatusDamagePerSecond; cueId = source.CueId;
        }
    }
}
