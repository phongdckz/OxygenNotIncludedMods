using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;


using KMod;

namespace FastFirePole
{
    public class FastFirePole : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(FirePoleConfig), "ConfigureBuildingTemplate")]
        public static class FirePoleConfig_Configure_Patch
        {
            public static void Postfix(GameObject go)
            {
                Ladder ladder = go.GetComponent<Ladder>();
                if (ladder != null)
                {
                    ladder.upwardsMovementSpeedMultiplier = 2f;
                    ladder.downwardsMovementSpeedMultiplier = 4f;
                }
            }
        }
    }
}
