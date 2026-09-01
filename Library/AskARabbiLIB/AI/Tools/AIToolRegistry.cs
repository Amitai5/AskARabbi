using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Discovers callable methods only from explicitly supplied provider instances and invokes them without a service locator.</summary>
public sealed class AIToolRegistry : IAIToolRegistry
{
    private static readonly JsonSerializerOptions SchemaJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IReadOnlyDictionary<string, RegisteredTool> tools;

    /// <summary>Creates a registry from explicitly trusted provider instances.</summary>
    /// <param name="providers">Provider instances whose public instance methods may declare <see cref="AIToolAttribute"/>.</param>
    public AIToolRegistry(IEnumerable<object> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var discovered = new Dictionary<string, RegisteredTool>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            foreach (var method in provider.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute<AIToolAttribute>();
                if (attribute is null)
                {
                    continue;
                }

                ValidateMethod(method, attribute);
                var registered = new RegisteredTool(provider, method, attribute, CreateDefinition(method, attribute));
                if (!discovered.TryAdd(attribute.Name, registered))
                {
                    throw new InvalidOperationException($"AI tool name '{attribute.Name}' is registered more than once.");
                }
            }
        }

        tools = discovered;
        Definitions = tools.Values.Select(value => value.Definition).OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<AIToolDefinition> Definitions { get; }

