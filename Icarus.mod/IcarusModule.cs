using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using WindowsGSH.Core.Modules;
using WindowsGSH.Core.Query;
using WindowsGSH.Core.Readiness;
using WindowsGSH.Core.Servers;

namespace WindowsGSH.Modules.Icarus;

public sealed class IcarusModule : IGameServerModule, IManifestBackedModule, IModuleExistingServerImportCapability, IModulePortCapability, IModuleReadinessCapability
{
    private const string ConfigRelativePath = "Icarus/Saved/Config/WindowsServer/ServerSettings.ini";
    private const string ConfigSection = "/Script/Icarus.DedicatedServerSettings";

    private ModuleManifest? _manifest;
    private string _moduleDirectory = AppContext.BaseDirectory;

    private ModuleManifest Manifest => _manifest ??= ModuleManifest.Load(Path.Combine(_moduleDirectory, "module.json"));

    public string Id => Manifest.Id;

    public string Name => Manifest.Name;

    public string Version => Manifest.Version;

    public ModuleCapabilities Capabilities => Manifest.ToCapabilities(supportsQuery: true, supportsRcon: false);

    public SteamInstallDefinition? SteamInstall => Manifest.ToSteamInstall();

    public ModuleRuntimeDefinition Runtime => Manifest.ToRuntime();

    public void Configure(ModuleManifest manifest, string moduleDirectory)
    {
        _manifest = manifest;
        _moduleDirectory = moduleDirectory;
    }

    public bool CanImport(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var installPath = ResolveExistingInstallPath(path);
        return File.Exists(Path.Combine(installPath, Runtime.StartPath));
    }

    public async Task<ModuleExistingServerImportProbe> PreviewImportAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = Path.GetFullPath(path);
        var installPath = ResolveExistingInstallPath(sourcePath);
        var probeInstance = new ServerInstance(
            Id: Path.GetFileName(sourcePath),
            Name: "Icarus Dedicated Server",
            ModuleId: Id,
            ServerFolder: sourcePath,
            InstallPath: installPath,
            ConfigPath: Path.Combine(installPath, ConfigRelativePath),
            Settings: new Dictionary<string, object?>());

        var settings = new Dictionary<string, object?>(
            await ReadConfigFileSettingsAsync(probeInstance, cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        if (!File.Exists(GetConfigPath(probeInstance)))
        {
            warnings.Add("Icarus ServerSettings.ini was not found. WindowsGSH will use module defaults for missing values.");
        }

        var sourceName = GetSetting(settings, "server.name", Path.GetFileName(sourcePath));
        return new ModuleExistingServerImportProbe(
            SourceName: sourceName,
            InstallPath: installPath,
            Settings: settings,
            Warnings: warnings);
    }

    public IReadOnlyList<ConfigFieldDefinition> GetConfigFields()
    {
        return Manifest.ToConfigFields();
    }

    public IReadOnlyList<ServerPortDefinition> GetPorts()
    {
        return Manifest.ToPorts();
    }

    public Task<IReadOnlyList<ReadinessCheckResult>> CheckReadinessAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var checks = new List<ReadinessCheckResult>();
        var executablePath = Path.Combine(instance.InstallPath, Runtime.StartPath);
        checks.Add(File.Exists(executablePath)
            ? ReadinessCheckResult.Pass("Icarus executable", $"Found: {executablePath}")
            : ReadinessCheckResult.Fail("Icarus executable", $"Missing {Runtime.StartPath}. Run install/update with SteamCMD app {SteamInstall?.AppId}."));

        var configPath = GetConfigPath(instance);
        checks.Add(File.Exists(configPath)
            ? ReadinessCheckResult.Pass("Icarus configuration", $"Found: {configPath}")
            : ReadinessCheckResult.Info("Icarus configuration", "ServerSettings.ini will be created before the first start."));
        checks.Add(ReadinessCheckResult.Warning("Headless shutdown", "A save-safe headless shutdown channel has not been proven; avoid rebooting or signing out while the server is running."));

        return Task.FromResult<IReadOnlyList<ReadinessCheckResult>>(checks);
    }

