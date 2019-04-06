# Paisley Park

Paisley Park is a waymark preset tool that allows you to save and load waymark presets to be used at any time without needing to do all the work manually.

## How does it work?

Paisley Park works by injecting assembly into the running application. Paisley Park however does not perform any malicious code on any process, and only injects code to assist with calling functions that already exist inside of the process. When the application is shut down properly, Paisley Park cleans up its mess as if nothing happened. Nothing Paisley Park does affects any process permanently. If you wish to see what is injected at runtime, you can view so [here](https://github.com/LeonBlade/PaisleyPark/blob/master/PaisleyPark/ViewModels/MainWindowViewModel.cs#L213).

## Will I get in trouble for using this?

Paisley Park was created by means of reverse engineering and is implemented through means of modifying the memory of the running process. This means that using this tool is against the terms of service. However, it is my opinion that this tool sits in the gray area that other similar third party applications such as ACT are where they don't negatively impact anyone experience, nor does it give you any major advantage over others or cheat in any way. This is simply a tool which automates a process that (in my opinion) should already be an existing feature.

## Is this a virus?

Paisley Park is 100% safe! The entire source code of this project is available for you to look through if you're skeptical. Your antivirus software may trigger a false positive. Paisley Park does modify the memory of a running application. This may be seen as a number of different types of viruses at a rudementary level. Please note that virus scanning software isn't always completely thourough, and I again ask you to look over the code for yourself if you have any doubts or concerns.

## Will it crash my game?

There is a likelyhood that Paisley Park could crash your game. However, this should only happen currently when the game is updated. Efforts are being made to prevent this from happening, but won't be available until a future update. This means that you cannot use Paisley Park on a new update of the game until it's updated for the patch. Other crashes are still possible however. In the event this happens, please submit an Issue here with your `error.log` and `output.log` located in the application folder and I'll try to assist you the best I can.

## How do I use it?

Paisely Park is currently at a very basic level from how I initially invisioned it. There are additional features that are in the pipeline, but cannot be worked on right now due to a lack of free time. That being said, Paisley Park is fully functional in its current state and is very easy to use. When starting up the application for the first time, click on the Settings button.

### Load

On the main window, this is where you will load your presets. Simply select one of the presets from the list and click load. Instantly, the waymarks will appear in game. These waymarks don't only show up on your screen, but for everyone in your party as well. Please take note of this before using it with people you don't know.

### Create

To create a new preset, simply place waymarks down in game where you wish to save them. Then, click create and name your preset something memorable. Ensure that the "Use current waymarks" is checked and click Create. This will add a new preset from your list. Now, on the main window, you can select your new preset from the drop down and click "Load" at any time to load this preset.

### Edit

Edit functions just like create, except it works on existing presets. Leaving the "Use current waymarks" unchecked, you will only update the name of the preset.

### Delete

Deleting is as simple as selecting a preset to delete, and clicking delete. Paisely Park will ask you first if you wish to delete to ensure you don't make any unwanted mistakes.

### Import/Export

Paisley Park was created for the purpose of making it easy to place waymarks down for various raid scenarios. With that in mind, it makes perfect sense that Paisely Park should have a feature to import and export presets to share with other members of the community. Simply click "Import" and paste in the JSON string shared from another user and click to import it. This will add that preset to your list. The same can be done for sharing your own. Simply click on which preset you wish to share, and click "Export". This will copy the JSON string to your clipboard where you can paste it in Discord, Reddit or elsewhere. While pasting these JSON strings aren't against the terms of service, it might be in your best interest not to share them in game if you wish to be safe.

## Final

Thank you for taking the time to view this project. I hope that you find it useful for your raid nights, or whatever else you find reason to use it. If you have any suggestions, feel free to leave them as "Issues" on this GitHub page, or message me on Discord: LeonBlade#9988.

You may already know of me as the original creator of what is now referred to as "SSTool", the screenshot tool for FFXIV, or from my Cheat Engine script "Tabletopper" to treat any item as a tabletop item for more housing options. I put a lot of effort into creating these tools for others, and I plan on creating more in the future. For those of you who have supported me so far, I thank you very much.

If you wish to donate to me for any reason, I greatly appreciate it. Any support though, monetarily or not is greatly appreciated. Knowing that people are using my tools and enjoying them makes me happy.

https://ko-fi.com/leonblade
