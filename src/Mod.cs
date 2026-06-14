using System;
using System.Collections.Generic;
using HarmonyLib;
using KMod;
using UnityEngine;

namespace superMicrobeMusher
{
    public sealed class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log("[superMicrobeMusher] Loaded.");
        }
    }

    [HarmonyPatch(typeof(ComplexRecipeManager), "PostProcess")]
    internal static class ComplexRecipeManagerPostProcessPatch
    {
        private static void Prefix()
        {
            SuperMicrobeMusherRecipes.RegisterAllSafeRecipes();
        }
    }

    internal static class SuperMicrobeMusherRecipes
    {
        private const string FabricatorId = "MicrobeMusher";
        private const float WaterKg = 0.001f;
        private const float WorkTimeSeconds = 1f;
        private const int SortOrderBase = -10000;
        private const float MassProductKg = 1000f;
        private const float UnitProductCount = 1f;

        private static readonly Tag WaterTag = SimHashes.Water.CreateTag();
        private static readonly Tag VisibilityTag = SimHashes.Dirt.CreateTag();
        private static readonly HashSet<Tag> ExtraExcludedTags = new HashSet<Tag>
        {
            GameTags.DeprecatedContent,
            GameTags.HideFromSpawnTool,
            GameTags.HideFromCodex,
            GameTags.TemplateBuilding,
            GameTags.Stored,
            GameTags.StoredPrivate,
            GameTags.Minions.Models.Standard,
            GameTags.Minions.Models.Bionic,
            GameTags.CreatureBrain,
            GameTags.DupeBrain,
            GameTags.BagableCreature,
            GameTags.Creatures.Deliverable,
            GameTags.GeyserFeature,
            GameTags.Rocket,
            GameTags.HighEnergyParticle
        };

        private static readonly HashSet<string> ExtraExcludedIds = new HashSet<string>
        {
            FabricatorId,
            "Minion",
            "BionicMinion",
            "SuitMarker",
            "Headquarters",
            "GravitasCreatureManipulator"
        };

        private static bool registered;
        private static readonly HashSet<string> RegisteredRecipeIds = new HashSet<string>();

        public static void RegisterAllSafeRecipes()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            try
            {
                if (Assets.Prefabs == null)
                {
                    Debug.LogWarning("[superMicrobeMusher] Assets.Prefabs is null; no recipes registered.");
                    return;
                }

                int registeredCount = 0;
                int skippedCount = 0;
                HashSet<Tag> seenResults = new HashSet<Tag>();

                foreach (KPrefabID prefabId in Assets.Prefabs)
                {
                    if (prefabId == null || prefabId.gameObject == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    Tag resultTag = prefabId.PrefabTag;
                    if (!resultTag.IsValid || seenResults.Contains(resultTag))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!IsSafeProduct(prefabId, resultTag))
                    {
                        skippedCount++;
                        continue;
                    }

                    float amount = GetOutputAmount(prefabId);
                    if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (RegisterRecipe(resultTag, amount, SortOrderBase + registeredCount))
                    {
                        seenResults.Add(resultTag);
                        registeredCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                Debug.Log("[superMicrobeMusher] Registered " + registeredCount + " Microbe Musher recipes; skipped " + skippedCount + " prefabs.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[superMicrobeMusher] Failed while registering recipes: " + ex);
            }
        }

        private static bool IsSafeProduct(KPrefabID prefabId, Tag resultTag)
        {
            GameObject prefab = prefabId.gameObject;

            if (ExtraExcludedIds.Contains(resultTag.Name))
            {
                return false;
            }

            if (HasAnyTag(prefabId, ExtraExcludedTags))
            {
                return false;
            }

            if (prefab.GetComponent<PrimaryElement>() == null)
            {
                return false;
            }

            if (prefab.GetComponent<Pickupable>() == null)
            {
                return false;
            }

            if (prefab.GetComponent<Building>() != null || prefab.GetComponent<BuildingComplete>() != null)
            {
                return false;
            }

            if (prefab.GetComponent<MinionIdentity>() != null || prefab.GetComponent<Navigator>() != null)
            {
                return false;
            }

            if (prefab.GetComponent<KPrefabID>().HasTag(GameTags.Creature))
            {
                return false;
            }

            if (Assets.TryGetPrefab(resultTag) == null)
            {
                return false;
            }

            return true;
        }

        private static float GetOutputAmount(KPrefabID prefabId)
        {
            PrimaryElement primaryElement = prefabId.GetComponent<PrimaryElement>();
            if (primaryElement == null)
            {
                return 0f;
            }

            if (HasAnyTag(prefabId, GameTags.DisplayAsUnits))
            {
                return UnitProductCount;
            }

            if (prefabId.HasTag(GameTags.Edible))
            {
                return UnitProductCount;
            }

            if (prefabId.HasTag(GameTags.Seed) || prefabId.HasTag(GameTags.CropSeed))
            {
                return UnitProductCount;
            }

            float mass = primaryElement.Mass;
            if (mass > 0.001f)
            {
                return Mathf.Clamp(Mathf.Max(mass, MassProductKg), 1f, MassProductKg);
            }

            return MassProductKg;
        }

        private static bool HasAnyTag(KPrefabID prefabId, IEnumerable<Tag> tags)
        {
            foreach (Tag tag in tags)
            {
                if (prefabId.HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RegisterRecipe(Tag resultTag, float outputAmount, int sortOrder)
        {
            try
            {
                ComplexRecipe.RecipeElement[] inputs =
                {
                    new ComplexRecipe.RecipeElement(WaterTag, WaterKg),
                    new ComplexRecipe.RecipeElement(VisibilityTag, 0f)
                };

                ComplexRecipe.RecipeElement[] outputs =
                {
                    new ComplexRecipe.RecipeElement(resultTag, outputAmount, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                };

                string recipeId = FabricatorId + "_superMicrobeMusher_" + resultTag.Name;
                if (RegisteredRecipeIds.Contains(recipeId) || ComplexRecipeManager.Get().GetRecipe(recipeId) != null)
                {
                    return false;
                }

                ComplexRecipe recipe = new ComplexRecipe(recipeId, inputs, outputs)
                {
                    time = WorkTimeSeconds,
                    description = "Use a tiny amount of water to fabricate " + resultTag.ProperName() + ".",
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                    fabricators = new List<Tag> { FabricatorId },
                    sortOrder = sortOrder
                };

                recipe.recipeCategoryID = ComplexRecipeManager.MakeRecipeCategoryID(FabricatorId, "superMicrobeMusher", resultTag.Name);
                recipe.SetFabricationAnim("mushbar_kanim");
                RegisteredRecipeIds.Add(recipeId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[superMicrobeMusher] Skipped recipe for " + resultTag + ": " + ex.Message);
                return false;
            }
        }
    }
}
