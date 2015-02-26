﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TweakScale
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class TechUpdater : MonoBehaviour
    {
        public void Start()
        {
            Tech.Reload();
        }
    }

    public static class Tech
    {
        private static HashSet<string> _unlockedTechs = new HashSet<string>();

        public static void Reload()
        {
            if (HighLogic.CurrentGame == null)
                return;
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
                return;

            var persistentfile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs";
            var config = ConfigNode.Load(persistentfile);
            var gameconf = config.GetNode("GAME");
            var scenarios = gameconf.GetNodes("SCENARIO");
            var thisScenario = scenarios.FirstOrDefault(a => a.GetValue("name") == "ResearchAndDevelopment");
            if (thisScenario == null)
                return;
            var techs = thisScenario.GetNodes("Tech");

            _unlockedTechs = techs.Select(a => a.GetValue("id")).ToHashSet();
            _unlockedTechs.Add("");
        }

        public static bool IsUnlocked(string techId)
        {
            if (HighLogic.CurrentGame == null)
                return true;
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
                return true;
            return techId == "" || _unlockedTechs.Contains(techId);
        }
    }

    /// <summary>
    /// Configuration values for TweakScale.
    /// </summary>
    public class ScaleType
    {
        /// <summary>
        /// Fetches the scale ScaleType with the specified name.
        /// </summary>
        /// <param name="name">The name of the ScaleType to fetch.</param>
        /// <returns>The specified ScaleType or the default ScaleType if none exists by that name.</returns>
        private static ScaleType GetScaleConfig(string name)
        {
            var config = GameDatabase.Instance.GetConfigs("SCALETYPE").FirstOrDefault(a => a.name == name);
            if (config == null && name != "default")
            {
                Tools.LogWf("No SCALETYPE with name {0}", name);
            }
            return (object)config == null ? DefaultScaleType : new ScaleType(config.config);
        }

        public class NodeInfo
        {
            public readonly string Family;
            public readonly float Scale;

            private NodeInfo()
            {
            }

            public NodeInfo(string family, float scale) : this()
            {

                Family = family;
                Scale = scale;
                if (Mathf.Abs(Scale) < 0.01)
                {
                    Tools.LogWf("Invalid scale for family {0}: {1}", family, scale);
                }
            }

            public NodeInfo(string s) : this()
            {
                var parts = s.Split(':');
                if (parts.Length == 1)
                {
                    if (!float.TryParse(parts[0], out Scale))
                        Tools.LogWf("Invalid attachment node string \"{0}\"", s);
                    return;
                }
                if (parts.Length == 0)
                {
                    return;
                }
                if (!float.TryParse(parts[1], out Scale))
                {
                    Tools.LogWf("Invalid attachment node string \"{0}\"", s);
                    return;
                }
                Family = parts[0];
                if (Mathf.Abs(Scale) < 0.01)
                {
                    Tools.LogWf("Invalid scale for family {0}: {1}", Family, Scale);
                }
            }

            public override string ToString()
            {
                return string.Format("({0}, {1})", Family, Scale);
            }
        }

        private static List<ScaleType> _scaleTypes;
        public static List<ScaleType> AllScaleTypes
        {
            get {
                return _scaleTypes = _scaleTypes ??
                        (GameDatabase.Instance.GetConfigs("SCALETYPE")
                            .Select(a => new ScaleType(a.config))
                            .ToList<ScaleType>());
            }
        }

        private static readonly ScaleType DefaultScaleType = new ScaleType();

        private readonly float[] _scaleFactors = { 0.625f, 1.25f, 2.5f, 3.75f, 5f };
        private readonly string[] _scaleNames = { "62.5cm", "1.25m", "2.5m", "3.75m", "5m" };
        public readonly Dictionary<string, ScaleExponents> Exponents = new Dictionary<string, ScaleExponents>();

        public readonly bool IsFreeScale = false;
        public readonly string[] TechRequired = { "", "", "", "", "" };
        public readonly Dictionary<string, NodeInfo> AttachNodes = new Dictionary<string, NodeInfo>();
        public readonly float MinValue = 0f;
        public readonly float MaxValue = 0f;
        public readonly float DefaultScale = 1.25f;
        public readonly float IncrementLarge = 0;
        public readonly float IncrementSmall = 0;
        public readonly float IncrementSlide = 0;
        public readonly string Suffix = "m";
        public readonly string Name;
        public readonly string Family;
        public float BaseScale {
            get { return AttachNodes["base"].Scale; }
        }

        public float[] AllScaleFactors
        {
            get
            {
                return _scaleFactors;
            }
        }

        public float[] ScaleFactors
        {
            get
            {
                var result = _scaleFactors.ZipFilter(TechRequired, Tech.IsUnlocked).ToArray();
                return result;
            }
        }

        public string[] ScaleNames
        {
            get
            {
                var result = _scaleNames.ZipFilter(TechRequired, Tech.IsUnlocked).ToArray();
                return result;
            }
        }

        public int[] ScaleNodes { get; private set; }

        private ScaleType()
        {
            ScaleNodes = new int[] {};
            AttachNodes = new Dictionary<string, NodeInfo>();
            AttachNodes["base"] = new NodeInfo("", 1);
        }

        public ScaleType(ConfigNode config)
        {
            ScaleNodes = new int[] {};
            if ((object)config == null || Tools.ConfigValue(config, "name", "default") == "default")
            {
                return; // Default values.
            }

            var type = Tools.ConfigValue(config, "type", "default");
            var source = GetScaleConfig(type);

            IsFreeScale   = Tools.ConfigValue(config, "freeScale",    source.IsFreeScale);
            MinValue      = Tools.ConfigValue(config, "minScale",     source.MinValue);
            MaxValue      = Tools.ConfigValue(config, "maxScale",     source.MaxValue);
            Suffix        = Tools.ConfigValue(config, "suffix",       source.Suffix);
            _scaleFactors = Tools.ConfigValue(config, "scaleFactors", source._scaleFactors);
            ScaleNodes    = Tools.ConfigValue(config, "scaleNodes",   source.ScaleNodes);
            _scaleNames   = Tools.ConfigValue(config, "scaleNames",   source._scaleNames).Select(a => a.Trim()).ToArray();
            TechRequired  = Tools.ConfigValue(config, "techRequired", source.TechRequired).Select(a=>a.Trim()).ToArray();
            Name          = Tools.ConfigValue(config, "name",         "unnamed scaletype");
            Family        = Tools.ConfigValue(config, "family",       "default");
            AttachNodes   = GetNodeFactors(config.GetNode("ATTACHNODES"), source.AttachNodes);
            IncrementLarge= Tools.ConfigValue(config, "incrementLarge",     source.IncrementLarge);
            IncrementSmall= Tools.ConfigValue(config, "incrementSmall",     source.IncrementSmall);
            IncrementSlide= Tools.ConfigValue(config, "incrementSlide",     source.IncrementSlide);

            if (Name == "TweakScale")
            {
                Name = source.Name;
            }

            if (!IsFreeScale && (_scaleFactors.Length != _scaleNames.Length))
            {
                Tools.LogWf("Wrong number of scaleFactors compared to scaleNames in scaleType \"{0}\": {1} scaleFactors vs {2} scaleNames", Name, _scaleFactors.Length, _scaleNames.Length);
            }

            int numTechs = TechRequired.Length;
            if (numTechs != _scaleFactors.Length)
            {
                if (numTechs > 0)
                    Tools.LogWf("Wrong number of techRequired compared to scaleFactors in scaleType \"{0}\": {1} scaleFactors vs {2} techRequired", Name, _scaleFactors.Length, TechRequired.Length);

                if (numTechs < _scaleFactors.Length)
                {
                    TechRequired = TechRequired.Concat("".Repeat()).Take(_scaleFactors.Length).ToArray();
                }
            }

            var tmpScale = Tools.ConfigValue(config, "defaultScale", source.DefaultScale);
            // fallback for MinValue and MaxValue
            if (MaxValue == 0f)
            {
                if (AllScaleFactors.Length > 0)
                    MaxValue = _scaleFactors.Max();
                else
                {
                    Tools.LogWf("ScaleType \"{0}\" is missing a maxScale", Name);
                    MaxValue = tmpScale * 4.0f;
                }
            }
            if (MinValue == 0f)
            {
                if (AllScaleFactors.Length > 0)
                    MinValue = _scaleFactors.Min();
                else
                {
                    MinValue = tmpScale * 0.5f;
                    Tools.LogWf("ScaleType \"{0}\" is missing a minScale", Name);
                }
            }
            if (!IsFreeScale)
            {
                tmpScale = Tools.Closest(tmpScale, AllScaleFactors);
            }
            DefaultScale = Tools.Clamp(tmpScale, MinValue, MaxValue);
            if (IncrementLarge == 0)
                IncrementLarge = MaxValue;
            if (IncrementSlide == 0)
                IncrementSlide = MaxValue / 200f;

            Exponents = ScaleExponents.CreateExponentsForModule(config, source.Exponents);
        }

        private Dictionary<string, NodeInfo> GetNodeFactors(ConfigNode node, Dictionary<string, NodeInfo> source)
        {
            var result = source.Clone();

            if (node != null)
            {
                foreach (var v in node.values.Cast<ConfigNode.Value>())
                {
                    result[v.name] = new NodeInfo(v.value);
                }
            }

            if (!result.ContainsKey("base"))
            {
                result["base"] = new NodeInfo(Family, 1.0f);
            }

            return result;
        }

        public override string ToString()
        {
            var result = "ScaleType {\n";
            result += "	isFreeScale = " + IsFreeScale + "\n";
            result += "	scaleFactors = " + ScaleFactors + "\n";
            result += " scaleNodes = " + ScaleNodes + "\n";
            result += "	minValue = " + MinValue + "\n";
            result += "	maxValue = " + MaxValue + "\n";
            return result + "}";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ScaleType) obj);
        }

        public static bool operator ==(ScaleType a, ScaleType b)
        {
            if ((object)a == null)
                return (object)b == null;
            if ((object)b == null)
                return false;
            return a.Name == b.Name;
        }

        public static bool operator !=(ScaleType a, ScaleType b)
        {
            return !(a == b);
        }

        protected bool Equals(ScaleType other)
        {
            return string.Equals(Name, other.Name);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}
