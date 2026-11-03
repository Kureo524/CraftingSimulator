# Mes règles de crafting — Atelier Nucléaire

**Le principe.** On ne fabrique pas un objet d'un clic : on construit et on règle une *chaîne de procédés* inspirée
de vrais procédés industriels (broyage, fonte, électrolyse, chimie du soufre, enrichissement, fission). Chaque machine
transforme des entrées en sorties ; le joueur choisit **quelles** machines poser, **comment** les relier et **à quel
régime** les faire tourner. Quatre règles gouvernent l'atelier, et c'est leur rencontre qui crée les décisions.

## Les règles et leur intention

| # | Règle | Intention de gameplay |
|---|---|---|
| 1 | **Budget d'énergie.** Chaque procédé coûte de l'énergie à chaque tick. Sous-alimentée, une machine ralentit en proportion ; certaines (déphaseur) exigent **100 %** sinon elles n'avancent pas du tout. | Obliger à dimensionner sa production : ajouter une machine, c'est aussi tirer sur le réseau. Le joueur arbitre entre panneaux solaires, générateurs au charbon ou à hydrogène, et turbine à vapeur. |
| 2 | **Sous-produits et sorties pleines.** Beaucoup de procédés rendent plusieurs produits (l'électrolyse donne hydrogène **et** oxygène, le réacteur donne vapeur **et** déchets). Une sortie pleine **bloque** la machine. | Rien n'est gratuit : chaque déchet doit être évacué, recyclé ou détruit (évent). Un sous-produit inutile ici devient l'ingrédient d'ailleurs (l'oxygène sert à la chimie du soufre, les déchets nucléaires au polonium). |
| 3 | **Chaleur et refroidissement.** Le joueur règle la vitesse de combustion du réacteur. Chaque mB brûlé exige 10 mB d'eau, qui ressortent en vapeur. Sans eau, **ou si la vapeur n'est pas évacuée**, la chaleur monte ; à 1000° : arrêt d'urgence, tout le combustible est perdu. | Une prise de risque lisible : brûler vite rapporte plus (énergie, déchets) mais demande une logistique d'eau et de vapeur à la hauteur. L'échec coûte cher mais s'explique toujours. |
| 4 | **Géométrie des multi-blocs.** Réacteur, déphaseur et fusion sont des structures creuses à construire. Chaque composant intérieur augmente la puissance (assemblage = +2 mB/tick de combustion ; bobine = +1 vitesse) **et** le coût (contrôle, 2000 J/tick par bobine). | Le *placement* devient une décision : une structure plus grande va plus vite, mais seulement si l'énergie suit. |

**Comment elles interagissent.** La vapeur du réacteur (règle 3) fait tourner la turbine qui alimente l'atelier
(règle 1) ; ses déchets (règle 2) deviennent du polonium, l'ingrédient de l'antimatière ; et le déphaseur qui la produit
réclame d'autant plus d'énergie qu'on lui donne de bobines (règle 4). Accélérer le réacteur accélère donc **tout**…
à condition que l'eau, la vapeur et les déchets suivent.

## Les trois objets, de difficulté croissante

1. **Lingot d'acier** (×4) — *découvrir les règles.* Fer + charbon au four électrique alimenté par un panneau solaire.
   Première vraie décision : fondre le minerai directement (1 lingot) ou le **broyer d'abord** (2 poussières → 2 lingots),
   au prix d'une machine et d'énergie en plus.
2. **Combustible fissile** (1000 mB) — *une chaîne chimique.* Uranium broyé, fondu, enrichi, oxydé ; en parallèle
   l'acide sulfurique (soufre → SO₂ + l'oxygène de l'électrolyse → SO₃ → acide), puis l'acide fluorhydrique ; l'infuseur
   chimique sert **deux fois**. L'enjeu est l'ordre et l'évacuation des sous-produits (règle 2).
3. **Pastille d'antimatière** — *l'énigme.* Le livre de recettes dit *quoi* produire, pas *comment y arriver* : le
   déphaseur exige à chaque tick plus d'énergie que tous les panneaux solaires réunis. Le joueur doit comprendre que
   **le réacteur à fission est à la fois sa centrale et sa source de polonium**, puis trouver un régime stable :
   assez d'assemblages et d'eau pour produire vapeur et déchets, assez de turbines pour les bobines du déphaseur, sans
   faire fondre le cœur. La fusion (allumée grâce à l'énergie de la fission) est une alternative pour les plus ambitieux.

## Un retour toujours lisible
Chaque machine affiche son état et **la cause** de son blocage (« il manque 50 mB d'eau », « Énergie à 60 % : la machine
ralentit », « Vapeur non évacuée : chaleur +12/tick »). Le réacteur montre sa jauge de chaleur ; un journal garde les
incidents ; le livre de recettes (touches R / U) remonte et descend l'arbre. Une règle ne s'applique jamais sans un
message qui la nomme.

## Données séparées du code
Ressources, machines, procédés, recettes d'établi, objectifs, améliorations et tutoriel sont des **ScriptableObjects**
(`Assets/_Project/Data`). Ajouter un procédé = créer un asset `ProcessRecipe` ; la base se met à jour seule et un
validateur vérifie que chaque objectif reste **atteignable** depuis les ressources de départ.
