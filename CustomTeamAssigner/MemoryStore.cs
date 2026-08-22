using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomTeamAssigner;

public sealed class MemoryStore
{
    public string AmongUsPath { get; } =
        @"C:\Program Files (x86)\Steam\steamapps\common\Among Us";

    public string SaveDataPath =>
        Path.Combine(AmongUsPath, "EHR_DATA", "SaveData");

    public string LanguagePath =>
        Path.Combine(AmongUsPath, "Language");

    public string MemoryPath =>
        Path.Combine(AppContext.BaseDirectory, "Memory.txt");

    public string ReloadSignalPath =>
        Path.Combine(SaveDataPath, "PresetReload.signal");

    public string ChatSettingsPath =>
        Path.Combine(SaveDataPath, "PresetChat.txt");

    public Dictionary<int, string> OptionsSlots { get; private set; } = new();
    public Dictionary<int, string> EnglishSlots { get; private set; } = new();

    public string OptionsCurrent { get; private set; } = "";
    public string EnglishCurrent { get; private set; } = "";

    public void LoadOrCreate()
    {
        if (!File.Exists(MemoryPath))
        {
            File.WriteAllText(
                MemoryPath,
                "[Options] Current = Preset List 1" + Environment.NewLine +
                "[Options] Slot 1 = Preset List 2" + Environment.NewLine +
                Environment.NewLine +
                "[English] Current = Preset List 1" + Environment.NewLine +
                "[English] Slot 1 = Preset List 2" + Environment.NewLine,
                Encoding.UTF8);
        }

        Load();
        EnsureChatSettings();
    }

