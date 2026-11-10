# Keyed Save States

Keyed Save States stores one named save state per key and level, and executes save/load commands through JumpKingHttpCommandBroker.

## Broker target

```text
keyed_save_states
```

## Requests

```text
http://127.0.0.1:8081/command?target=keyed_save_states&command=save&key=123456789
http://127.0.0.1:8081/command?target=keyed_save_states&command=load&key=123456789
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

`command=save` overwrites that key's file for the current level. `command=load` reads the key from the current level only, moves the player to the saved position, and clears velocity.

## Setting the attempt time

`command=load` accepts optional `h`, `m` and `s` (hours, minutes, seconds).
When any of them is given, the in-game attempt timer is set to their sum when the
load succeeds. Each may be omitted or have decimals; the total is rounded to the
nearest frame.

```text
http://127.0.0.1:8081/command?target=keyed_save_states&command=load&key=123456789&m=1&s=30.5
http://127.0.0.1:8081/command?target=keyed_save_states&command=load&key=123456789&s=0
```

If any of `h`, `m` or `s` is not a non-negative number, the whole command does
nothing.
The value is independent of the save file, and applies whichever player the
command targets, because the game keeps one attempt timer.

Like the SaveStates mod's "Include time", this changes only the timer in memory.
The mod does not write the game's `attempt_stats.stat`. The game itself writes
the in-memory value there when an attempt is continued from the menu, so the
change survives a return to the menu, but closing the game before that discards
it. Wind on
screens without a fixed direction is derived from this timer, so setting it also
changes the wind phase.

## Local multiplayer integration

When Local Multiplayer Mod is installed, the Broker request's optional `user`
selects the target player. The save key remains independent from the user and is
still the stable storage identifier. A command is ignored when its user does not
resolve to a player.

Without Local Multiplayer Mod, commands keep their normal Player 1 behavior.
