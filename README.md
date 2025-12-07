# JobAutoSwitcher

**JobAutoSwitcher** is a Dalamud plugin for *Final Fantasy XIV* that removes the annoyance of the "Class/Job is different" error when your queue pops.

If you queue for a duty as a **Warrior**, switch to **Armorer** to craft while waiting, and then click **Commence** when the duty pops, the game normally blocks you. This plugin intercepts that click, automatically switches you back to **Warrior**, and enters the duty for you.

## Features
* **Auto-Switching:** Instantly switches your gearset to match the job you queued with.
* **Smart Gear Selection:** Automatically picks the gearset with the **highest Item Level** if you have multiple sets for the same job.
* **Seamless Entry:** Blocks the error message and automatically clicks "Commence" again once the switch is complete.
* **Lag Proof:** Includes a smart retry loop that ensures you enter the duty even if you are stuck in an animation lock (like crafting or casting) when you click.

## Installation
1.  Open **Dalamud Settings**.
2.  Go to the **Experimental** tab.
3.  Add the path to your `JobAutoSwitcher.json` (or the folder containing it) to the **Dev Plugin Locations**.
4.  Enable the plugin in the main plugin installer/list.

## Usage
1.  Queue for a duty (e.g., Sastasha) as **Job A**.
2.  Switch to **Job B** while waiting.
3.  When the "Duty Ready" window appears, click **Commence**.
4.  The plugin will:
    * Detect the mismatch.
    * Switch you back to **Job A**.
    * Wait for the gear change animation.
    * Auto-click Commence to enter the instance.

## Technical Details
* **Detection:** Uses the Job Icon ID from the `ContentsFinderConfirm` addon to calculate the required Job ID (`IconID - 62100`).
* **Switching:** Interfaces directly with the `RaptureGearsetModule` to equip gearsets by ID, bypassing chat command limitations.
* **Safety:** Uses `AddonLifecycle` hooks to ensure stability across game updates.

## License
Distributed under the GNU Affero General Public License v3.0. See `LICENSE` for more information.