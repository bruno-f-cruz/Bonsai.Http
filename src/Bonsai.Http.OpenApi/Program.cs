using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;

namespace Bonsai.Http.OpenApi;

internal static class Program
{
    static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
        { "get", "post", "put", "patch", "delete", "head", "options", "trace" };

    static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: bonsai-http-openapi <openapi.json|yaml> <output-directory> [namespace]");
            return 2;
        }
        try
        {
            var input = Path.GetFullPath(args[0]);
            var output = Path.GetFullPath(args[1]);
            var ns = args.Length > 2 ? args[2] : "GeneratedHttpApi";
            if (!IsNamespace(ns)) throw new ArgumentException("Invalid namespace.");
            ns = string.Join('.', ns.Split('.').Select(ModelIdentifier));
            var document = ReadDocument(input);
            if (document["openapi"]?.GetValue<string>() is not string version || !version.StartsWith("3."))
                throw new InvalidDataException("Only OpenAPI 3.x documents are supported.");
            Directory.CreateDirectory(output);
            var schema = new JsonObject { ["title"] = "ApiModels", ["type"] = "object" };
            var definitions = new JsonObject();
            if (document["components"]?["schemas"] is JsonObject components)
            {
                foreach (var component in components)
                    definitions[component.Key] = ConvertSchema(component.Value?.DeepClone());
            }
            schema["definitions"] = definitions;
            var schemaPath = Path.Combine(output, "OpenApi.Models.schema.json");
            await File.WriteAllTextAsync(schemaPath, schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (definitions.Count > 0) await RunSgen(schemaPath, output, ns);
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            if (document["paths"] is JsonObject paths)
            {
                foreach (var path in paths)
                {
                    if (path.Value is not JsonObject pathItem) continue;
                    foreach (var method in pathItem)
                    {
                        if (!Verbs.Contains(method.Key) || method.Value is not JsonObject operation) continue;
                        var name = ModelIdentifier(operation["operationId"]?.GetValue<string>() ?? $"{method.Key}_{path.Key}");
                        if (!usedNames.Add(name)) throw new InvalidDataException($"Duplicate operation name: {name}");
                        var code = GenerateOperation(ns, name, method.Key.ToUpperInvariant(), path.Key, operation);
                        await File.WriteAllTextAsync(Path.Combine(output, name + ".Generated.cs"), code);
                    }
                }
            }
            Console.WriteLine($"Generated {usedNames.Count} operations in {output}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static JsonObject ReadDocument(string path)
    {
        var text = File.ReadAllText(path);
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("Empty document.");
        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
        var json = JsonSerializer.Serialize(ConvertYaml(yaml.Documents[0].RootNode));
        return JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Empty document.");
    }

    static object? ConvertYaml(YamlNode node) => node switch
    {
        YamlMappingNode map => map.Children.ToDictionary(pair => ((YamlScalarNode)pair.Key).Value!, pair => ConvertYaml(pair.Value)),
        YamlSequenceNode sequence => sequence.Children.Select(ConvertYaml).ToArray(),
        YamlScalarNode scalar when scalar.Value is "true" or "false" => bool.Parse(scalar.Value),
        YamlScalarNode scalar when long.TryParse(scalar.Value, out var integer) => integer,
        YamlScalarNode scalar => scalar.Value,
        _ => null
    };

    static JsonNode? ConvertSchema(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"]?.GetValue<string>() is string reference && reference.StartsWith("#/components/schemas/"))
                obj["$ref"] = "#/definitions/" + reference["#/components/schemas/".Length..];
            if (obj["nullable"]?.GetValue<bool>() == true && obj["type"]?.GetValue<string>() is string type)
                obj["type"] = new JsonArray(JsonValue.Create(type), JsonValue.Create("null"));
            obj.Remove("nullable");
            foreach (var key in obj.Select(item => item.Key).ToArray())
                if (obj[key] is JsonObject or JsonArray) obj[key] = ConvertSchema(obj[key]?.DeepClone());
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
                if (array[index] is JsonObject or JsonArray) array[index] = ConvertSchema(array[index]?.DeepClone());
        }
        return node;
    }

    sealed record ResponseSpec(int? Status, string? TypeName);

    static JsonNode? FindJsonSchema(JsonNode? content)
    {
        if (content is not JsonObject mediaTypes) return null;
        foreach (var mediaType in mediaTypes)
            if (mediaType.Key.Contains("json", StringComparison.OrdinalIgnoreCase))
                return mediaType.Value?["schema"];
        return null;
    }

    static string? ReferencedType(JsonNode? schema)
    {
        if (schema is null) return null;
        if (schema["$ref"]?.GetValue<string>() is string reference && reference.StartsWith("#/components/schemas/"))
            return ModelIdentifier(reference.Split('/').Last());
        throw new InvalidDataException("Inline operation schemas are not supported; use a component schema reference.");
    }

    static string GenerateOperation(string ns, string name, string method, string path, JsonObject operation)
    {
        var responses = new List<ResponseSpec>();
        if (operation["responses"] is JsonObject responseMap)
        {
            foreach (var entry in responseMap)
            {
                int? status = entry.Key.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : int.TryParse(entry.Key, out var parsed) ? parsed : throw new InvalidDataException($"{name}: response status '{entry.Key}' is not supported.");
                responses.Add(new ResponseSpec(status, ReferencedType(FindJsonSchema(entry.Value?["content"]))));
            }
        }
        var bodySchema = FindJsonSchema(operation["requestBody"]?["content"]);
        var bodyType = ReferencedType(bodySchema);
        var bodyContentType = operation["requestBody"]?["content"] is JsonObject bodyContent
            ? bodyContent.Select(item => item.Key).FirstOrDefault(media => media.Contains("json", StringComparison.OrdinalIgnoreCase)) ?? "application/json"
            : "application/json";
        var pathNames = System.Text.RegularExpressions.Regex.Matches(path, "\\{([^}]+)\\}")
            .Select(match => match.Groups[1].Value).ToArray();
        var queryNames = new List<string>();
        if (operation["parameters"] is JsonArray parameters)
        {
            foreach (var parameter in parameters)
                if (parameter?["in"]?.GetValue<string>() == "query" && parameter["name"]?.GetValue<string>() is string query)
                    queryNames.Add(query);
        }
        var parameterIdentifiers = pathNames.Concat(queryNames).Select(PropertyIdentifier).ToArray();
        if (parameterIdentifiers.Distinct(StringComparer.Ordinal).Count() != parameterIdentifiers.Length)
            throw new InvalidDataException($"{name}: parameters collide after C# name normalization.");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated> Bonsai.Http.OpenApi; models generated by Bonsai.Sgen 0.9.0 </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Reactive.Linq;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using Newtonsoft.Json;");
        sb.AppendLine("using Bonsai.Http;");
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine($"public sealed class {name}Request {{");
        foreach (var parameter in pathNames.Concat(queryNames).Distinct())
            sb.AppendLine($"    public string? {PropertyIdentifier(parameter)} {{ get; set; }}");
        if (bodyType is not null) sb.AppendLine($"    public {bodyType}? Body {{ get; set; }}");
        sb.AppendLine("}");
        sb.AppendLine($"public abstract record {name}Result(HttpResponse Response);");
        foreach (var response in responses)
        {
            var variant = $"{name}Status{response.Status?.ToString() ?? "Default"}";
            sb.AppendLine(response.TypeName is null
                ? $"public sealed record {variant}(HttpResponse Response) : {name}Result(Response);"
                : $"public sealed record {variant}(HttpResponse Response, {response.TypeName} Value) : {name}Result(Response);");
        }
        sb.AppendLine($"public sealed record {name}Unknown(HttpResponse Response) : {name}Result(Response);");
        sb.AppendLine($"[Bonsai.Combinator] public sealed class Ensure{name}SuccessStatusCode : Bonsai.Combinator<{name}Result, {name}Result> {{");
        sb.AppendLine($"    public override IObservable<{name}Result> Process(IObservable<{name}Result> source) => source.Select(result => result.Response.IsSuccessStatusCode ? result : throw new HttpStatusException(result.Response));");
        sb.AppendLine("}");
        sb.AppendLine("[Bonsai.Combinator]");
        sb.AppendLine($"public sealed class {name} {{");
        sb.AppendLine($"    public IObservable<{name}Result> Process(IObservable<{name}Request> source, IObservable<HttpClientResource> clients) {{");
        sb.AppendLine("        var requests = source.Select(input => {");
        sb.AppendLine($"            var path = {JsonSerializer.Serialize(path)};");
        foreach (var parameter in pathNames)
            sb.AppendLine($"            path = path.Replace({JsonSerializer.Serialize("{" + parameter + "}")}, Uri.EscapeDataString(input.{PropertyIdentifier(parameter)} ?? throw new ArgumentException({JsonSerializer.Serialize("Missing path parameter: " + parameter)})));");
        if (queryNames.Count > 0)
        {
            sb.AppendLine("            var query = new System.Collections.Generic.List<string>();");
            foreach (var parameter in queryNames)
                sb.AppendLine($"            if (input.{PropertyIdentifier(parameter)} is {{ }} value_{PropertyIdentifier(parameter)}) query.Add({JsonSerializer.Serialize(Uri.EscapeDataString(parameter) + "=")} + Uri.EscapeDataString(value_{PropertyIdentifier(parameter)}));");
            sb.AppendLine("            if (query.Count > 0) path += \"?\" + string.Join(\"&\", query);");
        }
        if (bodyType is null) sb.AppendLine($"            return new HttpRequest({JsonSerializer.Serialize(method)}, path);");
        else sb.AppendLine($"            return new HttpRequest({JsonSerializer.Serialize(method)}, path, body: input.Body is null ? null : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input.Body)), contentType: {JsonSerializer.Serialize(bodyContentType)});");
        sb.AppendLine("        });");
        sb.AppendLine($"        return new SendRequest().Process(requests, clients).Select(response => {{");
        sb.AppendLine("            try {");
        sb.AppendLine("                var isJson = response.Headers.TryGetValue(\"Content-Type\", out var types) && string.Join(\",\", types).Contains(\"json\", StringComparison.OrdinalIgnoreCase);");
        sb.AppendLine("                switch (response.StatusCode) {");
        foreach (var response in responses)
        {
            var variant = $"{name}Status{response.Status?.ToString() ?? "Default"}";
            sb.AppendLine(response.Status is null ? "                    default:" : $"                    case {response.Status}:");
            if (response.TypeName is null) sb.AppendLine($"                        return ({name}Result)new {variant}(response);");
            else sb.AppendLine($"                        return isJson && response.Body.Length > 0 ? ({name}Result)new {variant}(response, JsonConvert.DeserializeObject<{response.TypeName}>(Encoding.UTF8.GetString(response.Body)) ?? throw new JsonSerializationException(\"Empty response body\")) : new {name}Unknown(response);");
        }
        if (!responses.Any(response => response.Status is null))
            sb.AppendLine($"                    default: return new {name}Unknown(response);");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch (Exception error) { throw new HttpResponseParseException(response, error); }");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static async Task RunSgen(string schemaPath, string output, string ns)
    {
        var toolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");
        var restore = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = toolsDirectory };
        restore.ArgumentList.Add("tool");
        restore.ArgumentList.Add("restore");
        using (var restoreProcess = Process.Start(restore) ?? throw new InvalidOperationException("Could not restore Sgen."))
        {
            await restoreProcess.WaitForExitAsync();
            if (restoreProcess.ExitCode != 0) throw new InvalidOperationException("Could not restore the published Bonsai.Sgen tool.");
        }
        var process = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = toolsDirectory };
        foreach (var value in new[] { "tool", "run", "bonsai.sgen", "--", schemaPath,
                     "--namespace", ns, "--output", output, "--name", "OpenApi.Models.Generated.cs", "--serializer", "json" })
            process.ArgumentList.Add(value);
        using var child = Process.Start(process) ?? throw new InvalidOperationException("Could not start Sgen.");
        await child.WaitForExitAsync();
        if (child.ExitCode != 0) throw new InvalidOperationException("Sgen failed. Restore the pinned Bonsai.Sgen tool with 'dotnet tool restore'.");
    }

    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    static string ModelIdentifier(string value)
    {
        var pascal = PascalCaseNamingConvention.Instance.Apply(value);
        var prefix = pascal.StartsWith('_') ? "_" : string.Empty;
        return Identifier(prefix + pascal.Replace("_", string.Empty));
    }

    static string PropertyIdentifier(string value) => ModelIdentifier(value);

    static bool IsNamespace(string value) => value.Split('.').All(segment =>
        segment.Length > 0 && (char.IsLetter(segment[0]) || segment[0] == '_') &&
        segment.All(ch => char.IsLetterOrDigit(ch) || ch == '_') && !Keywords.Contains(segment));
    static string Identifier(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value) builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        if (builder.Length == 0 || char.IsDigit(builder[0])) builder.Insert(0, '_');
        if (Keywords.Contains(builder.ToString())) builder.Append('_');
        return builder.ToString();
    }
}
