# Atelier Nucléaire — « Mon atelier, mes règles »

Démo de crafting (La Horde, B3 — Quête I4) : une chaîne de production inspirée de vrais procédés
industriels (électrolyse, synthèse chimique, fonderie, fission…). Objectif ultime : **l'antimatière**.
Le joueur consulte le livre de recettes, remonte l'arbre de fabrication, construit ses machines à
l'établi, les relie par câbles / tuyaux / tubes / convoyeurs et configure leurs entrées-sorties.

> Rendu : vendredi 13 novembre, 21h.

## Lancer la démo
- **Build prête à jouer** : télécharger l'archive Windows ou Linux de la dernière *release* du dépôt, la décompresser
  et lancer `AtelierNucleaire.exe` (Windows) ou `AtelierNucleaire.x86_64` (Linux).
- Le jeu démarre directement dans l'atelier, sur un **tutoriel guidé** (environ 5 min). Le bouton
  **« Passer le tutoriel »** de la carte du tutoriel débloque tout immédiatement.
- Objectifs, de difficulté croissante : lingot d'acier → combustible fissile → pastille d'antimatière.

## Ouvrir le projet
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
Les touches suivent la position physique (AZERTY entre parenthèses) et se changent dans les options (Échap).

| Action | Touche |
|---|---|
| Déplacer la caméra | WASD (ZQSD), ou clic molette maintenu |
| Zoom | Molette |
| Tourner la caméra | R, ou défilement horizontal du pavé tactile |
| Sélectionner / inspecter | Clic gauche sur un élément |
| Menu contextuel (démonter, déplacer…) | Clic droit sur un élément |
| Équiper un objet posable | 1 à 9 (barre rapide), puis clic gauche pour poser (glisser pour une ligne de câbles) |
| Tourner la machine à poser | R pendant la pose |
| Type du câble universel | T (électricité, liquide, gaz, objets) |
| Ranger l'objet équipé / fermer | Clic droit ou Échap |
| Inventaire · Établi · Livre de recettes | E · C · B |
| Recette / usages de l'objet survolé | R / U |
| Mode normal → suppression → modification | Q (A) |
| Agrandir l'atelier | Bouton « Agrandir » en bas à gauche |
| Pause / reprise | Espace (vitesses ×1, ×2, ×4 en haut à droite) |

## Vérification
- Tests EditMode : `Unity -batchmode -projectPath . -runTests -testPlatform EditMode`
- Builds de rendu : `Unity -batchmode -quit -projectPath . -executeMethod Nucleaire.EditorTools.DevBuild.ReleaseWindows`
  (ou `ReleaseLinux`) → `Build/Release/<plateforme>/`
- Build Linux de développement : `-executeMethod Nucleaire.EditorTools.DevBuild.Linux`
- Captures automatiques : lancer la build avec `-autoshot <dossier> -autoshot-scenes overview,chain,book`

## Mes règles de crafting
Voir [`Docs/MesRegles.md`](Docs/MesRegles.md) : les quatre règles (énergie, sous-produits, chaleur, multi-blocs),
leur intention, leurs interactions et l'énigme de l'antimatière.