    public IReadOnlyList<ServerAddonDefinition> GetAddonDefinitions()
    {
        return Manifest.ToAddons();
    }

    public IReadOnlyList<ServerBackupTargetDefinition> GetBackupTargets()
    {
        return Manifest.ToBackupTargets();
    }

    public ServerAddonStatus GetAddonStatus(ServerInstance instance, string addonId)
    {
        return new ServerAddonStatus(addonId, IsInstalled: false, IsEnabled: false, StatusText: "Unknown addon");
    }

    public Task InstallAddonAsync(ServerInstance instance, string addonId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Icarus module does not define addons yet.");
    }

    public Task RemoveAddonAsync(ServerInstance instance, string addonId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Icarus module does not define addons yet.");
    }

    public string GetServerName(IReadOnlyDictionary<string, object?> settings)
    {
        return GetSetting(settings, "server.name", "Icarus Dedicated Server");
    }

    public ServerDisplayInfo GetDisplayInfo(ServerInstance instance)
    {
        return new ServerDisplayInfo(
            IpAddress: GetSetting(instance, "network.publicIp", "0.0.0.0"),
            Port: GetSetting(instance, "network.port", "17777"),
            MaxPlayers: GetSetting(instance, "server.maxPlayers", "8"));
    }

    public Task<IReadOnlyDictionary<string, object?>> ReadConfigFileSettingsAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var values = TryLoadSettings(instance);
        if (values.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<string, object?>>(settings);
        }

