# BTCantinaMissions — BTX pack installation

## Requirements

1. A working **BEXT** installation with its load-order dependencies settled.
   The two heavy content expansions must be installed BEFORE adding anything
   on top (this pack included):

   - [BTX_CAC_Compatibility](https://github.com/mcb5637/BTX_CAC_Compatibility)
   - [BTX_ExpansionPack](https://github.com/AkiraBrahe/BTX_ExpansionPack)

   Follow your BEXT distribution's instructions for those first; they reshape
   weapons, items and the merge data this pack's jobs reference.

2. **JwTweaks** with custom save blocks enabled — job progress persists
   exclusively through them. In `JwTweaks/mod.json` set inside `Settings`:

   ```json
   "CustomSaveBlocks": true
   ```

   If the key is already there, make sure it is `true`. Without it the board
   regenerates on every load and all job progress is lost. Do not toggle it
   back off mid-campaign: saves written with custom blocks become unreadable
   while the flag is off.

3. Drop the `BTCantinaMissions` folder (from this pack's zip) into `Mods/`.

## Notes

- The vanilla store button is NOT replaced in this pack — the cantina opens
  with the configured hotkey (**F7** by default, ship room view). See
  `settings.json` (`CantinaHotkey`, `InterceptStoreButton`).
- Cantina worlds are `planet_pop_large` systems by default (~540 on the merged map).