    /// <inheritdoc/>
    public bool MayApply(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        return tools.Values.Any(tool => tool.Attribute.QuestionHints.Any(hint => !string.IsNullOrWhiteSpace(hint) && question.Contains(hint, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc/>
    public async Task<AIToolExecutionResult> ExecuteAsync(string toolName, BinaryData arguments, AIToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!tools.TryGetValue(toolName, out var tool))
        {
            return AIToolExecutionResult.Failure($"Unknown tool '{toolName}'.");
        }

        object?[] invocationArguments;
        try
        {
            invocationArguments = BindArguments(tool.Method, arguments, context, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException)
        {
            return AIToolExecutionResult.Failure($"Invalid arguments for '{toolName}': {exception.Message}");
        }

        try
        {
            var invocation = tool.Method.Invoke(tool.Provider, invocationArguments);
            return invocation switch
            {
                AIToolExecutionResult result => result,
                Task<AIToolExecutionResult> task => await task.ConfigureAwait(false),
                _ => AIToolExecutionResult.Failure($"Tool '{toolName}' returned an unsupported result."),
            };
        }
        catch (TargetInvocationException exception) when (exception.InnerException is OperationCanceledException cancellationException)
        {
            ExceptionDispatchInfo.Capture(cancellationException).Throw();
            throw;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return AIToolExecutionResult.Failure(exception.InnerException.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return AIToolExecutionResult.Failure(exception.Message);
        }
        catch (Exception)
        {
            return AIToolExecutionResult.Failure($"Tool '{toolName}' could not complete its local calculation.");
        }
    }

    private static object?[] BindArguments(MethodInfo method, BinaryData arguments, AIToolExecutionContext context, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(arguments);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Tool arguments must be a JSON object.", nameof(arguments));
        }

        var supplied = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
        var knownNames = method.GetParameters().Where(IsModelParameter).Select(parameter => parameter.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = supplied.Keys.FirstOrDefault(name => !knownNames.Contains(name));
        if (unknown is not null)
        {
            throw new ArgumentException($"Unknown parameter '{unknown}'.", nameof(arguments));
        }

        var values = new object?[method.GetParameters().Length];
        for (var index = 0; index < method.GetParameters().Length; index++)
        {
            var parameter = method.GetParameters()[index];
            if (parameter.ParameterType == typeof(AIToolExecutionContext))
            {
                values[index] = context;
                continue;
            }
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                values[index] = cancellationToken;
                continue;
            }
            if (parameter.Name is not null && supplied.TryGetValue(parameter.Name, out var element))
            {
                values[index] = ConvertValue(element, parameter.ParameterType, parameter.Name);
                continue;
            }
            if (parameter.HasDefaultValue)
            {
                values[index] = parameter.DefaultValue;
                continue;
            }
            if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            {
                values[index] = null;
                continue;
            }

            throw new ArgumentException($"Required parameter '{parameter.Name}' was not supplied.", nameof(arguments));
        }

        return values;
    }

    private static object? ConvertValue(JsonElement value, Type parameterType, string parameterName)
    {
        var effectiveType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (value.ValueKind == JsonValueKind.Null && Nullable.GetUnderlyingType(parameterType) is not null)
        {
            return null;
        }
        if (effectiveType == typeof(string))
        {
            return value.GetString();
        }
        if (effectiveType == typeof(bool))
        {
            return value.GetBoolean();
        }
        if (effectiveType == typeof(int))
        {
            return value.GetInt32();
        }
        if (effectiveType == typeof(DateTime))
        {
            var text = value.GetString();
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : throw new FormatException($"Parameter '{parameterName}' must use an ISO-8601 date-time value.");
        }

        throw new ArgumentException($"Parameter '{parameterName}' uses unsupported type '{parameterType.Name}'.", nameof(parameterType));
    }

    private static AIToolDefinition CreateDefinition(MethodInfo method, AIToolAttribute attribute)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (var parameter in method.GetParameters().Where(IsModelParameter))
        {
            var parameterName = parameter.Name ?? throw new InvalidOperationException($"Tool method '{method.Name}' contains an unnamed parameter.");
            var parameterDescription = parameter.GetCustomAttribute<AIToolParameterAttribute>()?.Description ?? throw new InvalidOperationException($"Tool parameter '{method.Name}.{parameterName}' requires {nameof(AIToolParameterAttribute)}.");
            var effectiveType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            properties.Add(parameterName, CreateParameterSchema(effectiveType, Nullable.GetUnderlyingType(parameter.ParameterType) is not null, parameterDescription));
            if (!parameter.HasDefaultValue && Nullable.GetUnderlyingType(parameter.ParameterType) is null)
            {
                required.Add(parameterName);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
        return new AIToolDefinition(attribute.Name, attribute.Description, BinaryData.FromString(JsonSerializer.Serialize(schema, SchemaJsonOptions)));
    }

    private static object CreateParameterSchema(Type type, bool isNullable, string description)
    {
        var jsonType = type == typeof(string) || type == typeof(DateTime) ? "string" : type == typeof(bool) ? "boolean" : type == typeof(int) ? "integer" : throw new InvalidOperationException($"AI tool parameter type '{type.Name}' is not supported.");
        var schema = new Dictionary<string, object>
        {
            ["type"] = isNullable ? new[] { jsonType, "null" } : jsonType,
            ["description"] = description,
        };
        if (type == typeof(DateTime))
        {
            schema["format"] = "date-time";
        }
        return schema;
    }

    private static void ValidateMethod(MethodInfo method, AIToolAttribute attribute)
    {
        if (attribute.Name.Length > 64 || attribute.Name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new InvalidOperationException($"AI tool name '{attribute.Name}' must contain at most 64 ASCII letters, digits, underscores, or hyphens.");
        }
        if (method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(AIToolExecutionResult) && method.ReturnType != typeof(Task<AIToolExecutionResult>))
        {
            throw new InvalidOperationException($"AI tool method '{method.Name}' must be a non-generic instance method returning {nameof(AIToolExecutionResult)} or Task<{nameof(AIToolExecutionResult)}>.");
        }
        foreach (var parameter in method.GetParameters())
        {
            if (!IsModelParameter(parameter) || IsSupportedModelType(parameter.ParameterType))
            {
                continue;
            }
            throw new InvalidOperationException($"AI tool parameter '{method.Name}.{parameter.Name}' uses unsupported type '{parameter.ParameterType.Name}'.");
        }
    }

    private static bool IsModelParameter(ParameterInfo parameter) => parameter.ParameterType != typeof(AIToolExecutionContext) && parameter.ParameterType != typeof(CancellationToken);

    private static bool IsSupportedModelType(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        return effectiveType == typeof(string) || effectiveType == typeof(bool) || effectiveType == typeof(int) || effectiveType == typeof(DateTime);
    }

    private sealed record RegisteredTool(object Provider, MethodInfo Method, AIToolAttribute Attribute, AIToolDefinition Definition);
}
