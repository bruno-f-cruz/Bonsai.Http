using System.Text.Json.Nodes;

namespace Bonsai.Http.OpenApi;

internal static partial class Program
{
    static void ValidateSchemaReferences(JsonNode? node, JsonObject definitions)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is JsonValue referenceNode)
            {
                var reference = referenceNode.GetValue<string>();
                const string prefix = "#/definitions/";
                if (!reference.StartsWith(prefix) || !definitions.ContainsKey(reference.Substring(prefix.Length).Replace("~1", "/").Replace("~0", "~")))
                    throw new InvalidDataException($"Unresolved or unsupported schema reference: {reference}");
            }
            foreach (var child in obj) ValidateSchemaReferences(child.Value, definitions);
        }
        else if (node is JsonArray array) foreach (var child in array) ValidateSchemaReferences(child, definitions);
    }

    static JsonObject ResolveObject(JsonNode? value, JsonObject document, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (value is JsonObject obj && obj["$ref"] is JsonValue referenceValue)
        {
            var reference = referenceValue.GetValue<string>();
            if (!reference.StartsWith("#/") || !seen.Add(reference))
                throw new InvalidDataException($"{location}: external or cyclic reference '{reference}' is unsupported.");
            value = document;
            foreach (var segment in reference.Substring(2).Split('/')) value = value?[segment.Replace("~1", "/").Replace("~0", "~")];
            if (value is null) throw new InvalidDataException($"{location}: unresolved reference '{reference}'.");
        }
        return value?.DeepClone().AsObject() ?? throw new InvalidDataException($"{location}: expected an object.");
    }

    static void PrepareDocument(JsonObject document)
    {
        if (document["paths"] is not JsonObject paths) throw new InvalidDataException("The document requires a paths object.");
        foreach (var path in paths)
        {
            if (!path.Key.StartsWith("/") || path.Key.Contains('?') || path.Key.Contains('#'))
                throw new InvalidDataException($"Invalid OpenAPI path: {path.Key}");
            var pathItem = path.Value as JsonObject ?? throw new InvalidDataException($"{path.Key}: expected a path item.");
            if (pathItem.ContainsKey("$ref")) throw new InvalidDataException($"{path.Key}: referenced path items are unsupported.");
            foreach (var method in pathItem.ToArray())
            {
                if (!Verbs.Contains(method.Key)) continue;
                var location = $"{method.Key.ToUpperInvariant()} {path.Key}";
                var operation = method.Value as JsonObject ?? throw new InvalidDataException($"{location}: expected an operation.");
                var merged = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
                foreach (var list in new[] { pathItem["parameters"], operation["parameters"] })
                {
                    if (list is null) continue;
                    if (list is not JsonArray array) throw new InvalidDataException($"{location}: parameters must be an array.");
                    var localNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in array)
                    {
                        var parameter = ResolveObject(entry, document, location + " parameter");
                        var name = parameter["name"]?.GetValue<string>() ?? throw new InvalidDataException($"{location}: parameter requires a name.");
                        var place = parameter["in"]?.GetValue<string>();
                        if (place is not ("path" or "query" or "header"))
                            throw new InvalidDataException($"{location}: parameter '{name}' location '{place}' is unsupported.");
                        var key = place + ":" + (place == "header" ? name.ToLowerInvariant() : name);
                        if (!localNames.Add(key)) throw new InvalidDataException($"{location}: duplicate parameter '{name}'.");
                        if (place == "path" && parameter["required"]?.GetValue<bool>() != true)
                            throw new InvalidDataException($"{location}: path parameter '{name}' must be required.");
                        var expectedStyle = place == "query" ? "form" : "simple";
                        if (parameter["style"] is JsonValue style && style.GetValue<string>() != expectedStyle ||
                            parameter["allowReserved"]?.GetValue<bool>() == true)
                            throw new InvalidDataException($"{location}: unsupported serialization for parameter '{name}'.");
                        if (parameter["schema"] is not JsonObject rawSchema)
                            throw new InvalidDataException($"{location}: parameter '{name}' requires a scalar schema.");
                        var schema = ResolveObject(rawSchema, document, location + " parameter " + name);
                        var type = schema["type"]?.GetValue<string>();
                        if (type is not ("string" or "integer" or "number" or "boolean") || schema.ContainsKey("oneOf") || schema.ContainsKey("anyOf"))
                            throw new InvalidDataException($"{location}: parameter '{name}' requires a supported scalar schema.");
                        parameter["schema"] = schema;
                        merged[key] = parameter;
                    }
                }
                operation["parameters"] = new JsonArray(merged.Values.Select(value => (JsonNode)value).ToArray());
                if (operation["requestBody"] is { } body)
                    operation["requestBody"] = ResolveObject(body, document, location + " requestBody");
                if (operation["responses"] is not JsonObject responses || responses.Count == 0)
                    throw new InvalidDataException($"{location}: at least one response is required.");
                foreach (var key in responses.Select(pair => pair.Key).ToArray())
                {
                    if (key != "default" && (!int.TryParse(key, out var code) || code < 100 || code > 599))
                        throw new InvalidDataException($"{location}: response '{key}' is unsupported; use an exact status or default.");
                    responses[key] = ResolveObject(responses[key], document, location + " response " + key);
                    ValidateJsonMedia(responses[key]?["content"], location);
                }
                ValidateJsonMedia(operation["requestBody"]?["content"], location);
                if (operation["requestBody"]?["content"] is JsonObject content && FindJsonSchema(content) is null)
                    throw new InvalidDataException($"{location}: generated request bodies currently require a JSON media type.");
            }
        }
    }

    static void ValidateJsonMedia(JsonNode? content, string location)
    {
        if (content is not JsonObject mediaTypes) return;
        var jsonTypes = mediaTypes.Where(pair => pair.Key.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (jsonTypes.Length > 1)
            throw new InvalidDataException($"{location}: multiple JSON media types require an explicit choice; generation currently supports one per message.");
    }

    static string? ReferencedType(JsonNode? schema, JsonObject definitions, string hint, HashSet<string>? seen = null)
    {
        if (schema is null) return null;
        if (schema is not JsonObject obj) throw new InvalidDataException($"{hint}: boolean or non-object schemas are unsupported.");
        if (obj["$ref"] is JsonValue refValue)
        {
            var reference = refValue.GetValue<string>();
            var prefix = reference.StartsWith("#/components/schemas/") ? "#/components/schemas/" : "#/definitions/";
            if (!reference.StartsWith(prefix)) throw new InvalidDataException($"{hint}: external schema reference '{reference}' is unsupported.");
            var key = reference.Substring(prefix.Length).Replace("~1", "/").Replace("~0", "~");
            if (definitions[key] is not JsonObject target) throw new InvalidDataException($"{hint}: unresolved schema '{reference}'.");
            seen ??= new HashSet<string>(StringComparer.Ordinal);
            if (!seen.Add(key)) throw new InvalidDataException($"{hint}: cyclic schema alias '{key}'.");
            if (target["type"]?.ToJsonString() == "\"object\"" || target.ContainsKey("properties") || target.ContainsKey("allOf") || target.ContainsKey("enum"))
                return ModelIdentifier(key);
            return ReferencedType(target, definitions, hint, seen);
        }
        var type = obj["type"] is JsonArray union
            ? union.Select(value => value?.GetValue<string>()).SingleOrDefault(value => value != "null")
            : obj["type"]?.GetValue<string>();
        switch (type)
        {
            case "string": return "string";
            case "integer": return obj["format"]?.GetValue<string>() == "int32" ? "int" : "long";
            case "number": return "double";
            case "boolean": return "bool";
            case "array": return "System.Collections.Generic.List<" + ReferencedType(obj["items"], definitions, hint + "Item", seen) + ">";
            case "object":
                if (definitions.ContainsKey(hint)) throw new InvalidDataException($"Inline model name collision: {hint}");
                definitions[hint] = ConvertSchema(obj.DeepClone());
                return ModelIdentifier(hint);
            default: throw new InvalidDataException($"{hint}: schema must specify a supported type or component reference.");
        }
    }
}
