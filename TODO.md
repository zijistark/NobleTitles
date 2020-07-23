# Noble Titles: TODO

- Ensure that dead, titled nobles are always moved to the "dead guy assigned titles" list -- not just on the daily tick, because the player may save the game after a titled noble has died but before the next daily tick, preventing the proper persistence of the dead noble's title.
  - We could theoretically collect them in bulk `OnBeforeSave` in addition to the daily tick. This prevents the need to manipulate those lists on-demand every time a hero is killed.

- When a title is queried from the `TitleDB`, gracefully fallback to a default culture if the requested culture is not found.
  - Currently it's a lot of null reference exceptions waiting to happen for mods that add cultures (or if a minor faction culture like Vakken somehow creates a kingdom and is able to set Vakken as the kingdom culture).
  - Easiest way is to just create a 'default' culture entry and basically use Vlandia's titles for it (as generic as they come).
- Add (de)serialization of dead noble titles
  - List of `(MBGUID, TitlePrefix)` pairs (`MBGUID` of the `Hero`) stored either directly by the SaveSystem or manually marshaled into a simple JSON string synchronized by the SaveSystem.
  - Upon session launch, resolve the data structure to the `deadTitles` list (which means scanning `Hero.All` for MBGUID matches, unless they can be looked up directly in the `MBObjectManager`)