        SetText(settings, "server.name", values, "SessionName");
        SetText(settings, "server.joinPassword", values, "JoinPassword");
        SetText(settings, "server.adminPassword", values, "AdminPassword");
        SetInt(settings, "server.maxPlayers", values, "MaxPlayers");
        SetDouble(settings, "server.shutdownIfNotJoinedFor", values, "ShutdownIfNotJoinedFor");
        SetDouble(settings, "server.shutdownIfEmptyFor", values, "ShutdownIfEmptyFor");
        SetBool(settings, "prospect.allowNonAdminsToLaunch", values, "AllowNonAdminsToLaunchProspects");
        SetBool(settings, "prospect.allowNonAdminsToDelete", values, "AllowNonAdminsToDeleteProspects");
        SetText(settings, "prospect.loadProspect", values, "LoadProspect");
        SetText(settings, "prospect.createProspect", values, "CreateProspect");
        SetBool(settings, "prospect.resumeProspect", values, "ResumeProspect");
        SetText(settings, "prospect.lastProspectName", values, "LastProspectName");

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(settings);
    }

    public Task WriteConfigFileSettingsAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        WriteIcarusSettings(instance);
        return Task.CompletedTask;
    }

    public Task<InstallPlan> CreateInstallPlanAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var plan = new InstallPlan(
            Tool: "steamcmd",
            Arguments: $"+force_install_dir \"{instance.InstallPath}\" +login anonymous +app_update {SteamInstall?.AppId} validate +quit",
            WorkingDirectory: instance.InstallPath,
            Notes:
            [
                "Icarus Dedicated Server is available anonymously through SteamCMD.",
                "WindowsGSH writes Icarus/Saved/Config/WindowsServer/ServerSettings.ini from module settings.",
                "WindowsGSH pins Icarus' user-data directory to the managed install so configuration, logs and backups use the same Saved tree."
            ]);

        return Task.FromResult(plan);
    }

    public Task<ProcessStartInfo> CreateStartInfoAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        WriteIcarusSettings(instance);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(instance.InstallPath, Runtime.StartPath),
            WorkingDirectory = instance.InstallPath,
            Arguments = BuildLaunchArguments(instance),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        return Task.FromResult(startInfo);
    }

    public async Task<Process?> StartAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        if (!IsInstallValid(instance))
        {
            throw new FileNotFoundException("Icarus server executable was not found.", Path.Combine(instance.InstallPath, Runtime.StartPath));
        }

        var process = new Process
        {
            StartInfo = await CreateStartInfoAsync(instance, cancellationToken),
            EnableRaisingEvents = true
        };

        process.Start();
        return process;
    }

    public Task<IReadOnlyList<Process>> StartAddonProcessesAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Process>>([]);
    }

    public Task StopAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        return StopProcessesAsync(instance, cancellationToken);
    }

    private async Task StopProcessesAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        foreach (var process in ServerProcessLocator.FindProcesses(this, instance.InstallPath))
        {
            using (process)
            {
                if (process.HasExited)
                {
                    continue;
                }

                try
                {
                    process.CloseMainWindow();
                    await Task.Delay(5000, cancellationToken);
                }
                catch
                {
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
        }
    }

    public bool IsInstallValid(ServerInstance instance)
    {
        return File.Exists(Path.Combine(instance.InstallPath, Runtime.StartPath));
    }

    public string? GetConsoleLogPath(ServerInstance instance)
    {
        return Path.Combine(instance.InstallPath, "Icarus", "Saved", "Logs");
    }

    public Task<string> ExecuteRconCommandAsync(ServerInstance instance, string command, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Icarus RCON is not implemented by this module.");
    }

    public async Task<QueryResult> QueryAsync(ServerInstance instance, CancellationToken cancellationToken)
    {
        var host = GetLocalQueryHost();
        var port = int.TryParse(GetSetting(instance, "network.queryPort", "27015"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort)
            ? parsedPort
            : 27015;

        try
        {
            var info = await new SourceA2sClient().QueryInfoAsync(host, port, TimeSpan.FromSeconds(2), cancellationToken);
            return new QueryResult(
                ModuleServerStatus.Online,
                OnlinePlayers: info.Players,
                MaxPlayers: info.MaxPlayers,
                Version: info.Version,
                Map: info.Map,
                Game: info.Game,
                QueryDurationMilliseconds: info.QueryDurationMilliseconds,
                Players: info.PlayerRows,
                DetailMessage: info.DetailMessage,
                Protocol: "A2S",
                Message: string.IsNullOrWhiteSpace(info.Map)
                    ? $"A2S responded from {host}:{port}."
                    : $"A2S responded from {host}:{port}. Map: {info.Map}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new QueryResult(ModuleServerStatus.Offline, Message: $"A2S query to {host}:{port} timed out.");
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException)
        {
            return new QueryResult(ModuleServerStatus.Offline, Message: $"A2S query to {host}:{port} failed: {ex.Message}");
        }
    }

    private static string GetLocalQueryHost()
    {
        return "127.0.0.1";
    }

    private string BuildLaunchArguments(ServerInstance instance)
    {
        var args = new List<string>
        {
            "-log",
            $"-UserDir={WindowsCommandLineEscaper.Quote(Path.Combine(instance.InstallPath, "Icarus"))}",
            $"-PORT={GetSetting(instance, "network.port", "17777")}",
            $"-QueryPort={GetSetting(instance, "network.queryPort", "27015")}",
            $"-SteamServerName={WindowsCommandLineEscaper.Quote(GetSetting(instance, "server.name", "Icarus Dedicated Server"))}"
        };

        var additional = SanitizeAdditionalArguments(GetSetting(instance, "server.additionalArguments", ""));
        if (!string.IsNullOrWhiteSpace(additional))
        {
            args.Add(additional.Trim());
        }

        return string.Join(' ', args);
    }

    private static void WriteIcarusSettings(ServerInstance instance)
    {
        var configPath = GetConfigPath(instance);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var values = File.Exists(configPath)
            ? ParseIni(File.ReadAllText(configPath))
            : ParseIni(GetDefaultConfigText());

        SetValue(values, "SessionName", GetSetting(instance, "server.name", "Icarus Dedicated Server"));
        SetValue(values, "JoinPassword", GetSetting(instance, "server.joinPassword", ""));
        SetValue(values, "MaxPlayers", GetSetting(instance, "server.maxPlayers", "8"));
        SetValue(values, "AdminPassword", GetSetting(instance, "server.adminPassword", ""));
        SetValue(values, "ShutdownIfNotJoinedFor", GetDouble(instance, "server.shutdownIfNotJoinedFor", 300.0));
        SetValue(values, "ShutdownIfEmptyFor", GetDouble(instance, "server.shutdownIfEmptyFor", 300.0));
        SetValue(values, "AllowNonAdminsToLaunchProspects", GetBool(instance, "prospect.allowNonAdminsToLaunch", true));
        SetValue(values, "AllowNonAdminsToDeleteProspects", GetBool(instance, "prospect.allowNonAdminsToDelete", false));
        SetValue(values, "LoadProspect", GetSetting(instance, "prospect.loadProspect", ""));
        SetValue(values, "CreateProspect", GetSetting(instance, "prospect.createProspect", ""));
        SetValue(values, "ResumeProspect", GetBool(instance, "prospect.resumeProspect", true));
        SetValue(values, "LastProspectName", GetSetting(instance, "prospect.lastProspectName", ""));

        File.WriteAllText(configPath, RenderIni(values), Encoding.UTF8);
    }

    private static Dictionary<string, string> TryLoadSettings(ServerInstance instance)
    {
        var configPath = GetConfigPath(instance);
        return File.Exists(configPath)
            ? ParseIni(File.ReadAllText(configPath))
            : [];
    }

    private static Dictionary<string, string> ParseIni(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inTargetSection = false;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inTargetSection = string.Equals(trimmed[1..^1], ConfigSection, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inTargetSection)
            {
                continue;
            }

            var index = trimmed.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            values[trimmed[..index].Trim()] = trimmed[(index + 1)..].Trim();
        }

        return values;
    }

    private static string RenderIni(IReadOnlyDictionary<string, string> values)
    {
        var builder = new StringBuilder();
        builder.AppendLine("; Generated by WindowsGSH. Edit settings from WindowsGSH to avoid overwriting manual changes.");
        builder.AppendLine($"[{ConfigSection}]");
        foreach (var key in DefaultIniOrder)
        {
            if (values.TryGetValue(key, out var value))
            {
                builder.Append(key).Append('=').AppendLine(value);
            }
        }

        foreach (var item in values.Where(item => !DefaultIniOrder.Contains(item.Key, StringComparer.OrdinalIgnoreCase)))
        {
            builder.Append(item.Key).Append('=').AppendLine(item.Value);
        }

        return builder.ToString();
    }

    private static string GetDefaultConfigText()
    {
        return """
[/Script/Icarus.DedicatedServerSettings]
SessionName=
JoinPassword=
MaxPlayers=
AdminPassword=
ShutdownIfNotJoinedFor=300.000000
ShutdownIfEmptyFor=300.000000
AllowNonAdminsToLaunchProspects=True
AllowNonAdminsToDeleteProspects=False
LoadProspect=
CreateProspect=
ResumeProspect=True
LastProspectName=
""";
    }

    private static readonly string[] DefaultIniOrder =
    [
        "SessionName",
        "JoinPassword",
        "MaxPlayers",
        "AdminPassword",
        "ShutdownIfNotJoinedFor",
        "ShutdownIfEmptyFor",
        "AllowNonAdminsToLaunchProspects",
        "AllowNonAdminsToDeleteProspects",
        "LoadProspect",
        "CreateProspect",
        "ResumeProspect",
        "LastProspectName"
    ];

    private static void SetValue(Dictionary<string, string> values, string key, string value)
    {
        values[key] = value;
    }

    private static void SetValue(Dictionary<string, string> values, string key, bool value)
    {
        values[key] = value ? "True" : "False";
    }

    private static void SetValue(Dictionary<string, string> values, string key, double value)
    {
        values[key] = value.ToString("0.000000", CultureInfo.InvariantCulture);
    }

    private static string SanitizeAdditionalArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return string.Empty;
        }

        var tokens = TokenizeCommandLine(arguments);
        var sanitized = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var switchName = token.Text.Split('=', 2)[0];
            if (IsModuleManagedArgument(switchName))
            {
                if (!token.Text.Contains('=') && index + 1 < tokens.Count && !tokens[index + 1].Text.StartsWith("-", StringComparison.Ordinal))
                {
                    index++;
                }

                continue;
            }

            sanitized.Add(token.WasQuoted ? QuoteCommandLineToken(token.Text) : token.Text);
        }

        return string.Join(' ', sanitized);
    }

    private static bool IsModuleManagedArgument(string switchName)
    {
        return switchName.Equals("-log", StringComparison.OrdinalIgnoreCase) ||
               switchName.Equals("-UserDir", StringComparison.OrdinalIgnoreCase) ||
               switchName.Equals("-PORT", StringComparison.OrdinalIgnoreCase) ||
               switchName.Equals("-QueryPort", StringComparison.OrdinalIgnoreCase) ||
               switchName.Equals("-SteamServerName", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CommandLineToken(string Text, bool WasQuoted);

    private static List<CommandLineToken> TokenizeCommandLine(string commandLine)
    {
        var tokens = new List<CommandLineToken>();
        var current = new StringBuilder();
        var inQuotes = false;
        var tokenWasQuoted = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var ch = commandLine[index];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                tokenWasQuoted = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                AddCurrentToken();
                continue;
            }

            current.Append(ch);
        }

        AddCurrentToken();
        return tokens;

        void AddCurrentToken()
        {
            if (current.Length == 0 && !tokenWasQuoted)
            {
                return;
            }

            tokens.Add(new CommandLineToken(current.ToString(), tokenWasQuoted));
            current.Clear();
            tokenWasQuoted = false;
        }
    }

    private static string QuoteCommandLineToken(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string GetConfigPath(ServerInstance instance)
    {
        return Path.Combine(instance.InstallPath, ConfigRelativePath);
    }

    private string ResolveExistingInstallPath(string path)
    {
        var sourcePath = Path.GetFullPath(path);
        if (File.Exists(Path.Combine(sourcePath, Runtime.StartPath)))
        {
            return sourcePath;
        }

        var windowsGsmInstallPath = Path.Combine(sourcePath, "serverfiles");
        if (File.Exists(Path.Combine(windowsGsmInstallPath, Runtime.StartPath)))
        {
            return windowsGsmInstallPath;
        }

        return sourcePath;
    }

    private static string GetSetting(ServerInstance instance, string key, string fallback)
    {
        return GetSetting(instance.Settings, key, fallback);
    }

    private static string GetSetting(IReadOnlyDictionary<string, object?> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()!.Trim()
            : fallback;
    }

    private static bool GetBool(ServerInstance instance, string key, bool fallback)
    {
        return instance.Settings.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;
    }

    private static double GetDouble(ServerInstance instance, string key, double fallback)
    {
        return instance.Settings.TryGetValue(key, out var value) &&
               double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static void SetText(Dictionary<string, object?> settings, string key, Dictionary<string, string> values, string iniKey)
    {
        if (values.TryGetValue(iniKey, out var value))
        {
            settings[key] = value;
        }
    }

    private static void SetInt(Dictionary<string, object?> settings, string key, Dictionary<string, string> values, string iniKey)
    {
        if (values.TryGetValue(iniKey, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            settings[key] = parsed;
        }
    }

    private static void SetDouble(Dictionary<string, object?> settings, string key, Dictionary<string, string> values, string iniKey)
    {
        if (values.TryGetValue(iniKey, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            settings[key] = parsed;
        }
    }

    private static void SetBool(Dictionary<string, object?> settings, string key, Dictionary<string, string> values, string iniKey)
    {
        if (values.TryGetValue(iniKey, out var value) && bool.TryParse(value, out var parsed))
        {
            settings[key] = parsed;
        }
    }
}
