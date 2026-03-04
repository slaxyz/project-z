# Combat Data Workflow

This project now uses data-driven enemy spells.

## Where assets live
- Spell library: `Assets/Resources/Combat/SpellLibrary.asset`
- Enemy catalog: `Assets/Resources/Combat/EnemyCatalog.asset`
- Individual spells: `Assets/Resources/Combat/Spells/`

## Add a new spell
1. In Unity, right-click `Assets/Resources/Combat/Spells`.
2. Select `Create > Project Z > Combat > Spell`.
3. Fill:
- `spellId` (must be unique)
- `displayName`
- `effectType` (`Damage`, `Shield`, `Heal`)
- `value`
- `costs` (elements + amounts)
- `isEnemyUsable` (enabled for enemy spells)
4. Open `SpellLibrary.asset` and add the new spell to `spells`.

## Link a spell to an enemy
1. Open `EnemyCatalog.asset`.
2. Find your enemy in `enemies`.
3. Add an entry in `intents`:
- `spellId`: same id as in spell asset
- `intentLabelOverride`: optional custom label in combat UI

## Debug warnings (important)
- `SpellLibrary missing at Resources/Combat/SpellLibrary`
- `Enemy intent references unknown spellId: X (enemyId: Y)`
- `Duplicate spellId detected: X`
- `Enemy Y has no valid intents after spell resolution`

If any of these appear, fights still run using code fallback data.
