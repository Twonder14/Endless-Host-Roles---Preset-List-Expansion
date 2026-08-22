using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace EHRPresetReload;

[BepInPlugin(PluginGuid, "EHR Preset Reload", PluginVersion)]
[BepInProcess("Among Us.exe")]
public sealed class PresetReloadPlugin : BasePlugin
{
    public const string PluginGuid = "com.tbuddy5.ehrpresetreload";
    public const string PluginVersion = "4.0.1";

    public override void Load()
    {
        ClassInjector.RegisterTypeInIl2Cpp<PresetReloadBehaviour>();
        AddComponent<PresetReloadBehaviour>();

        Log.LogInfo("EHR Preset Reload 4.0.1 loaded.");
        Log.LogInfo("Options reload uses EHR.Modules.OptionSaver.Load().");
        Log.LogInfo("English reload uses EHR.Translator.LoadLangs().");
        Log.LogInfo("Preset announcements support EHR template.txt templates.");
    }
}

public sealed class PresetReloadBehaviour : MonoBehaviour
{
    private float _nextCheck;
    private bool _busy;

    private static string GameRoot => BepInEx.Paths.GameRootPath;

    private static string SaveDataPath =>
        Path.Combine(GameRoot, "EHR_DATA", "SaveData");

    private static string SignalPath =>
        Path.Combine(SaveDataPath, "PresetReload.signal");

    private static string ChatSettingsPath =>
        Path.Combine(SaveDataPath, "PresetChat.txt");

    private static string TemplatePath =>
        Path.Combine(GameRoot, "EHR_DATA", "template.txt");

    public PresetReloadBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        if (_busy || Time.time < _nextCheck)
            return;

        _nextCheck = Time.time + 0.20f;

        if (!File.Exists(SignalPath))
            return;

        _busy = true;

