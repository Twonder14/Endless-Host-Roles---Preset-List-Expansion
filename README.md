# Endless-Host-Roles---Preset-List-Expansion
Adds Preset Lists to Endless Host Roles (Among Us Mod) For More Presets.

EHR Preset System 1.0.0

This package contains the CTA preset-list integration and the independent
EHRPresetReload.dll plugin.

IMPORTANT:
- EHRPresetReload.dll is independent of EHR.dll. Do not merge it into EHR.dll.
- Options.json is reloaded with EHR.Modules.OptionSaver.Load().
- English.dat is reloaded with EHR.Translator.LoadLangs(). It does not send F5+T.
- The CTA uses Memory.txt as the source of truth for preset-list names and slots.

CHAT TEMPLATE SYSTEM
--------------------
CTA creates this file automatically:
C:\Program Files (x86)\Steam\steamapps\common\Among Us\EHR_DATA\SaveData\PresetChat.txt

The EHR template file is read from:
C:\Program Files (x86)\Steam\steamapps\common\Among Us\EHR_DATA\template.txt

A template line is expected to use this format:
example: this is a template <color=red>Red</color>

The text AFTER the first colon is sent exactly as the template message,
including EHR/Among Us rich-text tags such as <color=red>...</color>.
The template name is REQUIRED; the plugin does not interpret template names
as gamemodes and does not search for gamemode names.

DEFAULT CHAT CONFIG
-------------------
Enabled=true
DefaultMode=Template
DefaultTemplate=example
DefaultMessage={preset} is now active!
DefaultAdditionalInfoEnabled=false
DefaultAdditionalInfo=Slot {slot} is now active.

PER-PRESET CHAT CONFIG
----------------------
Add a section whose name exactly matches a preset-list name from Memory.txt.
Matching is case-insensitive.

Example:

[Preset List 1]
Mode=Template
Template=example
AdditionalInfoEnabled=true
AdditionalInfo=Preset {preset} is now active in Slot {slot}.

[Preset List 2]
Mode=Template
Template=another_example
AdditionalInfoEnabled=false
AdditionalInfo=

You can also use a normal message instead of a template:

[Preset List 3]
Mode=Message
Message=Preset {preset} is now active!
AdditionalInfoEnabled=true
AdditionalInfo=This is Slot {slot}.

To disable chat for one preset list:

[Preset List 4]
Mode=None

Supported placeholders in template text and additional info:
{preset} = selected preset-list name
{slot} = selected physical slot number
\n = newline

Templates are read fresh when a preset is switched, so editing EHR's
template.txt does not require rebuilding the DLL.

