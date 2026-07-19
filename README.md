# Keyed Save States

Keyed Save States stores one save state per key and executes save/load commands through JumpKingHttpCommandBroker.

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

The mod writes one XML file per key next to the mod DLL.

```text
keyed_save_states/
  123456789.xml
```

Each file contains one save state:

```xml
<?xml version="1.0"?>
<KeyedSaveState>
  <screen>52</screen>
  <x>123.45</x>
  <y>678.90</y>
</KeyedSaveState>
```

`save,{key}` overwrites that key's file. `load,{key}` reads only that key's file, moves the player to the saved position, and clears velocity.