        try
        {
            ReloadRequest request = ReadSignal();

            if (request.Options)
                ReloadOptions();

            if (request.English)
                ReloadEnglish();

            if (request.Chat)
                SendPresetChat(request);

            TryDeleteSignal();
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[EHR Preset Reload] Reload request failed: " + ex);

            // Keep the signal so it can be retried on the next update.
        }
        finally
        {
            _busy = false;
        }
    }

    private static ReloadRequest ReadSignal()
    {
        bool options = false;
        bool english = false;
        bool chat = false;
        int slot = 0;
        string preset = "";

        foreach (string raw in File.ReadAllLines(SignalPath, Encoding.UTF8))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            int equals = line.IndexOf('=');
            if (equals < 0)
                continue;

            string key = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "options":
                    options = ParseBool(value);
                    break;

                case "english":
                    english = ParseBool(value);
                    break;

                case "chat":
                    chat = ParseBool(value);
                    break;

                case "slot":
                    int.TryParse(value, out slot);
                    break;

                case "preset":
                    preset = UnescapeSignalValue(value);
                    break;
            }
        }

        if (!options && !english && !chat)
        {
            options = true;
            english = true;
        }

        return new ReloadRequest(options, english, chat, slot, preset);
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string UnescapeSignalValue(string value)
    {
        return value.Replace("\\\\", "\\");
    }

    private static Assembly? FindAssembly(string name)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a =>
                string.Equals(
                    a.GetName().Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void ReloadOptions()
    {
        Assembly? ehr = FindAssembly("EHR");

        if (ehr == null)
            throw new InvalidOperationException("EHR.dll is not loaded.");

        Type? type = ehr.GetType("EHR.Modules.OptionSaver", false);

        if (type == null)
            throw new MissingMemberException(
                "EHR.Modules.OptionSaver was not found.");

        MethodInfo? method = type.GetMethod(
            "Load",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method == null)
            throw new MissingMethodException(
                "EHR.Modules.OptionSaver.Load() was not found.");

        method.Invoke(null, null);

        Debug.Log(
            "[EHR Preset Reload] Options.json reloaded through EHR.OptionSaver.");
    }

    private static void ReloadEnglish()
    {
        Assembly? ehr = FindAssembly("EHR");

        if (ehr == null)
            throw new InvalidOperationException("EHR.dll is not loaded.");

        Type? translator = ehr.GetType("EHR.Translator", false);

        if (translator == null)
            throw new MissingMemberException(
                "EHR.Translator was not found.");

        MethodInfo? loadLangs = translator.GetMethod(
            "LoadLangs",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (loadLangs == null)
            throw new MissingMethodException(
                "EHR.Translator.LoadLangs() was not found.");

        loadLangs.Invoke(null, null);

        Debug.Log(
            "[EHR Preset Reload] English.dat reloaded through EHR.Translator.LoadLangs().");
    }

    private static void SendPresetChat(ReloadRequest request)
    {
        if (!File.Exists(ChatSettingsPath))
        {
            Debug.LogWarning(
                "[EHR Preset Reload] PresetChat.txt was not found; chat announcement skipped.");
            return;
        }

        ChatSettings settings = ChatSettings.Load(ChatSettingsPath);

        if (!settings.Enabled)
            return;

        string preset = string.IsNullOrWhiteSpace(request.Preset)
            ? "the selected preset list"
            : request.Preset;

        PresetMessage? configured =
            settings.GetPresetMessage(preset);

        string mode = string.IsNullOrWhiteSpace(configured?.Mode)
            ? settings.DefaultMode
            : configured!.Mode;

        if (string.IsNullOrWhiteSpace(mode))
            mode = "Template";

        string message;

        if (mode.Equals("None", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        else if (mode.Equals("Template", StringComparison.OrdinalIgnoreCase))
        {
            string templateName =
                string.IsNullOrWhiteSpace(configured?.TemplateName)
                    ? settings.DefaultTemplateName
                    : configured!.TemplateName;

            if (string.IsNullOrWhiteSpace(templateName))
            {
                Debug.LogWarning(
                    $"[EHR Preset Reload] No template name configured for '{preset}'.");
                return;
            }

            string? template = FindTemplate(templateName);

            if (template == null)
            {
                Debug.LogError(
                    $"[EHR Preset Reload] Template '{templateName}' was not found in {TemplatePath}.");
                return;
            }

            message = Expand(
                template,
                preset,
                request.Slot);
        }
        else if (mode.Equals("Message", StringComparison.OrdinalIgnoreCase))
        {
            string configuredMessage =
                string.IsNullOrWhiteSpace(configured?.Message)
                    ? settings.DefaultMessage
                    : configured!.Message;

            message = Expand(
                configuredMessage,
                preset,
                request.Slot);
        }
        else
        {
            Debug.LogWarning(
                $"[EHR Preset Reload] Unknown chat mode '{mode}' for '{preset}'.");
            return;
        }

        bool additionalEnabled =
            configured?.AdditionalInfoEnabled ??
            settings.DefaultAdditionalInfoEnabled;

        string additional =
            configured?.AdditionalInfo ??
            settings.DefaultAdditionalInfo;

        if (additionalEnabled && !string.IsNullOrWhiteSpace(additional))
        {
            additional = Expand(
                additional,
                preset,
                request.Slot);

            if (!string.IsNullOrWhiteSpace(additional))
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? additional
                    : message + "\n" + additional;
            }
        }

        if (string.IsNullOrWhiteSpace(message))
            return;

        // Among Us chat has a practical message-size limit. Keep the
        // configured EHR template intact unless it exceeds that limit.
        if (message.Length > 400)
            message = message[..400];

        SendChatToEveryone(message);
    }

    private static string? FindTemplate(string templateName)
    {
        if (!File.Exists(TemplatePath))
            return null;

        foreach (string raw in File.ReadAllLines(TemplatePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            int colon = raw.IndexOf(':');

            if (colon <= 0)
                continue;

            string name = raw[..colon].Trim();
            string content = raw[(colon + 1)..].Trim();

            if (name.Equals(
                    templateName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }
        }

        return null;
    }

    private static string Expand(
        string text,
        string preset,
        int slot)
    {
        return text
            .Replace("{preset}", preset, StringComparison.OrdinalIgnoreCase)
            .Replace("{slot}", slot.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("\\n", "\n");
    }

    // EHR's public system-message title.
    // This is deliberately sent through EHR.Utils.SendMessage instead of
    // PlayerControl.RpcSendChat so the announcement appears as an EHR
    // system message rather than as the host/player.
    private const string PresetSystemMessageTitle =
        "<#00ffa5>∞ <#00a5ff>EHR <#00ffff>SYSTEM MESSAGE <#00ffa5>∞";

    private static void SendChatToEveryone(string message)
    {
        try
        {
            Assembly? ehr = FindAssembly("EHR");

            if (ehr == null)
            {
                Debug.LogError(
                    "[EHR Preset Reload] EHR.dll is not loaded; system message skipped.");
                return;
            }

            Type? utilsType = ehr.GetType("EHR.Utils", false);

            if (utilsType == null)
            {
                Debug.LogError(
                    "[EHR Preset Reload] EHR.Utils was not found.");
                return;
            }

            // Find EHR's SendMessage overload without referencing EHR.dll at
            // compile time. This keeps this plugin independently buildable.
            MethodInfo? sendMessage = utilsType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!method.Name.Equals(
                            "SendMessage",
                            StringComparison.Ordinal))
                        return false;

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length == 0 ||
                        parameters[0].ParameterType != typeof(string))
                        return false;

                    return parameters.Any(parameter =>
                        parameter.Name?.Equals(
                            "title",
                            StringComparison.OrdinalIgnoreCase) == true &&
                        parameter.ParameterType == typeof(string));
                });

            if (sendMessage == null)
            {
                Debug.LogError(
                    "[EHR Preset Reload] EHR.Utils.SendMessage(string, ..., title) was not found.");
                return;
            }

            ParameterInfo[] parametersInfo = sendMessage.GetParameters();
            object?[] arguments = new object?[parametersInfo.Length];

            for (int i = 0; i < parametersInfo.Length; i++)
            {
                ParameterInfo parameter = parametersInfo[i];

                if (i == 0)
                {
                    arguments[i] = message;
                    continue;
                }

                if (parameter.Name?.Equals(
                        "title",
                        StringComparison.OrdinalIgnoreCase) == true &&
                    parameter.ParameterType == typeof(string))
                {
                    arguments[i] = PresetSystemMessageTitle;
                    continue;
                }

                // Preserve EHR's own defaults whenever possible.
                if (parameter.HasDefaultValue)
                {
                    arguments[i] = parameter.DefaultValue;
                    continue;
                }

                arguments[i] = parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType)
                    : null;
            }

            sendMessage.Invoke(null, arguments);

            Debug.Log(
                "[EHR Preset Reload] Preset system announcement sent: " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[EHR Preset Reload] Could not send preset system announcement: " + ex);
        }
    }

    private static void TryDeleteSignal()
    {
        try
        {
            if (File.Exists(SignalPath))
                File.Delete(SignalPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[EHR Preset Reload] Could not delete signal: " + ex.Message);
        }
    }

    private readonly struct ReloadRequest
    {
        public readonly bool Options;
        public readonly bool English;
        public readonly bool Chat;
        public readonly int Slot;
        public readonly string Preset;

        public ReloadRequest(
            bool options,
            bool english,
            bool chat,
            int slot,
            string preset)
        {
            Options = options;
            English = english;
            Chat = chat;
            Slot = slot;
            Preset = preset;
        }
    }

    private sealed class ChatSettings
    {
        public bool Enabled { get; private set; }
        public string DefaultMode { get; private set; } = "Template";
        public string DefaultTemplateName { get; private set; } = "";
        public string DefaultMessage { get; private set; } = "";
        public bool DefaultAdditionalInfoEnabled { get; private set; }
        public string DefaultAdditionalInfo { get; private set; } = "";

        private readonly Dictionary<string, PresetMessage> _presetMessages =
            new(StringComparer.OrdinalIgnoreCase);

        public static ChatSettings Load(string path)
        {
            var result = new ChatSettings();
            PresetMessage? current = null;
            string? currentSection = null;

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (current != null && currentSection != null)
                        result._presetMessages[currentSection] = current;

                    currentSection =
                        line[1..^1].Trim();

                    current = new PresetMessage
                    {
                        Mode = "Template"
                    };
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals < 0)
                    continue;

                string key = line[..equals].Trim();
                string value = line[(equals + 1)..].Trim();

                if (current != null)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "mode":
                            current.Mode = value;
                            break;

                        case "template":
                        case "templatename":
                            current.TemplateName = value;
                            break;

                        case "message":
                            current.Message = value;
                            break;

                        case "additionalinfoenabled":
                            current.AdditionalInfoEnabled = ParseBool(value);
                            break;

                        case "additionalinfo":
                            current.AdditionalInfo = value;
                            break;
                    }
                }
                else
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "enabled":
                            result.Enabled = ParseBool(value);
                            break;

                        case "defaultmode":
                            result.DefaultMode = value;
                            break;

                        case "defaulttemplate":
                        case "defaulttemplatename":
                            result.DefaultTemplateName = value;
                            break;

                        case "defaultmessage":
                        case "message":
                            result.DefaultMessage = value;
                            break;

                        case "defaultadditionalinfoenabled":
                        case "additionalinfoenabled":
                            result.DefaultAdditionalInfoEnabled = ParseBool(value);
                            break;

                        case "defaultadditionalinfo":
                        case "additionalinfo":
                            result.DefaultAdditionalInfo = value;
                            break;
                    }
                }
            }

            if (current != null && currentSection != null)
                result._presetMessages[currentSection] = current;

            return result;
        }

        public PresetMessage? GetPresetMessage(string presetName)
        {
            return _presetMessages.TryGetValue(
                presetName.Trim(),
                out PresetMessage? result)
                ? result
                : null;
        }
    }

    private sealed class PresetMessage
    {
        public string Mode { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string Message { get; set; } = "";
        public bool AdditionalInfoEnabled { get; set; }
        public string AdditionalInfo { get; set; } = "";
    }
}
