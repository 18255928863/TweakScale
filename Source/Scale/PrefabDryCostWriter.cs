﻿using System.Linq;
using System;
using System.Collections;

using UnityEngine;
using TweakScale.Annotations;

namespace TweakScale
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class PrefabDryCostWriter : SingletonBehavior<PrefabDryCostWriter>
    {
		private static readonly int WAIT_ROUNDS = 120; // @60fps, would render 2 secs.
        
		internal static bool isConcluded = false;
        
        [UsedImplicitly]
        private void Start()
        {
            StartCoroutine("WriteDryCost");
        }

        private IEnumerator WriteDryCost()
        {
            PrefabDryCostWriter.isConcluded = false;
            Debug.Log("TweakScale::WriteDryCost: Started");
            for (int i = WAIT_ROUNDS; i >= 0 && null == PartLoader.LoadedPartsList; --i)
            {
                yield return null;
                if (0 == i) Debug.LogError("TweakScale::Timeout waiting for PartLoader.LoadedPartsList!!");
            }

			 // I Don't know if this is needed, but since I don't know that this is not needed,
			 // I choose to be safe than sorry!
            {
                int last_count = int.MinValue;
			    for (int i = WAIT_ROUNDS; i >= 0; --i)
				{
                    if (last_count == PartLoader.LoadedPartsList.Count) break;
					last_count = PartLoader.LoadedPartsList.Count;
                    yield return null;
                    if (0 == i) Debug.LogError("TweakScale::Timeout waiting for PartLoader.LoadedPartsList.Count!!");
				}
			 }

			foreach (AvailablePart p in PartLoader.LoadedPartsList)
            {
				for (int i = WAIT_ROUNDS; i >= 0 && null == p.partPrefab && null == p.partPrefab.Modules && p.partPrefab.Modules.Count < 1; --i)
                {
					yield return null;
                    if (0 == i) Debug.LogErrorFormat("TweakScale::Timeout waiting for {0}.prefab.Modules!!", p.name);
				}
                
                Part prefab = p.partPrefab;
                
                // Historically, we had problems here.
                // However, that co-routine stunt appears to have solved it.
                // But we will keep this as a ghinea-pig in the case the problem happens again.
                try 
                {
                    if (!prefab.Modules.Contains("TweakScale"))
                        continue;
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("[TweakScale] Exception on {0}.prefab.Modules.Contains: {1}", p.name, e);
					Debug.LogWarningFormat("{0}", prefab.Modules);
                    continue; // TODO: Cook a way to try again!
                }
                
                try
                {
					TweakScale m = prefab.Modules["TweakScale"] as TweakScale;
                    m.DryCost = (float)(p.cost - prefab.Resources.Cast<PartResource>().Aggregate(0.0, (a, b) => a + b.maxAmount * b.info.unitCost));
					m.ignoreResourcesForCost |= prefab.Modules.Contains("FSfuelSwitch");

                    if (m.DryCost < 0)
                    {
                        Debug.LogErrorFormat("TweakScale::PrefabDryCostWriter: negative dryCost: part={0}, DryCost={1}", p.name, m.DryCost);
                        m.DryCost = 0;
                    }
#if DEBUG
					  Debug.LogFormat("Part {0} has drycost {1} with ignoreResourcesForCost {2}", p.name, m.DryCost, m.ignoreResourcesForCost);
#endif
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("[TweakScale] part={0} ({1}) Exception on writeDryCost: {2}", p.name, p.title, e);
                }
            }
            Debug.Log("TweakScale::WriteDryCost: Concluded");
            PrefabDryCostWriter.isConcluded = true;
        }
    }
}