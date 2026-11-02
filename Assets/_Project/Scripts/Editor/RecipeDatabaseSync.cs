using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEditor;
using UnityEngine;

namespace Nucleaire.EditorTools
{
    /// <summary>Garde la RecipeDatabase à jour : tout asset de données créé, supprimé ou déplacé dans
    /// Assets/_Project/Data est automatiquement ajouté/retiré. Ajouter une recette = créer un asset, rien d'autre.</summary>
    public sealed class RecipeDatabaseSync : AssetPostprocessor
    {
        public const string DataFolder = "Assets/_Project/Data";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(DataFolder) && p.EndsWith(".asset") && !p.EndsWith("RecipeDatabase.asset"));
            if (touched) EditorApplication.delayCall += () => Sync(logReport: false);
        }

        [MenuItem("Nucleaire/Synchroniser et valider les recettes")]
        public static void SyncAndValidate() => Sync(logReport: true);

        public static RecipeDatabase Sync(bool logReport)
        {
            var db = FindDatabase();
            if (db == null) return null;

            db.EditorSetContent(Load<ResourceDef>(), Load<MachineDef>(), Load<TransmitterDef>(), Load<ProcessRecipe>(),
                Load<CraftingRecipe>(), Load<UpgradeDef>(), Load<ObjectiveDef>().OrderBy(o => o.Order).ToList(), Load<StructureDef>());
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssetIfDirty(db);

            var report = RecipeDatabaseValidator.Validate(db);
            if (!report.IsValid) Debug.LogError($"[Recettes] Données invalides :\n{report}", db);
            else if (logReport) Debug.Log($"[Recettes] OK — {db.Processes.Count} procédés, {db.CraftingRecipes.Count} recettes d'établi.\n{report}", db);
            return db;
        }

        public static RecipeDatabase FindDatabase()
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(RecipeDatabase), new[] { DataFolder }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<RecipeDatabase>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static List<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => a.name)
                .ToList();
    }
}
