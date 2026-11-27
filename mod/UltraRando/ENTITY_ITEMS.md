# Entité Celeste -> item Archipelago (option `interactable_shuffle`)

Table appliquée par `Source/AP/ItemNames.cs` (`ItemForEntity`). Garder les deux synchronisés.
Quand `slot_data.options.interactable_shuffle` vaut 1, au chargement de chaque salle d'une aire active,
les entités dont l'item n'est pas reçu ne sont **pas chargées** (la `LevelData` n'est pas modifiée : un item reçu
prend effet au prochain chargement de salle ou respawn).

| Nom d'entité (`EntityData.Name`) | Condition | Item (nom Open World) |
|---|---|---|
| `spring`, `wallSpringLeft`, `wallSpringRight` | | Springs |
| `zipMover` | | Traffic Blocks |
| `cassetteBlock` | `index` = 0 | Blue Cassette Blocks |
| `cassetteBlock` | `index` = 1 | Pink Cassette Blocks |
| `cassetteBlock` | `index` = 2 | Yellow Cassette Blocks |
| `cassetteBlock` | `index` = 3 | Green Cassette Blocks |
| `dreamBlock` | | Dream Blocks |
| `touchSwitch` | | Coins |
| `sinkingPlatform` | | Sinking Platforms |
| `movingPlatform` | | Moving Platforms |
| `booster` | `red` = false (bulle verte) | Blue Boosters |
| `booster` | `red` = true | Red Boosters |
| `cloud` | `fragile` = false | Blue Clouds |
| `cloud` | `fragile` = true | Pink Clouds |
| `moveBlock` | | Move Blocks |
| `whiteblock` | | White Block |
| `swapBlock` | | Swap Blocks |
| `torch` | | Blue Torches |
| `infiniteStar` (plume) | | Feathers |
| `bigSpinner` (bumper) | | Bumpers |
| `crushBlock` | | Kevins |
| `badelineBoost` | | Badeline Boosters |
| `fireBall` | | Fire and Ice Balls |
| `coreModeToggle` | | Core Toggles |
| `bounceBlock` | | Core Blocks |
| `eyebomb` (puffer) | | Pufferfish |
| `glider` | | Jellyfish |
| `lightningBlock` | | Breaker Boxes |
| `refill` | `twoDash` = false | Dash Refills |
| `refill` | `twoDash` = true | Double Dash Refills |
| `dashSwitchH`, `dashSwitchV` | | Dash Switches |
| `seeker` | | Seekers |
| `flingBird` | | Bird |

## Volontairement non retirés

- `theoCrystal` (item « Theo Crystal ») : sans Theo, les portes de temple et plusieurs salles de 5a ne
  fonctionnent plus ; on ne le retire pas.
- Clés, `lockBlock`, portes à cœurs, fraises (y compris les fraises à graines : item « Strawberry Seeds » non
  appliqué), cœurs, cassettes : ce sont des checks ou des éléments de structure.
- Clutter de 3a (« Celestial Resort A - Brown/Green/Pink Clutter »), « Yellow Torches », « Farewell - Power
  Source - Breaker Disable » : sémantique propre à Open World, pas d'entité unique à retirer. À traiter plus tard.
- Toute entité de cinématique ou de déclenchement.
