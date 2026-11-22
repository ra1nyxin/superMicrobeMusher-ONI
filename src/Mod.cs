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
    [HarmonyPriority(Priority.Last)]
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

        public static void RegisterAllSafeRecipes()
        {
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
                    try
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
                    catch (Exception ex)
                    {
                        skippedCount++;
                        Debug.LogWarning("[superMicrobeMusher] Failed to inspect a prefab: " + ex);
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
            ComplexRecipe recipe = null;
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
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
                string finalId = ComplexRecipeManager.MakeRecipeID(FabricatorId, inputs, outputs, null);
                if (manager.GetRecipe(recipeId) != null)
                    return false;
                if (manager.GetRecipe(finalId) != null || HasPendingConflict(manager, finalId))
                {
                    Debug.LogWarning("[superMicrobeMusher] Skipped conflicting recipe for " + resultTag + " (" + finalId + ").");
                    return false;
                }

                recipe = new ComplexRecipe(recipeId, inputs, outputs);
                recipe.time = WorkTimeSeconds;
                recipe.description = "Use a tiny amount of water to fabricate " + resultTag.ProperName() + ".";
                recipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
                recipe.fabricators = new List<Tag> { FabricatorId };
                recipe.sortOrder = sortOrder;

                recipe.recipeCategoryID = ComplexRecipeManager.MakeRecipeCategoryID(FabricatorId, "superMicrobeMusher", resultTag.Name);
                recipe.SetFabricationAnim("mushbar_kanim");
                return true;
            }
            catch (Exception ex)
            {
                if (recipe != null)
                {
                    manager.preProcessRecipes.Remove(recipe);
                    if (recipe.FabricationVisualizer != null)
                    {
                        UnityEngine.Object.Destroy(recipe.FabricationVisualizer);
                    }
                }
                Debug.LogWarning("[superMicrobeMusher] Skipped recipe for " + resultTag + ": " + ex.Message);
                return false;
            }
        }

        private static bool HasPendingConflict(ComplexRecipeManager manager, string finalId)
        {
            foreach (ComplexRecipe pending in manager.preProcessRecipes)
            {
                if (pending == null || pending.id == null || pending.ingredients == null || pending.results == null || pending.results.Length == 0)
                    continue;
                int separator = pending.id.IndexOf('_');
                if (separator < 0 || pending.id.Substring(0, separator) != FabricatorId || pending.ingredients.Length != 2)
                    continue;
                if (!ContainsMaterial(pending.ingredients[0], WaterTag) || !ContainsMaterial(pending.ingredients[1], VisibilityTag))
                    continue;
                ComplexRecipe.RecipeElement[] candidateInputs = {
                    new ComplexRecipe.RecipeElement(WaterTag, WaterKg),
                    new ComplexRecipe.RecipeElement(VisibilityTag, 0f)
                };
                if (ComplexRecipeManager.MakeRecipeID(FabricatorId, candidateInputs, pending.results, pending.results[0].facadeID) == finalId)
                    return true;
            }
            return false;
        }

        private static bool ContainsMaterial(ComplexRecipe.RecipeElement ingredient, Tag tag)
        {
            if (ingredient == null || ingredient.possibleMaterials == null) return false;
            foreach (Tag material in ingredient.possibleMaterials)
                if (material == tag) return true;
            return false;
        }
    }
}
