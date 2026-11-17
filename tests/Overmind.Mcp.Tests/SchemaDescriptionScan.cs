using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Overmind.Mcp.Tests;

internal static class SchemaDescriptionScan
{
    public sealed record Surface(
        int PropertiesScanned,
        int ParametersScanned,
        IReadOnlyList<string> PropertyDescriptions,
        IReadOnlyList<string> SchemaTypedParameters);

    private const BindingFlags PropertyFlags =
        BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type[] InjectedParameterTypes = [typeof(CancellationToken)];

    public static Surface Measure(Assembly adapterAssembly)
    {
        var propertyDescriptions = new List<string>();
        var propertiesScanned = 0;
        foreach (var type in adapterAssembly.GetTypes())
        {
            foreach (var property in type.GetProperties(PropertyFlags))
            {
                propertiesScanned++;
                if (property.GetCustomAttribute<DescriptionAttribute>() is not null)
                {
                    propertyDescriptions.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        var schemaTypedParameters = new List<string>();
        var parametersScanned = 0;
        foreach (var method in ToolDescriptionScanner.ToolMethods(adapterAssembly))
        {
            foreach (var parameter in method.GetParameters())
            {
                parametersScanned++;
                var type = parameter.ParameterType;
                if (IsFlatSchemaType(type) || InjectedParameterTypes.Contains(type))
                {
                    continue;
                }

                schemaTypedParameters.Add($"{method.Name}.{parameter.Name}: {type.Name}");
            }
        }

        return new Surface(propertiesScanned, parametersScanned, propertyDescriptions, schemaTypedParameters);
    }

    private static bool IsFlatSchemaType(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        if (unwrapped.IsPrimitive
            || unwrapped.IsEnum
            || unwrapped == typeof(string)
            || unwrapped == typeof(decimal)
            || unwrapped == typeof(DateTime)
            || unwrapped == typeof(DateTimeOffset)
            || unwrapped == typeof(DateOnly)
            || unwrapped == typeof(TimeOnly)
            || unwrapped == typeof(Guid)
            || unwrapped == typeof(JsonElement)
            || unwrapped == typeof(JsonNode))
        {
            return true;
        }

        if (unwrapped.IsArray && unwrapped.GetElementType() is { } element)
        {
            return IsFlatSchemaType(element);
        }

        if (unwrapped.IsGenericType)
        {
            var definition = unwrapped.GetGenericTypeDefinition();
            var flatContainer = definition == typeof(List<>)
                || definition == typeof(IList<>)
                || definition == typeof(ICollection<>)
                || definition == typeof(IEnumerable<>)
                || definition == typeof(IReadOnlyList<>)
                || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(Dictionary<,>)
                || definition == typeof(IDictionary<,>);

            return flatContainer && unwrapped.GetGenericArguments().All(IsFlatSchemaType);
        }

        return false;
    }
}
