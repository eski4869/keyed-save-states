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

The key can be a Twitch user id or any short identifier provided by an external tool.

## Data file

The mod writes `keyed_save_states.tsv` next to the mod DLL.

```text
key	screen	x	y
```

`save,{key}` overwrites that key's saved position. `load,{key}` moves the player to the saved position and clears velocity.