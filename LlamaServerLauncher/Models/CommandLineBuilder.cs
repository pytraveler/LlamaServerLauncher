using System.Text;

namespace LlamaServerLauncher.Models;

public static class CommandLineBuilder
{
    public static string Build(ServerConfiguration config)
    {
        var args = new List<string>();
        
        // Отслеживаем уже обработанные свойства для избежания задваивания синонимов
        var processedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalizedCustomArgs = CommandLineParser.NormalizeSpecialCharacters(config.CustomArguments);
        Dictionary<string, string?> customArgValues = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(normalizedCustomArgs))
        {
            var parsed = CommandLineParser.ParseArguments(normalizedCustomArgs);
            customArgValues = CommandLineParser.GetArgumentValues(parsed);
        }

        // Функция проверки - переопределен ли аргумент (включая синонимы)
        bool IsOverriddenByAlias(string flag)
        {
            // Проверяем сам флаг
            if (customArgValues.ContainsKey(flag))
                return true;
            
            // Проверяем все известные синонимы этого флага
            foreach (var kvp in ServerConfiguration.KnownArguments)
            {
                if (kvp.Value.PropertyName == GetPropertyNameForFlag(flag) && 
                    customArgValues.ContainsKey(kvp.Key))
                {
                    return true;
                }
            }
            
            return false;
        }

