using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>Data-driven definition of every Avatar5 toon material variant (own work).</summary>
    public static class ToonMaterialLibrary
    {
        public const string MaterialFolder = "Assets/_Game/Rendering/Materials";
        public const string ToonShaderName = "Avatar5/Toon";
        public const string WaterShaderName = "Avatar5/Toon Water";
        public const string HullShaderName = "Avatar5/Toon Hull Outline";

        public sealed class Definition
        {
            public string Name;
            public string Family;
            public Color Base;
            public Color Shade;
            public int Bands = 3;
            public float Softness = 0.02f;
            public float ShadeOffset;
            public float Ambient = 0.5f;
            public Color Specular = Color.black;
            public float SpecularSize = 0.05f;
            public float RimStrength;
            public Color Rim = Color.white;
            public float Noise = 0.06f;
            public float NoiseScale = 1.6f;
            public float ColorVariation = 0.04f;
            public Color Emission = Color.black;
            public float InnerLineWeight = 1f;
            public bool ExcludeFromInk;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>All palette materials. Family = Fire, Water, Earth, Air, Neutral, Environment, Character.</summary>
        public static readonly Definition[] Definitions =
        {
            // Fire: warm orange / red / yellow; shade shifts toward plum so shadows never go muddy brown.
            new Definition { Name = "Toon_Fire_Orange", Family = "Fire", Base = C(1f, 0.5f, 0.12f), Shade = C(0.74f, 0.42f, 0.55f), RimStrength = 0.25f, Rim = C(1f, 0.85f, 0.5f) },
            new Definition { Name = "Toon_Fire_Red", Family = "Fire", Base = C(0.86f, 0.16f, 0.12f), Shade = C(0.62f, 0.42f, 0.68f), RimStrength = 0.2f, Rim = C(1f, 0.6f, 0.4f) },
            new Definition { Name = "Toon_Fire_Yellow", Family = "Fire", Base = C(1f, 0.82f, 0.22f), Shade = C(0.82f, 0.56f, 0.5f) },
            new Definition { Name = "Toon_Fire_Flame", Family = "Fire", Base = C(1f, 0.78f, 0.25f), Shade = C(1f, 0.6f, 0.4f), Bands = 2, Noise = 0f, Emission = new Color(1.6f, 0.62f, 0.12f), InnerLineWeight = 0f },
            // Water: blue / turquoise.
            new Definition { Name = "Toon_Water_Blue", Family = "Water", Base = C(0.13f, 0.42f, 0.86f), Shade = C(0.55f, 0.55f, 0.82f), Specular = C(0.6f, 0.8f, 1f), SpecularSize = 0.06f },
            new Definition { Name = "Toon_Water_Turquoise", Family = "Water", Base = C(0.1f, 0.72f, 0.72f), Shade = C(0.5f, 0.62f, 0.82f), Specular = C(0.7f, 1f, 1f), SpecularSize = 0.08f, RimStrength = 0.3f, Rim = C(0.75f, 1f, 1f) },
            // Earth: ochre / brown / sandstone.
            new Definition { Name = "Toon_Earth_Ochre", Family = "Earth", Base = C(0.8f, 0.58f, 0.2f), Shade = C(0.64f, 0.5f, 0.6f), Noise = 0.09f, ColorVariation = 0.06f },
            new Definition { Name = "Toon_Earth_Brown", Family = "Earth", Base = C(0.46f, 0.29f, 0.16f), Shade = C(0.62f, 0.5f, 0.64f), Noise = 0.09f, ColorVariation = 0.06f },
            new Definition { Name = "Toon_Earth_Sandstone", Family = "Earth", Base = C(0.87f, 0.73f, 0.53f), Shade = C(0.68f, 0.57f, 0.68f), Noise = 0.11f, NoiseScale = 1.1f, ColorVariation = 0.07f },
            // Air: light blue / white / turquoise.
            new Definition { Name = "Toon_Air_LightBlue", Family = "Air", Base = C(0.62f, 0.82f, 0.98f), Shade = C(0.62f, 0.66f, 0.88f), RimStrength = 0.35f },
            new Definition { Name = "Toon_Air_White", Family = "Air", Base = C(0.95f, 0.95f, 0.92f), Shade = C(0.66f, 0.7f, 0.86f), RimStrength = 0.2f },
            new Definition { Name = "Toon_Air_Turquoise", Family = "Air", Base = C(0.45f, 0.9f, 0.84f), Shade = C(0.55f, 0.68f, 0.86f), RimStrength = 0.3f },
            // Neutral: wood / stone / metal / cloth.
            new Definition { Name = "Toon_Neutral_Wood", Family = "Neutral", Base = C(0.63f, 0.41f, 0.23f), Shade = C(0.6f, 0.48f, 0.62f), Noise = 0.12f, NoiseScale = 3f, ColorVariation = 0.08f },
            new Definition { Name = "Toon_Neutral_Stone", Family = "Neutral", Base = C(0.62f, 0.62f, 0.6f), Shade = C(0.56f, 0.57f, 0.74f), Noise = 0.1f, NoiseScale = 1.2f, ColorVariation = 0.06f },
            new Definition { Name = "Toon_Neutral_Metal", Family = "Neutral", Base = C(0.62f, 0.66f, 0.72f), Shade = C(0.45f, 0.5f, 0.68f), Bands = 4, Specular = C(0.95f, 0.95f, 0.88f), SpecularSize = 0.09f, RimStrength = 0.45f, Noise = 0.02f },
            new Definition { Name = "Toon_Neutral_Cloth", Family = "Neutral", Base = C(0.33f, 0.27f, 0.56f), Shade = C(0.62f, 0.52f, 0.76f), Softness = 0.04f, Noise = 0.05f, NoiseScale = 2.5f },
            // Environment.
            new Definition { Name = "Toon_Env_Grass", Family = "Environment", Base = C(0.45f, 0.67f, 0.33f), Shade = C(0.56f, 0.62f, 0.78f), Noise = 0.1f, NoiseScale = 0.6f, ColorVariation = 0.07f, InnerLineWeight = 0.7f },
            new Definition { Name = "Toon_Env_Soil", Family = "Environment", Base = C(0.5f, 0.38f, 0.27f), Shade = C(0.6f, 0.5f, 0.64f), Noise = 0.1f, NoiseScale = 1.4f, ColorVariation = 0.06f },
            new Definition { Name = "Toon_Env_Riverbed", Family = "Environment", Base = C(0.36f, 0.42f, 0.4f), Shade = C(0.55f, 0.58f, 0.75f), Noise = 0.08f },
            new Definition { Name = "Toon_Env_Cliff", Family = "Environment", Base = C(0.5f, 0.46f, 0.47f), Shade = C(0.52f, 0.52f, 0.72f), Noise = 0.12f, NoiseScale = 0.9f, ColorVariation = 0.07f },
            new Definition { Name = "Toon_Env_Foliage", Family = "Environment", Base = C(0.3f, 0.58f, 0.32f), Shade = C(0.5f, 0.6f, 0.78f), Noise = 0.14f, NoiseScale = 2.2f, ColorVariation = 0.08f, InnerLineWeight = 0.25f },
            // Characters.
            new Definition { Name = "Toon_Char_Skin", Family = "Character", Base = C(0.97f, 0.77f, 0.62f), Shade = C(0.86f, 0.58f, 0.6f), Softness = 0.03f, Noise = 0f, ColorVariation = 0f, RimStrength = 0.15f, Rim = C(1f, 0.9f, 0.8f) },
            new Definition { Name = "Toon_Char_Hair", Family = "Character", Base = C(0.3f, 0.17f, 0.11f), Shade = C(0.6f, 0.5f, 0.68f), Specular = C(0.5f, 0.35f, 0.25f), SpecularSize = 0.04f, Noise = 0.04f },
            new Definition { Name = "Toon_Char_Eye", Family = "Character", Base = C(0.07f, 0.06f, 0.1f), Shade = C(1f, 1f, 1f), Bands = 2, Noise = 0f, ColorVariation = 0f, ExcludeFromInk = true },
            new Definition { Name = "Toon_Char_Leather", Family = "Character", Base = C(0.4f, 0.24f, 0.15f), Shade = C(0.6f, 0.5f, 0.66f), Noise = 0.05f },
            new Definition { Name = "Toon_Enemy_Hide", Family = "Character", Base = C(0.37f, 0.31f, 0.44f), Shade = C(0.56f, 0.46f, 0.64f), Noise = 0.1f, NoiseScale = 2f, ColorVariation = 0.06f, RimStrength = 0.35f, Rim = C(0.9f, 0.4f, 0.35f) },
            new Definition { Name = "Toon_Enemy_Bone", Family = "Character", Base = C(0.92f, 0.87f, 0.74f), Shade = C(0.7f, 0.6f, 0.68f) },
            new Definition { Name = "Toon_Enemy_Glow", Family = "Character", Base = C(1f, 0.25f, 0.15f), Shade = C(1f, 1f, 1f), Bands = 2, Noise = 0f, Emission = new Color(2.2f, 0.35f, 0.12f), ExcludeFromInk = true }
        };

        public static string PathFor(string name) => MaterialFolder + "/" + name + ".mat";

        public static Dictionary<string, Material> CreateOrUpdateAll()
        {
            Directory.CreateDirectory(MaterialFolder);
            var shader = FindShader(ToonShaderName);
            var result = new Dictionary<string, Material>();
            foreach (var d in Definitions)
            {
                var material = LoadOrCreate(PathFor(d.Name), shader);
                material.SetColor("_BaseColor", d.Base);
                material.SetColor("_ShadeColor", d.Shade);
                material.SetFloat("_BandCount", d.Bands);
                material.SetFloat("_BandSoftness", d.Softness);
                material.SetFloat("_ShadeOffset", d.ShadeOffset);
                material.SetFloat("_AmbientStrength", d.Ambient);
                material.SetColor("_SpecularColor", d.Specular);
                material.SetFloat("_SpecularSize", d.SpecularSize);
                material.SetColor("_RimColor", d.Rim);
                material.SetFloat("_RimStrength", d.RimStrength);
                material.SetFloat("_PaintNoiseStrength", d.Noise);
                material.SetFloat("_PaintNoiseScale", d.NoiseScale);
                material.SetFloat("_ColorVariation", d.ColorVariation);
                material.SetColor("_EmissionColor", d.Emission);
                material.SetFloat("_InnerLineWeight", d.InnerLineWeight);
                material.SetFloat("_OutlineExclude", d.ExcludeFromInk ? 1f : 0f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result[d.Name] = material;
            }

            var water = LoadOrCreate(PathFor("Toon_Water_Surface"), FindShader(WaterShaderName));
            EditorUtility.SetDirty(water);
            result["Toon_Water_Surface"] = water;

            var hull = LoadOrCreate(PathFor("Toon_Hull_Outline"), FindShader(HullShaderName));
            EditorUtility.SetDirty(hull);
            result["Toon_Hull_Outline"] = hull;
            return result;
        }

        public static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Avatar5: shader not found: " + name);
            return shader;
        }

        private static Material LoadOrCreate(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }
    }
}
