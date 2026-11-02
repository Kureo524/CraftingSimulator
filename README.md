# Atelier Nucléaire — « Mon atelier, mes règles »

Démo de crafting (La Horde, B3 — Quête I4) : une chaîne de production inspirée de vrais procédés
industriels (électrolyse, synthèse chimique, fonderie, fission…). Objectif ultime : **l'antimatière**.
Le joueur consulte le livre de recettes, remonte l'arbre de fabrication, construit ses machines à
l'établi, les relie par câbles / tuyaux / tubes / convoyeurs et configure leurs entrées-sorties.

> 🚧 En cours de développement — rendu le vendredi 13 novembre, 21h.

## Lancer le projet
- **Unity 6000.3.23f1** (Unity 6.3 LTS), URP, Input System, UI Toolkit.
- Cloner avec Git LFS installé : `git lfs install && git clone <url>`
- Ouvrir le dossier racine dans Unity Hub, puis la scène `Assets/_Project/Scenes/Workshop.unity` et lancer Play.

## Organisation
| Dossier | Contenu |
|---|---|
| `Assets/_Project/Data` | **Toutes les données** (ScriptableObjects) : objets, machines, recettes, améliorations, objectifs |
| `Assets/_Project/Scripts` | Code : `Data`, `Simulation`, `Logistics`, `Crafting`, `Objectives`, `Presentation`, `UI` |
| `Assets/_Project/UI` | UI Toolkit (UXML / USS) |
| `Assets/_Project/Tests` | Tests EditMode |
| `Docs/` | Sujet et page « Mes règles de crafting » |

Ajouter une recette = créer un asset `ProcessRecipe` dans `Data/Recipes`, sans toucher au code.

## Contrôles
_À compléter._

## Mes règles de crafting
Voir [`Docs/MesRegles.md`](Docs/MesRegles.md) (à venir).
