# Keyed Save States

Keyed Save States stores one named save state per key and level, and executes save/load commands through JumpKingHttpCommandBroker.

## Broker target

```text
keyed_save_states
```

## Commands

```text
save,{key}
load,{key}
```

Examples:

```text
save,123456789
load,123456789
```

The key can contain only letters, numbers, `_`, and `-`.

## Data files

The mod creates one directory per level and writes one XML file per key inside it.

```text
keyed_save_states/
  Main Babe/
    123456789.xml
  Babe of Ascension/
    123456789.xml
```

Each file contains one save state:

```xml
<?xml version="1.0"?>
<KeyedSaveState>
  <level_name>Main Babe</level_name>
  <screen>52</screen>
  <x>123.45</x>
  <y>678.90</y>
</KeyedSaveState>
```

`save,{key}` overwrites that key's file for the current level. `load,{key}` reads the key from the current level only, moves the player to the saved position, and clears velocity.
