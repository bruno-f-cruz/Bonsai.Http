using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;

namespace Bonsai.Http.OpenApi;

internal static partial class Program
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
            PrepareDocument(document);
            var schema = new JsonObject { ["title"] = "ApiModels", ["type"] = "object" };
            var definitions = new JsonObject();
            if (document["components"]?["schemas"] is JsonObject components)
            {
                foreach (var component in components)
                    definitions[component.Key] = ConvertSchema(component.Value?.DeepClone());
            }
            schema["definitions"] = definitions;
            ValidateSchemaReferences(definitions, definitions);
            var generated = new Dictionary<string, string>(StringComparer.Ordinal);
            var originalModels = definitions.Select(pair => ModelIdentifier(pair.Key)).ToArray();
            if (originalModels.Distinct(StringComparer.Ordinal).Count() != originalModels.Length)
                throw new InvalidDataException("Component schema names collide after C# normalization.");
            var usedNames = new HashSet<string>(originalModels, StringComparer.Ordinal);
            usedNames.Add("ApiModels");
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
                        foreach (var helper in new[] { name + "Request", name + "Result", name + "Unknown", "Create" + name + "Request", "Ensure" + name + "SuccessStatusCode" }
                            .Concat(operation["responses"]!.AsObject().Select(response => name + "Status" + (response.Key == "default" ? "Default" : response.Key))))
                            if (!usedNames.Add(helper)) throw new InvalidDataException($"Generated type name collision: {helper}");
                        var code = GenerateOperation(ns, name, method.Key.ToUpperInvariant(), path.Key, operation, definitions);
                        generated.Add(name + ".Generated.cs", code);
                    }
                }
            }
            foreach (var model in definitions.Select(pair => ModelIdentifier(pair.Key)).Except(originalModels))
                if (!usedNames.Add(model)) throw new InvalidDataException($"Generated inline model name collision: {model}");
            var staging = Path.Combine(Path.GetTempPath(), "bonsai-http-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                var schemaPath = Path.Combine(staging, "OpenApi.Models.schema.json");
                await File.WriteAllTextAsync(schemaPath, schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                if (definitions.Count > 0) await RunSgen(schemaPath, staging, ns);
                foreach (var file in generated) await File.WriteAllTextAsync(Path.Combine(staging, file.Key), file.Value);
                Directory.CreateDirectory(output);
                var manifest = Path.Combine(output, ".bonsai-http-openapi.files.json");
                var names = Directory.GetFiles(staging).Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (File.Exists(manifest))
                {
                    foreach (var old in JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(manifest)) ?? Array.Empty<string>())
                        if (old == Path.GetFileName(old) && !names.Contains(old) && old.EndsWith(".Generated.cs", StringComparison.Ordinal))
                            File.Delete(Path.Combine(output, old));
                }
                foreach (var file in Directory.GetFiles(staging)) File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
                await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(names));
            }
            finally { Directory.Delete(staging, true); }
            Console.WriteLine($"Generated {generated.Count} operations in {output}.");
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
        YamlScalarNode scalar when scalar.Style == YamlDotNet.Core.ScalarStyle.SingleQuoted || scalar.Style == YamlDotNet.Core.ScalarStyle.DoubleQuoted => scalar.Value,
        YamlScalarNode scalar when scalar.Value is null or "null" or "~" => null,
        YamlScalarNode scalar when bool.TryParse(scalar.Value, out var boolean) => boolean,
        YamlScalarNode scalar when long.TryParse(scalar.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer) => integer,
        YamlScalarNode scalar when double.TryParse(scalar.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) => number,
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

    static string GenerateOperation(string ns, string name, string method, string path, JsonObject operation, JsonObject definitions)
    {
        var responses = new List<ResponseSpec>();
        if (operation["responses"] is JsonObject responseMap)
        {
            foreach (var entry in responseMap)
            {
                int? status = entry.Key.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : int.TryParse(entry.Key, out var parsed) ? parsed : throw new InvalidDataException($"{name}: response status '{entry.Key}' is not supported.");
                responses.Add(new ResponseSpec(status, ReferencedType(FindJsonSchema(entry.Value?["content"]), definitions, name + "Response" + (status?.ToString() ?? "Default"))));
            }
        }
        var bodySchema = FindJsonSchema(operation["requestBody"]?["content"]);
        var bodyType = ReferencedType(bodySchema, definitions, name + "Body");
        var bodyContentType = operation["requestBody"]?["content"] is JsonObject bodyContent
            ? bodyContent.Select(item => item.Key).FirstOrDefault(media => media.Contains("json", StringComparison.OrdinalIgnoreCase)) ?? "application/json"
            : "application/json";
        var parameters = ((JsonArray)operation["parameters"]!).Select(item => item!.AsObject()).ToArray();
        var pathNames = System.Text.RegularExpressions.Regex.Matches(path, "\\{([^}]+)\\}").Select(match => match.Groups[1].Value).ToArray();
        foreach (var pathName in pathNames)
            if (!parameters.Any(parameter => parameter["in"]!.GetValue<string>() == "path" && parameter["name"]!.GetValue<string>() == pathName))
                throw new InvalidDataException($"{name}: path parameter '{pathName}' is not declared.");
        var parameterIdentifiers = parameters.Select(parameter => PropertyIdentifier(parameter["name"]!.GetValue<string>())).ToArray();
        if (parameterIdentifiers.Distinct(StringComparer.Ordinal).Count() != parameterIdentifiers.Length)
            throw new InvalidDataException($"{name}: parameters collide after C# name normalization.");
        if (bodyType is not null && parameterIdentifiers.Contains("Body")) throw new InvalidDataException($"{name}: parameter conflicts with Body property.");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated> Bonsai.Http.OpenApi; models generated by Bonsai.Sgen 0.9.0 </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Reactive.Linq;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using Newtonsoft.Json;");
        sb.AppendLine("using Bonsai.Http;");
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine($"[System.ComponentModel.TypeConverter(typeof(System.ComponentModel.ExpandableObjectConverter))] public sealed class {name}Request {{");
        foreach (var parameter in parameters)
        {
            var parameterType = ReferencedType(parameter["schema"], definitions, name + PropertyIdentifier(parameter["name"]!.GetValue<string>()));
            var defaultValue = parameter["schema"]?["default"];
            var initializer = defaultValue is null ? "" : " = " + defaultValue.ToJsonString() + ";";
            sb.AppendLine($"    public {parameterType}? {PropertyIdentifier(parameter["name"]!.GetValue<string>())} {{ get; set; }}{initializer}");
        }
        if (bodyType is not null) sb.AppendLine($"    public {bodyType}? Body {{ get; set; }}");
        sb.AppendLine("}");
        sb.AppendLine($"[System.ComponentModel.TypeConverter(typeof(System.ComponentModel.ExpandableObjectConverter))] public sealed class Create{name}Request : Bonsai.Source<{name}Request> {{");
        sb.AppendLine($"    public {name}Request Value {{ get; set; }} = new {name}Request();");
        sb.AppendLine($"    public override IObservable<{name}Request> Generate() => Observable.Defer(() => Observable.Return(JsonConvert.DeserializeObject<{name}Request>(JsonConvert.SerializeObject(Value))!));");
        sb.AppendLine("}");
        sb.AppendLine($"public abstract class {name}Result : IHttpResponseResult {{ protected {name}Result(HttpResponse response) {{ Response = response; }} public HttpResponse Response {{ get; }} }}");
        foreach (var response in responses)
        {
            var variant = $"{name}Status{response.Status?.ToString() ?? "Default"}";
            sb.AppendLine(response.TypeName is null
                ? $"public sealed class {variant} : {name}Result {{ public {variant}(HttpResponse response) : base(response) {{ }} }}"
                : $"public sealed class {variant} : {name}Result {{ public {variant}(HttpResponse response, {response.TypeName} value) : base(response) {{ Value = value; }} public {response.TypeName} Value {{ get; }} }}");
        }
        sb.AppendLine($"public sealed class {name}Unknown : {name}Result {{ public {name}Unknown(HttpResponse response) : base(response) {{ }} }}");
        sb.AppendLine($"[Bonsai.Combinator] public sealed class Ensure{name}SuccessStatusCode : Bonsai.Combinator<{name}Result, {name}Result> {{");
        sb.AppendLine($"    public override IObservable<{name}Result> Process(IObservable<{name}Result> source) => source.Select(result => result.Response.IsSuccessStatusCode ? result : throw new HttpStatusException(result.Response));");
        sb.AppendLine("}");
        sb.AppendLine("[Bonsai.Combinator]");
        sb.AppendLine($"public sealed class {name} {{");
        sb.AppendLine("    static string Format(object value) { var json = JsonConvert.SerializeObject(value); return json.StartsWith(\"\\\"\", StringComparison.Ordinal) ? JsonConvert.DeserializeObject<string>(json)! : json; }");
        sb.AppendLine("    static T Decode<T>(HttpResponse response) { var value = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(response.Body)); if (value is null) throw new JsonSerializationException(\"Empty response body\"); return value; }");
        sb.AppendLine($"    public IObservable<{name}Result> Process(IObservable<{name}Request> source, IObservable<HttpClientResource> clients) {{");
        sb.AppendLine("        var requests = source.Select(input => {");
        sb.AppendLine($"            var path = {JsonSerializer.Serialize(path)};");
        sb.AppendLine("            var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("            var query = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();");
        foreach (var parameter in parameters)
        {
            var rawName = parameter["name"]!.GetValue<string>();
            var property = PropertyIdentifier(rawName);
            var place = parameter["in"]!.GetValue<string>();
            var required = parameter["required"]?.GetValue<bool>() == true;
            if (place == "path" && !pathNames.Contains(rawName)) throw new InvalidDataException($"{name}: declared path parameter '{rawName}' is absent from path template.");
            if (required) sb.AppendLine($"            if (input.{property} is null) throw new ArgumentException({JsonSerializer.Serialize("Missing required parameter: " + rawName)});");
            sb.AppendLine($"            if (input.{property} is {{ }} value_{property}) {{");
            if (place == "path") sb.AppendLine($"                path = path.Replace({JsonSerializer.Serialize("{" + rawName + "}")}, Uri.EscapeDataString(Format(value_{property})));");
            else if (place == "header") sb.AppendLine($"                headers[{JsonSerializer.Serialize(rawName)}] = Format(value_{property});");
            else sb.AppendLine($"                query.Add(new System.Collections.Generic.KeyValuePair<string, string>({JsonSerializer.Serialize(rawName)}, Format(value_{property})));");
            sb.AppendLine("            }");
        }
        if (bodyType is not null && operation["requestBody"]?["required"]?.GetValue<bool>() == true)
            sb.AppendLine("            if (input.Body is null) throw new ArgumentException(\"A request body is required.\");");
        if (bodyType is null) sb.AppendLine($"            return new HttpRequest({JsonSerializer.Serialize(method)}, path, headers).WithQuery(query);");
        else sb.AppendLine($"            return new HttpRequest({JsonSerializer.Serialize(method)}, path, headers, body: input.Body is null ? null : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input.Body)), contentType: {JsonSerializer.Serialize(bodyContentType)}).WithQuery(query);");
        sb.AppendLine("        });");
        sb.AppendLine($"        return new SendRequest().Process(requests, clients).Select(response => {{");
        sb.AppendLine("            try {");
        sb.AppendLine("                var isJson = response.Headers.TryGetValue(\"Content-Type\", out var types) && string.Join(\",\", types).IndexOf(\"json\", StringComparison.OrdinalIgnoreCase) >= 0;");
        sb.AppendLine("                switch (response.StatusCode) {");
        foreach (var response in responses)
        {
            var variant = $"{name}Status{response.Status?.ToString() ?? "Default"}";
            sb.AppendLine(response.Status is null ? "                    default:" : $"                    case {response.Status}:");
            if (response.TypeName is null) sb.AppendLine($"                        return ({name}Result)new {variant}(response);");
            else sb.AppendLine($"                        return isJson ? ({name}Result)new {variant}(response, Decode<{response.TypeName}>(response)) : new {name}Unknown(response);");
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
        // dotnet's local-tool restore can race when multi-target builds invoke us concurrently.
        // Serialize the shared package/resolver setup and tool run, while keeping output directories independent.
        using var toolLock = await AcquireSgenLockAsync();
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

    static async Task<FileStream> AcquireSgenLockAsync()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bonsai.Http.OpenApi");
        Directory.CreateDirectory(cache);
        var path = Path.Combine(cache, "sgen-0.9.0.lock");
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromMinutes(2)) { await Task.Delay(100); }
        }
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
