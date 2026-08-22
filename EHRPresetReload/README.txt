EHRPresetReload is a separate BepInEx plugin.

It has NO compile-time reference to EHR.dll. It locates EHR.Modules.OptionSaver.Load()
at runtime by reflection. Therefore you can build/update this DLL independently.

The CTA writes EHR_DATA\SaveData\PresetReload.signal after swapping Options.json.
The plugin detects that signal on Unity's main thread, invokes OptionSaver.Load(),
and deletes the signal after success.

Designed against EHR v7.9.0. If a future EHR update removes/renames OptionSaver.Load,
the plugin logs the error rather than silently doing the wrong thing.

Build the plugin with Visual Studio 2022 and place the resulting EHRPresetReload.dll
in Among Us\BepInEx\plugins\ next to EHR.dll.

This plugin only reloads Options.json. English.dat hot reload is not implemented here.
