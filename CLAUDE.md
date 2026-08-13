# Blastlands — Project Instructions

Hérite des règles globales du workspace (`../CLAUDE.md`). Ce fichier ajoute le contexte
spécifique au projet.

## Ce que c'est

Jeu de bomber multijoueur en arène, 2 à 8 joueurs, serveurs dédiés. Pas Steam, pas de
compte obligatoire. Lire [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) avant de toucher au
réseau ou au déploiement, et [`docs/GAMEPLAY.md`](docs/GAMEPLAY.md) avant de toucher aux
règles de jeu.

Le nom **Blastlands** est volontaire : « Bomberman » est une marque Konami, on ne l'utilise
ni dans le nom, ni dans les assets, ni dans le marketing.

## Stack

- Unity **6000.4.2f1** (Unity 6 LTS), URP 17.4, Input System 1.19
- Netcode for GameObjects 2.x + Unity Transport, topologie **serveur dédié**
- Lobby : Rust stable, axum, tokio
- Pas de package Unity ni de crate ajouté sans discussion préalable

## La règle qui structure tout : `Core/` ne connaît pas Unity

`client/Assets/_Game/Core/` est du C# pur. **Aucun `using UnityEngine`**, jamais. C'est
vérifié par l'asmdef (`noEngineReferences: true`) et par le fait que `tests/` recompile ces
mêmes sources sous .NET nu.

Conséquences pratiques :

- La CI teste le gameplay avec `dotnet test`, sans licence Unity et sans image éditeur.
  Seuls les builds et les tests PlayMode ont besoin de la licence.
- La simulation est déterministe, donc prédiction client et réconciliation serveur sont
  possibles : les deux côtés font tourner le même code sur les mêmes inputs.

Dans `Core/`, interdits : `float` pour les positions (grille entière + point fixe pour le
sous-tile), `System.Random` non seedé, `DateTime`, `Time.*`, itération sur une collection
non ordonnée quand l'ordre change le résultat.

Tout ce qui est présentation, input, VFX, audio, wiring NGO va dans
`client/Assets/_Game/Runtime/`, qui a le droit de dépendre de `Core/` — jamais l'inverse.

## Dossiers

```
client/Assets/_Game/Core/       Simulation déterministe, C# pur
client/Assets/_Game/Runtime/    Glue Unity (présentation, input, netcode)
client/Assets/_Game/Tests/      Tests NUnit (EditMode), partagés avec tests/
client/Assets/_Game/Scenes/     Scènes
client/Assets/Settings/         URP (ne pas toucher sans raison)
server/crates/lobby/            Service lobby Rust
tests/Blastlands.Core.Tests/    Projet .NET qui compile Core/ + Tests/ hors Unity
```

## Conventions de code

- **Pas de commentaires** (cf. règle globale). Noms parlants, fonctions courtes.
- Namespace racine : `Blastlands` (puis `.Core`, `.Runtime`, …).
- MonoBehaviour : `[SerializeField] private` plutôt que `public`.
- ScriptableObject pour toute donnée de tuning. Pas de magic number dans le code de règles.
- Côté Rust : idiomatique (voir règle globale), erreurs typées, pas de `unwrap` hors tests.

## Serveur et client sont le même build

Le serveur dédié est le **même projet Unity**, buildé pour la plateforme Dedicated Server.
Ne pas dupliquer la logique de jeu dans le lobby Rust : le lobby ne connaît pas les bombes,
il ne tient que l'annuaire des parties.

## Tests

- Logique de gameplay (`Core/`) : NUnit, systématiquement testée. C'est la partie qui a des
  invariants (chaînes d'explosions, génération d'arène, danger map des bots) et c'est la
  partie qui casse silencieusement.
- Lobby Rust : `cargo test`, endpoints et cycle de vie des parties.
- Pas de test PlayMode « la scène se charge ». Si un test ne peut pas échouer sur un vrai
  bug, ne pas l'écrire.

## Commits

Conventional Commits. Scopes typiques : `core`, `sim`, `netcode`, `lobby`, `bots`, `ui`,
`arena`, `input`, `ci`, `server`.

## CI

- `ci-core` tourne sur chaque PR, sans licence Unity. C'est le vrai filet de sécurité.
- `ci-unity` (tests PlayMode + builds) a besoin des secrets `UNITY_EMAIL`, `UNITY_PASSWORD`,
  `UNITY_LICENSE`. Tant qu'ils ne sont pas configurés, le job se skippe proprement au lieu
  de rougir le repo.
- `ci-lobby` et les builds d'images passent par les workflows réutilisables de
  `FerrLabs/.github`, pinnés par SHA.