    public Process? OpenMemory()
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = $"\"{MemoryPath}\"",
            UseShellExecute = true
        });
    }

    public void Load()
    {
        var options = new Dictionary<int, string>();
        var english = new Dictionary<int, string>();
        string optionsCurrent = "";
        string englishCurrent = "";

        foreach (string raw in File.ReadAllLines(MemoryPath, Encoding.UTF8))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            int equals = line.IndexOf('=');
            if (equals < 0)
                continue;

            string left = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();

            int close = left.IndexOf(']');
            if (!left.StartsWith("[") || close < 0)
                continue;

            string section = left[1..close].Trim();
            string key = left[(close + 1)..].Trim();

            bool isOptions = section.Equals("Options", StringComparison.OrdinalIgnoreCase);
            bool isEnglish = section.Equals("English", StringComparison.OrdinalIgnoreCase);

            if (!isOptions && !isEnglish)
                continue;

            if (key.Equals("Current", StringComparison.OrdinalIgnoreCase))
            {
                if (isOptions)
                    optionsCurrent = value;
                else
                    englishCurrent = value;

                continue;
            }

            const string slotPrefix = "Slot ";

            if (!key.StartsWith(slotPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!int.TryParse(key[slotPrefix.Length..].Trim(), out int slot) || slot < 1)
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (isOptions)
                options[slot] = value;
            else
                english[slot] = value;
        }

        OptionsSlots = options;
        EnglishSlots = english;
        OptionsCurrent = optionsCurrent;
        EnglishCurrent = englishCurrent;
    }

    /// <summary>
    /// Returns the presets that should be shown on screen.
    /// The active preset is NOT a numbered slot, so it is represented
    /// separately with Slot = 0. Numbered entries are the actual
    /// physical preset slots.
    /// </summary>
    public IReadOnlyList<(int Slot, string Preset, bool IsCurrent)> GetVisiblePresets()
    {
        var result = new List<(int Slot, string Preset, bool IsCurrent)>();

        // Current is a preset-list name, not Slot 1.
        string current = !string.IsNullOrWhiteSpace(OptionsCurrent)
            ? OptionsCurrent
            : EnglishCurrent;

        if (!string.IsNullOrWhiteSpace(current))
            result.Add((0, current, true));

        var slots = OptionsSlots.Keys
            .Union(EnglishSlots.Keys)
            .Distinct()
            .OrderBy(x => x);

        foreach (int slot in slots)
        {
            // Prefer Options because the two sides should normally
            // contain the same preset-list name. If they differ, the
            // mismatch is shown instead of silently renaming it.
            string preset;

            bool hasOptions = OptionsSlots.TryGetValue(slot, out string? optionsPreset) &&
                              !string.IsNullOrWhiteSpace(optionsPreset);
            bool hasEnglish = EnglishSlots.TryGetValue(slot, out string? englishPreset) &&
                              !string.IsNullOrWhiteSpace(englishPreset);

            if (hasOptions && hasEnglish &&
                !string.Equals(optionsPreset, englishPreset, StringComparison.OrdinalIgnoreCase))
            {
                preset = optionsPreset!;
            }
            else if (hasOptions)
            {
                preset = optionsPreset!;
            }
            else if (hasEnglish)
            {
                preset = englishPreset!;
            }
            else
            {
                preset = $"Unnamed Preset";
            }

            result.Add((slot, preset, false));
        }

        return result;
    }

    // Kept for compatibility with any existing CTA code.
    public IReadOnlyList<(int Slot, string Preset)> GetVisibleSlots()
    {
        return GetVisiblePresets()
            .Where(x => !x.IsCurrent)
            .Select(x => (x.Slot, x.Preset))
            .ToList();
    }

    public string SwitchToSlot(int slot)
    {
        Load();

        if (!OptionsSlots.TryGetValue(slot, out string? optionsPreset))
            throw new InvalidOperationException(
                $"[Options] Slot {slot} is missing from Memory.txt.");

        if (!EnglishSlots.TryGetValue(slot, out string? englishPreset))
            throw new InvalidOperationException(
                $"[English] Slot {slot} is missing from Memory.txt.");

        Directory.CreateDirectory(SaveDataPath);
        Directory.CreateDirectory(LanguagePath);

        string optionsCurrentPath =
            Path.Combine(SaveDataPath, "Options.json");

        string optionsSlotPath =
            Path.Combine(SaveDataPath, $"Options_Preset_List_Slot_{slot}.json");

        string englishCurrentPath =
            Path.Combine(LanguagePath, "English.dat");

        string englishSlotPath =
            Path.Combine(LanguagePath, $"English_Preset_List_Slot_{slot}.dat");

        RequireFile(optionsCurrentPath);
        RequireFile(optionsSlotPath);
        RequireFile(englishCurrentPath);
        RequireFile(englishSlotPath);

        // Read all four files before modifying any of them.
        byte[] currentOptions = File.ReadAllBytes(optionsCurrentPath);
        byte[] slotOptions = File.ReadAllBytes(optionsSlotPath);
        byte[] currentEnglish = File.ReadAllBytes(englishCurrentPath);
        byte[] slotEnglish = File.ReadAllBytes(englishSlotPath);

        // Swap Options.json <-> selected Options slot.
        AtomicWrite(optionsCurrentPath, slotOptions);
        AtomicWrite(optionsSlotPath, currentOptions);

        // Swap English.dat <-> selected English slot.
        AtomicWrite(englishCurrentPath, slotEnglish);
        AtomicWrite(englishSlotPath, currentEnglish);

        // The selected slot becomes Current, and the old Current
        // preset-list name moves into that same slot.
        //
        // Example:
        //   Current = Preset List 2
        //   Slot 1  = Preset List 1
        //
        // Selecting Slot 1 produces:
        //   Current = Preset List 1
        //   Slot 1  = Preset List 2
        string oldOptionsCurrent = OptionsCurrent;
        string oldEnglishCurrent = EnglishCurrent;

        OptionsCurrent = optionsPreset;
        EnglishCurrent = englishPreset;

        OptionsSlots[slot] = oldOptionsCurrent;
        EnglishSlots[slot] = oldEnglishCurrent;

        SaveMemory();

        // Tell the independent in-game DLL exactly what happened.
        Directory.CreateDirectory(SaveDataPath);

        string signal =
            "options=true" + Environment.NewLine +
            "english=true" + Environment.NewLine +
            "chat=true" + Environment.NewLine +
            $"slot={slot}" + Environment.NewLine +
            $"preset={EscapeSignalValue(optionsPreset)}" + Environment.NewLine +
            $"timestamp={DateTime.UtcNow:O}" + Environment.NewLine;

        AtomicWriteText(ReloadSignalPath, signal);

        return
            $"Switched to {optionsPreset}." + Environment.NewLine +
            $"Options slot: {slot}" + Environment.NewLine +
            $"English slot: {slot}" + Environment.NewLine +
            "Options.json reload requested." + Environment.NewLine +
            "English.dat reload requested." + Environment.NewLine +
            "Chat announcement requested.";
    }

    public string ChooseRandom()
    {
        Load();

        var candidates =
            OptionsSlots.Keys
                .Intersect(EnglishSlots.Keys)
                .Where(slot =>
                    !string.Equals(
                        OptionsSlots[slot],
                        OptionsCurrent,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        EnglishSlots[slot],
                        EnglishCurrent,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "There are no non-current slots available for Random.");

        return SwitchToSlot(candidates[0]);
    }

    public string ValidateSlots()
    {
        Load();

        var errors = new List<string>();
        int checkedSlots = 0;

        foreach (int slot in OptionsSlots.Keys.Union(EnglishSlots.Keys).Distinct().OrderBy(x => x))
        {
            checkedSlots++;

            if (!OptionsSlots.ContainsKey(slot))
                errors.Add($"Slot {slot}: missing [Options] entry.");
            else
            {
                string path = Path.Combine(
                    SaveDataPath,
                    $"Options_Preset_List_Slot_{slot}.json");

                if (!File.Exists(path))
                    errors.Add($"Slot {slot}: missing {Path.GetFileName(path)}.");
            }

            if (!EnglishSlots.ContainsKey(slot))
                errors.Add($"Slot {slot}: missing [English] entry.");
            else
            {
                string path = Path.Combine(
                    LanguagePath,
                    $"English_Preset_List_Slot_{slot}.dat");

                if (!File.Exists(path))
                    errors.Add($"Slot {slot}: missing {Path.GetFileName(path)}.");
            }
        }

        if (errors.Count == 0)
            return $"Validation successful. {checkedSlots} slot(s) are complete.";

        return
            $"Validation found {errors.Count} issue(s):" +
            Environment.NewLine +
            string.Join(Environment.NewLine, errors);
    }

    public void EnsureChatSettings()
    {
        Directory.CreateDirectory(SaveDataPath);

        if (File.Exists(ChatSettingsPath))
            return;

        File.WriteAllText(
            ChatSettingsPath,
            "# EHR Preset Control chat announcement settings" + Environment.NewLine +
            "# Supported placeholders: {preset}, {slot}" + Environment.NewLine +
            "# Use \\n inside AdditionalInfo to insert a new line." + Environment.NewLine +
            Environment.NewLine +
            "Enabled=true" + Environment.NewLine +
            "DefaultMode=Template" + Environment.NewLine +
            "DefaultTemplate=example" + Environment.NewLine +
            "DefaultMessage={preset} is now active!" + Environment.NewLine +
            "DefaultAdditionalInfoEnabled=false" + Environment.NewLine +
            "DefaultAdditionalInfo=Slot {slot} is now active." + Environment.NewLine +
            Environment.NewLine +
            "# Each preset list can have its own chat message." + Environment.NewLine +
            "# Section names must exactly match the preset-list name in Memory.txt." + Environment.NewLine +
            "# Example:" + Environment.NewLine +
            "# [Preset List 1]" + Environment.NewLine +
            "# Mode=Template" + Environment.NewLine +
            "# Template=example" + Environment.NewLine +
            "# AdditionalInfoEnabled=true" + Environment.NewLine +
            "# AdditionalInfo=Welcome to Preset List 1!" + Environment.NewLine,
            Encoding.UTF8);
    }

    private void SaveMemory()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"[Options] Current = {OptionsCurrent}");

        foreach (var pair in OptionsSlots.OrderBy(x => x.Key))
            sb.AppendLine($"[Options] Slot {pair.Key} = {pair.Value}");

        sb.AppendLine();

        sb.AppendLine($"[English] Current = {EnglishCurrent}");

        foreach (var pair in EnglishSlots.OrderBy(x => x.Key))
            sb.AppendLine($"[English] Slot {pair.Key} = {pair.Value}");

        AtomicWriteText(MemoryPath, sb.ToString());
    }

    private static string EscapeSignalValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\r", "")
            .Replace("\n", "");
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Required file was not found: {path}",
                path);
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temp, bytes);
        Replace(temp, path);
    }

    private static void AtomicWriteText(string path, string text)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, text, Encoding.UTF8);
        Replace(temp, path);
    }

    private static void Replace(string temp, string destination)
    {
        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(temp, destination);
    }
}