        // Получить значение из CustomArguments для конкретного флага или его синонимов
        string? GetCustomValue(string flag)
        {
            if (customArgValues.TryGetValue(flag, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;  // Оставляем как есть - с кавычками если они были
            
            foreach (var kvp in ServerConfiguration.KnownArguments)
            {
                if (kvp.Value.PropertyName == GetPropertyNameForFlag(flag))
                {
                    if (customArgValues.TryGetValue(kvp.Key, out val) && !string.IsNullOrWhiteSpace(val))
                        return val;  // Оставляем как есть - с кавычками если они были
                }
            }
            
            return null;
        }

        // Проверить, указан ли флаг в CustomArguments (независимо от значения)
        bool IsFlagPresentInCustomArgs(string flag)
        {
            if (customArgValues.ContainsKey(flag))
                return true;
            
            foreach (var kvp in ServerConfiguration.KnownArguments)
            {
                if (kvp.Value.PropertyName == GetPropertyNameForFlag(flag))
                {
                    if (customArgValues.ContainsKey(kvp.Key))
                        return true;
                }
            }
            
            return false;
        }

        // Получить конкретный флаг из CustomArguments (основной или инвертированный)
        string? GetActualCustomFlag(string flag, string invertedFlag = "")
        {
            string? propertyName = GetPropertyNameForFlag(flag);
            if (propertyName == null)
            {
                return customArgValues.ContainsKey(flag) ? flag : null;
            }
            
            // Проверяем все флаги с таким же PropertyName
            foreach (var kvp in ServerConfiguration.KnownArguments)
            {
                if (kvp.Value.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (customArgValues.ContainsKey(kvp.Key))
                        return kvp.Key;
                }
            }
            
            return null;
        }

        void AddIfNotOverridden(List<string> list, string flag, string? uiValue)
        {
            // Проверяем: если флаг есть в CustomArguments со значением - используем его
            string? customValue = GetCustomValue(flag);
            
            if (customValue != null)
            {
                // Есть значение из CustomArguments - добавляем его с кавычками если нужно
                list.Add($"{flag} {QuoteIfNeeded(customValue)}");
            }
            else if (!IsFlagPresentInCustomArgs(flag) && !string.IsNullOrEmpty(uiValue))
            {
                // Флага нет в CustomArguments и есть UI значение - добавляем из UI
                list.Add($"{flag} {uiValue}");
            }
            // Если флаг указан в CustomArguments но БЕЗ значения - ничего не добавляем
            // Это позволяет "отключить" параметр через CustomArguments
        }

        void AddBoolOnOff(List<string> list, string flag, bool? uiValue, string invertedFlag = "")
        {
            // Проверяем, указал ли пользователь этот аргумент в любой форме
            string? customValue = GetCustomValue(flag);
            
            if (customValue != null)
            {
                // Пользователь явно указал значение - используем его
                list.Add($"{flag} {customValue}");
            }
            else if (uiValue.HasValue)
            {
                // Нет в CustomArguments, используем UI значение
                list.Add($"{flag} {(uiValue.Value ? "on" : "off")}");
            }
        }

        void AddBoolFlag(List<string> list, string flag, bool? uiValue, string invertedFlag = "")
        {
            // Проверяем, не обработали ли мы уже это свойство (для избежания задваивания синонимов)
            string? propertyName = GetPropertyNameForFlag(flag);
            if (propertyName != null && !processedProperties.Add(propertyName))
                return; // Уже обработано
            
            // Получаем конкретный флаг который указал пользователь (основной или инвертированный)
            string? actualFlag = GetActualCustomFlag(flag, invertedFlag);
            
            if (actualFlag != null)
            {
                // Пользователь явно указал флаг в CustomArguments - добавляем ТОЧНО тот флаг который он указал
                list.Add(actualFlag);
                return;
            }

            // Нет в CustomArguments - используем UI значение
            if (!uiValue.HasValue)
                return;

            if (uiValue.Value)
            {
                list.Add(flag);
            }
            else if (!string.IsNullOrEmpty(invertedFlag))
            {
                list.Add(invertedFlag);
            }
        }

        if (!IsOverriddenByAlias("-m"))
        {
            if (!string.IsNullOrEmpty(config.ModelPath))
                args.Add($"-m \"{EscapePath(config.ModelPath)}\"");
            else if (!string.IsNullOrEmpty(config.ModelsDir))
                args.Add($"--models-dir \"{EscapePath(config.ModelsDir)}\"");
        }

        AddIfNotOverridden(args, "--host", config.Host);
        AddIfNotOverridden(args, "--port", config.Port.ToString());
        AddIfNotOverridden(args, "-c", config.ContextSize?.ToString());
        AddIfNotOverridden(args, "-t", config.Threads?.ToString());
        AddIfNotOverridden(args, "-ngl", config.GpuLayers?.ToString());
        AddIfNotOverridden(args, "--temp", config.Temperature?.ToString());
        AddIfNotOverridden(args, "-n", config.MaxTokens?.ToString());
        AddIfNotOverridden(args, "-b", config.BatchSize?.ToString());
        AddIfNotOverridden(args, "-ub", config.UBatchSize?.ToString());
        AddIfNotOverridden(args, "--min-p", config.MinP?.ToString());
        
        // Проверяем, переопределён ли -mm через CustomArguments
        string? mmCustomValue = GetCustomValue("-mm");
        
        if (mmCustomValue != null)
        {
            // Есть значение из CustomArguments - используем его
            args.Add($"-mm {QuoteIfNeeded(mmCustomValue)}");
        }
        else if (!string.IsNullOrEmpty(config.MmprojPath))
        {
            // Нет в CustomArguments, используем UI значение
            args.Add($"-mm \"{EscapePath(config.MmprojPath)}\"");
        }
        
        AddIfNotOverridden(args, "-ctk", config.CacheTypeK);
        AddIfNotOverridden(args, "-ctv", config.CacheTypeV);

        AddIfNotOverridden(args, "--top-k", config.TopK?.ToString());
        AddIfNotOverridden(args, "--top-p", config.TopP?.ToString());
        AddIfNotOverridden(args, "--repeat-penalty", config.RepeatPenalty?.ToString());

        AddBoolOnOff(args, "-fa", config.FlashAttention);

        // Обработка --webui / --no-webui - используем единую функцию для поиска флага
        string? actualWebuiFlag = GetActualCustomFlag("--webui", "--no-webui");
        
        if (actualWebuiFlag != null)
        {
            // Пользователь указал конкретный флаг в CustomArguments
            args.Add(actualWebuiFlag);
        }
        else if (config.EnableWebUI == true)
        {
            args.Add("--webui");
        }
        else if (config.EnableWebUI == false)
        {
            args.Add("--no-webui");
        }

        AddBoolFlag(args, "--embedding", config.EmbeddingMode);
        AddBoolFlag(args, "--embeddings", config.EmbeddingMode);
        AddBoolFlag(args, "--slots", config.EnableSlots, "--no-slots");
        AddBoolFlag(args, "--metrics", config.EnableMetrics);

        AddIfNotOverridden(args, "--api-key", string.IsNullOrEmpty(config.ApiKey) ? null : $"\"{config.ApiKey}\"");
        AddIfNotOverridden(args, "--log-file", string.IsNullOrEmpty(config.LogFilePath) ? null : $"\"{config.LogFilePath}\"");
        // AddIfNotOverridden(args, "-a", string.IsNullOrEmpty(config.Alias) ? null : $"\"{config.Alias}\"");
        AddIfNotOverridden(args, "--alias", string.IsNullOrEmpty(config.Alias) ? null : $"\"{config.Alias}\"");

        // Обработка -v / --verbose
        string? actualVerboseFlag = GetActualCustomFlag("-v", "--verbose");
        
        if (actualVerboseFlag != null)
        {
            // Пользователь явно указал -v в CustomArguments
            args.Add(actualVerboseFlag);
        }
        else if (config.VerboseLogging)
        {
            args.Add("-v");
        }

        AddRemainingCustomArgs(args, normalizedCustomArgs, customArgValues);

        return string.Join(" ", args);
    }

    private static string? GetPropertyNameForFlag(string flag)
    {
        if (ServerConfiguration.KnownArguments.TryGetValue(flag, out var mapping))
            return mapping.PropertyName;
        return null;
    }

    private static string QuoteIfNeeded(string value)
    {
        // Добавляем кавычки если значение содержит пробелы или спецсимволы
        if (value.Contains(' ') || value.Contains('\t') || value.Contains('"') || value.Contains('\''))
        {
            // Экранируем внутренние двойные кавычки
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
        return value;
    }

    private static string? StripQuotes(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        
        // Удаляем внешние кавычки (одинарные или двойные)
        if ((value.StartsWith('"') && value.EndsWith('"')) || 
            (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            return value.Length > 2 ? value[1..^1] : string.Empty;
        }
        
        return value;
    }

    private static string EscapePath(string path)
    {
        // Экранируем \ для записи в BAT файл: \ -> \\
        return path.Replace("\\", "\\\\");
    }

    public static string UnescapePath(string path)
    {
        // Обратное преобразование при чтении из BAT файла
        // Строка @"\\" в C# — это два обратных слэша, @"\" — один
        // Из .\\models\\... (два слэша в строке) делаем .\models\... (один слэш)
        if (string.IsNullOrEmpty(path))
            return path;
        
        return path.Replace(@"\\", @"\");
    }

    // Список свойств, содержащих пути файловой системы, которые нужно декодировать при чтении из BAT
    private static readonly HashSet<string> PathProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "ModelPath",
        "ModelsDir",
        "MmprojPath",
        "ExecutablePath"
    };

    public static bool IsPathProperty(string propertyName) => PathProperties.Contains(propertyName);

    private static void AddRemainingCustomArgs(List<string> args, string normalizedCustomArgs, Dictionary<string, string?> usedCustomValues)
    {
        if (string.IsNullOrEmpty(normalizedCustomArgs))
            return;

        var parsed = CommandLineParser.ParseArguments(normalizedCustomArgs);
        var usedFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Добавляем все известные флаги в список использованных
        foreach (var kvp in ServerConfiguration.KnownArguments)
        {
            if (usedCustomValues.ContainsKey(kvp.Key) || 
                (usedCustomValues.ContainsKey(kvp.Key) && usedCustomValues[kvp.Key] != null))
            {
                usedFlags.Add(kvp.Key);
            }
        }

        for (int i = 0; i < parsed.Count; i++)
        {
            string arg = parsed[i];
            if (!arg.StartsWith("-"))
                continue;

            if (usedFlags.Contains(arg))
                continue;

            if (i + 1 < parsed.Count && !parsed[i + 1].StartsWith("-"))
            {
                // Добавляем кавычки если значение содержит пробелы
                args.Add($"{arg} {QuoteIfNeeded(parsed[i + 1])}");
                i++;
            }
            else
            {
                args.Add(arg);
            }
        }
    }

    public static string BuildFullCommand(ServerConfiguration config)
    {
        var args = Build(config);
        return $"\"{config.ExecutablePath}\" {args}";
    }
}