namespace ObjectIR.Core.Serialization;

using ObjectIR.Core.IR;
using ObjectIR.Core.Builder;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System;
using System.IO;
using Module = IR.Module;

/// <summary>
/// Loads ObjectIR modules from text format and provides storage/caching capabilities
/// </summary>
public sealed class ModuleLoader
{
    private readonly Dictionary<string, Module> _moduleCache = new();

    /// <summary>
    /// Loads a module from text format (as shown in documentation)
    /// </summary>
    /// <example>
    /// var textFormat = @"
    /// module CalculatorApp
    /// class Calculator {
    ///     field history: List&lt;int32&gt;
    ///     method Add(a: int32, b: int32) -&gt; int32 {
    ///         ldarg a
    ///         ldarg b
    ///         add
    ///         ret
    ///     }
    /// }
    /// ";
    /// var loader = new ModuleLoader();
    /// var module = loader.LoadFromText(textFormat);
    /// </example>
    public Module LoadFromText(string text)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("//"))
            .ToList();

        if (lines.Count == 0)
            throw new ArgumentException("Empty module text");

        int lineIndex = 0;

        // Parse module declaration
        if (!lines[lineIndex].StartsWith("module "))
            throw new FormatException($"Expected 'module' keyword at line {lineIndex}");

        string moduleName = lines[lineIndex].Substring("module ".Length).Trim();
        lineIndex++;

        var builder = new IRBuilder(moduleName);

        // Parse types and functions (allow leading modifiers before keywords)
        var typeClassPattern = new Regex("^((?:public|private|protected|internal|static|abstract|sealed)\\s+)*class\\b", RegexOptions.IgnoreCase);
        var typeInterfacePattern = new Regex("^((?:public|private|protected|internal)\\s+)*interface\\b", RegexOptions.IgnoreCase);
        var typeStructPattern = new Regex("^((?:public|private|protected|internal)\\s+)*struct\\b", RegexOptions.IgnoreCase);
        var typeEnumPattern = new Regex("^((?:public|private|protected|internal)\\s+)*enum\\b", RegexOptions.IgnoreCase);

        while (lineIndex < lines.Count)
        {
            string line = lines[lineIndex];

            if (typeClassPattern.IsMatch(line))
            {
                lineIndex = ParseClass(builder, lines, lineIndex);
            }
            else if (typeInterfacePattern.IsMatch(line))
            {
                lineIndex = ParseInterface(builder, lines, lineIndex);
            }
            else if (typeStructPattern.IsMatch(line))
            {
                lineIndex = ParseStruct(builder, lines, lineIndex);
            }
            else if (typeEnumPattern.IsMatch(line))
            {
                lineIndex = ParseEnum(builder, lines, lineIndex);
            }
            else
            {
                lineIndex++;
            }
        }

        var module = builder.Build();
        CacheModule(module);
        return module;
    }

    /// <summary>
    /// Loads a module from JSON format
    /// </summary>
    public Module LoadFromJson(string json)
    {
        var module = ModuleSerializer.LoadFromJson(json);
        CacheModule(module);
        return module;
    }

    /// <summary>
    /// Loads a module from a JSON file
    /// </summary>
    public Module LoadFromJsonFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Loads a module from text file format
    /// </summary>
    public Module LoadFromTextFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return LoadFromText(text);
    }

    /// <summary>
    /// Saves a module to JSON file
    /// </summary>
    public void SaveToJsonFile(Module module, string filePath, bool indented = true)
    {
        var json = module.DumpJson(indented);
        File.WriteAllText(filePath, json);
        CacheModule(module);
    }

    /// <summary>
    /// Saves a module to text file format
    /// </summary>
    public void SaveToTextFile(Module module, string filePath)
    {
        var text = module.DumpText();
        File.WriteAllText(filePath, text);
        CacheModule(module);
    }

    /// <summary>
    /// Loads a module from BSON (binary JSON) format
    /// </summary>
    public Module LoadFromBson(byte[] bsonData)
    {
        var module = ModuleSerializer.LoadFromBson(bsonData);
        CacheModule(module);
        return module;
    }

    /// <summary>
    /// Loads a module from a BSON file
    /// </summary>
    public Module LoadFromBsonFile(string filePath)
    {
        var bsonData = File.ReadAllBytes(filePath);
        return LoadFromBson(bsonData);
    }

    /// <summary>
    /// Saves a module to BSON file for smaller file size
    /// </summary>
    public void SaveToBsonFile(Module module, string filePath)
    {
        var bsonData = module.DumpBson();
        File.WriteAllBytes(filePath, bsonData);
        CacheModule(module);
    }

    /// <summary>
    /// Gets a cached module by name
    /// </summary>
    public Module? GetCachedModule(string moduleName)
    {
        return _moduleCache.TryGetValue(moduleName, out var module) ? module : null;
    }

    /// <summary>
    /// Clears the module cache
    /// </summary>
    public void ClearCache()
    {
        _moduleCache.Clear();
    }

    /// <summary>
    /// Gets all cached modules
    /// </summary>
    public IReadOnlyDictionary<string, Module> GetAllCachedModules()
    {
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, Module>(_moduleCache);
    }

    /// <summary>
    /// Loads multiple module files from a directory
    /// </summary>
    public Dictionary<string, Module> LoadModulesFromDirectory(string directoryPath, string pattern = "*.ir.txt")
    {
        var modules = new Dictionary<string, Module>();
        var files = Directory.GetFiles(directoryPath, pattern);

        foreach (var file in files)
        {
            try
            {
                // Detect format based on file extension
                Module module;
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    module = LoadFromJsonFile(file);
                }
                else
                {
                    module = LoadFromTextFile(file);
                }
                modules[module.Name] = module;
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load module from {file}: {ex.Message}", ex);
            }
        }

        return modules;
    }

    /// <summary>
    /// Saves multiple modules to a directory
    /// </summary>
    public void SaveModulesToDirectory(IEnumerable<Module> modules, string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        foreach (var module in modules)
        {
            var filename = Path.Combine(directoryPath, $"{module.Name}.ir.txt");
            SaveToTextFile(module, filename);
        }
    }

    // ============================================================================
    // Private Parsing Methods
    // ============================================================================

    private void CacheModule(Module module)
    {
        _moduleCache[module.Name] = module;
    }

    private int ParseClass(IRBuilder builder, List<string> lines, int startIndex)
    {
        // Extract class name and base type
        var classLine = lines[startIndex];
        var classMatch = Regex.Match(classLine, @"^((?:(?:public|private|protected|internal|static|abstract|sealed)\s+)*)class\s+(\w+)\s*(?:\:\s*(\S+))?\s*\{?");

        if (!classMatch.Success)
            throw new FormatException($"Invalid class declaration at line {startIndex}");

        var modifiersStr = classMatch.Groups[1].Value.Trim();
        string className = classMatch.Groups[2].Value;
        string? baseType = classMatch.Groups[3].Success ? classMatch.Groups[3].Value : null;

        var classBuilder = builder.Class(className);

        var modTokens = GetModifierTokens(modifiersStr);
        classBuilder.Access(ToAccessModifier(modTokens));
        if (modTokens.Contains("abstract")) classBuilder.Abstract();
        if (modTokens.Contains("sealed")) classBuilder.Sealed();
        int endIndex = FindBlockEnd(lines, startIndex);

        int i = startIndex + 1;
        var fieldPattern = new Regex("^((?:public|private|protected|internal|static|readonly)\\s+)*field\\b", RegexOptions.IgnoreCase);
        var methodPattern = new Regex("^((?:public|private|protected|internal|static|abstract|virtual|override)\\s+)*method\\b", RegexOptions.IgnoreCase);
        var propPattern = new Regex("^((?:public|private|protected|internal|static)\\s+)*property\\b", RegexOptions.IgnoreCase);

        while (i < endIndex)
        {
            var line = lines[i];
            if (fieldPattern.IsMatch(line))
            {
                i = ParseField(classBuilder, lines, i);
            }
            else if (methodPattern.IsMatch(line))
            {
                i = ParseMethod(classBuilder, lines, i);
            }
            else if (propPattern.IsMatch(line))
            {
                i = ParseProperty(classBuilder, lines, i);
            }
            else
            {
                i++;
            }
        }

        classBuilder.EndClass();
        return endIndex + 1;
    }

    private int ParseInterface(IRBuilder builder, List<string> lines, int startIndex)
    {
        var interfaceLine = lines[startIndex];
        var interfaceMatch = Regex.Match(interfaceLine, @"^((?:(?:public|private|protected|internal)\s+)*)interface\s+(\w+)\s*\{?");

        if (!interfaceMatch.Success)
            throw new FormatException($"Invalid interface declaration at line {startIndex}");

        var modifiersStr = interfaceMatch.Groups[1].Value.Trim();
        string interfaceName = interfaceMatch.Groups[2].Value;
        var interfaceBuilder = builder.Interface(interfaceName);
        interfaceBuilder.Access(ToAccessModifier(GetModifierTokens(modifiersStr)));
        int endIndex = FindBlockEnd(lines, startIndex);
        // Interfaces currently not parsing members in detail; skip inner block
        interfaceBuilder.EndInterface();
        return endIndex + 1;
    }

    private int ParseStruct(IRBuilder builder, List<string> lines, int startIndex)
    {
        var structLine = lines[startIndex];
        var structMatch = Regex.Match(structLine, @"^((?:(?:public|private|protected|internal)\s+)*)struct\s+(\w+)\s*\{?");

        if (!structMatch.Success)
            throw new FormatException($"Invalid struct declaration at line {startIndex}");

        var modifiersStr = structMatch.Groups[1].Value.Trim();
        string structName = structMatch.Groups[2].Value;
        var structBuilder = builder.Struct(structName);
        structBuilder.Access(ToAccessModifier(GetModifierTokens(modifiersStr)));
        int endIndex = FindBlockEnd(lines, startIndex);

        int i = startIndex + 1;
        var fieldPattern = new Regex("^((?:public|private|protected|internal|static|readonly)\\s+)*field\\b", RegexOptions.IgnoreCase);
        while (i < endIndex)
        {
            var line = lines[i];
            if (fieldPattern.IsMatch(line))
            {
                i = ParseField(structBuilder, lines, i);
            }
            else
            {
                i++;
            }
        }

        structBuilder.EndStruct();
        return endIndex + 1;
    }

    private int ParseEnum(IRBuilder builder, List<string> lines, int startIndex)
    {
        var enumLine = lines[startIndex];
        var enumMatch = Regex.Match(enumLine, @"^((?:(?:public|private|protected|internal)\s+)*)enum\s+(\w+)\s*(?:\:\s*(\S+))?\s*\{?");

        if (!enumMatch.Success)
            throw new FormatException($"Invalid enum declaration at line {startIndex}");

        var modifiersStr = enumMatch.Groups[1].Value.Trim();
        string enumName = enumMatch.Groups[2].Value;
        int lineIndex = startIndex + 1;

        // Try to register enum with access modifier if possible (builder has no enum API)
        try
        {
            var moduleField = typeof(IRBuilder).GetField("_module", BindingFlags.NonPublic | BindingFlags.Instance);
            if (moduleField != null)
            {
                var module = moduleField.GetValue(builder) as Module;
                if (module != null)
                {
                    var enumDef = new EnumDefinition(enumName);
                    enumDef.Access = ToAccessModifier(GetModifierTokens(modifiersStr));
                    module.Types.Add(enumDef);
                }
            }
        }
        catch { }

        int endIndex = FindBlockEnd(lines, startIndex);
        return endIndex + 1;
    }

    private int ParseField(dynamic classBuilder, List<string> lines, int startIndex)
    {
        // [modifiers] field name: type
        var fieldLine = lines[startIndex];
        var fieldMatch = Regex.Match(fieldLine, @"^((?:(?:public|private|protected|internal|static|readonly)\s+)*)field\s+(\w+)\s*:\s*(.+)$");

        if (!fieldMatch.Success)
            throw new FormatException($"Invalid field declaration at line {startIndex}");

        var modifiersStr = fieldMatch.Groups[1].Value.Trim();
        string fieldName = fieldMatch.Groups[2].Value;
        string fieldType = fieldMatch.Groups[3].Value.Trim();

        var typeRef = ParseTypeReference(fieldType);
        // Call Field and try to apply modifiers via returned builder when available
        var result = classBuilder.Field(fieldName, typeRef);
        var mods = GetModifierTokens(modifiersStr);

        if (result != null && result.GetType().GetMethod("Access") != null)
        {
            dynamic fb = result;
            fb.Access(ToAccessModifier(mods));
            if (mods.Contains("static")) fb.Static();
            if (mods.Contains("readonly")) fb.ReadOnly();
        }
        else
        {
            // Fallback: try to set last-defined field on underlying type via reflection
            try
            {
                var builderType = classBuilder.GetType();
                FieldInfo backingField = builderType.GetField("_class", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? builderType.GetField("_struct", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    var typeDef = backingField.GetValue(classBuilder);
                    var fieldsProp = typeDef.GetType().GetProperty("Fields");
                    var fields = fieldsProp.GetValue(typeDef) as System.Collections.IList;
                    if (fields != null && fields.Count > 0)
                    {
                        var lastField = fields[fields.Count - 1] as FieldDefinition;
                        if (lastField != null)
                        {
                            lastField.Access = ToAccessModifier(mods);
                            lastField.IsStatic = mods.Contains("static");
                            lastField.IsReadOnly = mods.Contains("readonly");
                        }
                    }
                }
            }
            catch { }
        }

        return startIndex + 1;
    }

    private int ParseProperty(dynamic classBuilder, List<string> lines, int startIndex)
    {
        var propLine = lines[startIndex];
        var propMatch = Regex.Match(propLine, @"^((?:(?:public|private|protected|internal|static)\s+)*)property\s+(\w+)\s*:\s*(.+)$");

        if (!propMatch.Success)
            throw new FormatException($"Invalid property declaration at line {startIndex}");

        var modifiersStr = propMatch.Groups[1].Value.Trim();
        string propName = propMatch.Groups[2].Value;
        string propType = propMatch.Groups[3].Value.Trim();

        var typeRef = ParseTypeReference(propType);
        // Try to invoke Property; if builder returns an accessor builder, attempt to apply access
        try
        {
            var result = classBuilder.Property(propName, typeRef);
            var mods = GetModifierTokens(modifiersStr);
            if (result != null && result.GetType().GetMethod("Access") != null)
            {
                dynamic pb = result;
                pb.Access(ToAccessModifier(mods));
                if (mods.Contains("static") && result.GetType().GetMethod("Static") != null) pb.Static();
            }
            else
            {
                // fallback: set last defined property's access via reflection
                var builderType = classBuilder.GetType();
                FieldInfo backingField = builderType.GetField("_class", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? builderType.GetField("_interface", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    var typeDef = backingField.GetValue(classBuilder);
                    var propsProp = typeDef.GetType().GetProperty("Properties");
                    var props = propsProp.GetValue(typeDef) as System.Collections.IList;
                    if (props != null && props.Count > 0)
                    {
                        var lastProp = props[props.Count - 1] as PropertyDefinition;
                        if (lastProp != null)
                        {
                            lastProp.Access = ToAccessModifier(GetModifierTokens(modifiersStr));
                        }
                    }
                }
            }
        }
        catch { }

        return startIndex + 1;
    }

    private int ParseMethod(dynamic classBuilder, List<string> lines, int startIndex)
    {
        // [modifiers] method Name(param: type, ...) -> ReturnType { ... }
        var methodLine = lines[startIndex];
        var methodMatch = Regex.Match(methodLine,
            @"^((?:(?:public|private|protected|internal|static|abstract|virtual|override)\s+)*)method\s+(\w+)\s*\((.*?)\)\s*(?:->\s*([^\{]+?))?\s*\{?");

        if (!methodMatch.Success)
            throw new FormatException($"Invalid method declaration at line {startIndex}");

        var modifiersStr = methodMatch.Groups[1].Value.Trim();
        string methodName = methodMatch.Groups[2].Value;
        string paramsStr = methodMatch.Groups[3].Value;
        // determine return type by searching for '->' in the method line (robust against regex quirks)
        string returnTypeStr = "void";
        var arrowIdx = methodLine.IndexOf("->");
        if (arrowIdx >= 0)
        {
            var after = methodLine.Substring(arrowIdx + 2);
            var braceIdx = after.IndexOf('{');
            var rawType = braceIdx >= 0 ? after.Substring(0, braceIdx) : after;
            returnTypeStr = rawType.Trim();
        }

        var modTokens = GetModifierTokens(modifiersStr);

        var methodBuilder = classBuilder.Method(methodName, ParseTypeReference(returnTypeStr));
        // Apply modifiers to method builder when available
        try
        {
            methodBuilder.Access(ToAccessModifier(modTokens));
            if (modTokens.Contains("static")) methodBuilder.Static();
            if (modTokens.Contains("virtual")) methodBuilder.Virtual();
            if (modTokens.Contains("override")) methodBuilder.Override();
            if (modTokens.Contains("abstract")) methodBuilder.Abstract();
        }
        catch { }

        // Parse parameters
        if (!string.IsNullOrWhiteSpace(paramsStr))
        {
            var paramParts = paramsStr.Split(',');
            foreach (var paramPart in paramParts)
            {
                var paramMatch = Regex.Match(paramPart.Trim(), @"(\w+)\s*:\s*(.+)");
                if (paramMatch.Success)
                {
                    string paramName = paramMatch.Groups[1].Value;
                    string paramType = paramMatch.Groups[2].Value.Trim();
                    methodBuilder.Parameter(paramName, ParseTypeReference(paramType));
                }
            }
        }

        // Try to get underlying MethodDefinition via reflection so we can populate Instructions
        MethodDefinition? methodDef = null;
        try
        {
            var mbType = methodBuilder.GetType();
            var methodField = mbType.GetField("_method", BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodField != null)
            {
                methodDef = methodField.GetValue(methodBuilder) as MethodDefinition;
            }
        }
        catch { }

        int endIndex = FindBlockEnd(lines, startIndex);

        int i = startIndex + 1;
        while (i < endIndex)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("local "))
            {
                var localMatch = Regex.Match(line, @"local\s+(\w+)\s*:\s*(.+)$");
                if (localMatch.Success && methodDef != null)
                {
                    string localName = localMatch.Groups[1].Value;
                    string localType = localMatch.Groups[2].Value.Trim();
                    methodDef.DefineLocal(localName, ParseTypeReference(localType));
                }
                i++;
                continue;
            }

            if (line.StartsWith("if ") || line.StartsWith("if("))
            {
                // parse if(condition) { ... } [else { ... }]
                var condMatch = Regex.Match(line, @"if\s*\((.*)\)\s*\{?");
                Condition condition = Condition.Stack();
                if (condMatch.Success)
                {
                    condition = ParseConditionFromText(condMatch.Groups[1].Value.Trim());
                }

                int ifEnd = FindBlockEnd(lines, i);
                var ifInst = new IfInstruction(condition);
                // parse then block
                ParseInstructionsIntoList(methodDef, lines, i + 1, ifEnd, ifInst.ThenBlock);

                // check for else
                int nextIndex = ifEnd + 1;
                if (nextIndex < lines.Count && lines[nextIndex].TrimStart().StartsWith("else"))
                {
                    int elseEnd = FindBlockEnd(lines, nextIndex);
                    ifInst.ElseBlock = new InstructionList();
                    ParseInstructionsIntoList(methodDef, lines, nextIndex + 1, elseEnd, ifInst.ElseBlock);
                    i = elseEnd + 1;
                }
                else
                {
                    i = ifEnd + 1;
                }

                if (methodDef != null) methodDef.Instructions.Add(ifInst);
                continue;
            }

            if (line.StartsWith("while ") || line.StartsWith("while("))
            {
                var condMatch = Regex.Match(line, @"while\s*\((.*)\)\s*\{?");
                Condition condition = Condition.Stack();
                if (condMatch.Success)
                {
                    condition = ParseConditionFromText(condMatch.Groups[1].Value.Trim());
                }
                int wEnd = FindBlockEnd(lines, i);
                var whileInst = new WhileInstruction(condition);
                ParseInstructionsIntoList(methodDef, lines, i + 1, wEnd, whileInst.Body);
                if (methodDef != null) methodDef.Instructions.Add(whileInst);
                i = wEnd + 1;
                continue;
            }

            if (line.StartsWith("foreach") || line.StartsWith("for("))
            {
                var feMatch = Regex.Match(line, @"foreach\s*\((\w+)\s+in\s+(\w+)\)\s*\{?");
                if (feMatch.Success)
                {
                    var item = feMatch.Groups[1].Value;
                    var coll = feMatch.Groups[2].Value;
                    int feEnd = FindBlockEnd(lines, i);
                    var feInst = new ForEachInstruction(item, coll);
                    ParseInstructionsIntoList(methodDef, lines, i + 1, feEnd, feInst.Body);
                    if (methodDef != null) methodDef.Instructions.Add(feInst);
                    i = feEnd + 1;
                    continue;
                }
            }

            // Single-line instruction parsing
            var instr = ParseSingleInstruction(line);
            if (instr != null && methodDef != null)
            {
                methodDef.Instructions.Add(instr);
            }
            i++;
        }

        methodBuilder.EndMethod();
        return endIndex + 1;
    }

    private TypeReference ParseTypeReference(string typeStr)
    {
        typeStr = typeStr.Trim();
        if (typeStr == "void") return TypeReference.Void;
        if (typeStr == "int16") return TypeReference.Int16;
        if (typeStr == "int32") return TypeReference.Int32;
        if (typeStr == "int64") return TypeReference.Int64;
        if (typeStr == "uint8") return TypeReference.UInt8;
        if (typeStr == "uint16") return TypeReference.UInt16;
        if (typeStr == "uint32") return TypeReference.UInt32;
        if (typeStr == "uint64") return TypeReference.UInt64;
        if (typeStr == "float32") return TypeReference.Float32;
        if (typeStr == "float64") return TypeReference.Float64;
        if (typeStr == "char") return TypeReference.Char;
        if (typeStr == "string") return TypeReference.String;

        return TypeReference.FromName(typeStr);
    }

    private List<string> GetModifierTokens(string modifiersStr)
    {
        if (string.IsNullOrWhiteSpace(modifiersStr)) return new List<string>();
        return modifiersStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLowerInvariant()).ToList();
    }

    private AccessModifier ToAccessModifier(IEnumerable<string> mods)
    {
        var list = mods.ToList();
        if (list.Contains("private")) return AccessModifier.Private;
        if (list.Contains("protected")) return AccessModifier.Protected;
        if (list.Contains("internal")) return AccessModifier.Internal;
        return AccessModifier.Public;
    }

    private int FindBlockEnd(List<string> lines, int startIndex)
    {
        int depth = 0;
        bool started = false;
        for (int i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i];
            for (int c = 0; c < line.Length; c++)
            {
                if (line[c] == '{') { depth++; started = true; }
                else if (line[c] == '}') { depth--; }
            }

            if (started && depth == 0)
            {
                return i;
            }
        }
        // If we never find a matching brace, return last line
        return lines.Count - 1;
    }

    private Condition ParseConditionFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Condition.Stack();

        var t = text.Trim();

        // explicit negation like '!stack' or leading '!'
        if (t.StartsWith("!"))
        {
            var inner = t.Substring(1).Trim();
            if (string.IsNullOrEmpty(inner) || inner.Equals("stack", StringComparison.OrdinalIgnoreCase))
                return Condition.Not(Condition.Stack());
            // try parse inner condition recursively
            return Condition.Not(ParseConditionFromText(inner));
        }

        // operator detection: check multi-char ops first
        if (t.Contains("!=")) return Condition.Binary(ComparisonOp.NotEqual);
        if (t.Contains("==")) return Condition.Binary(ComparisonOp.Equal);
        if (t.Contains(">=")) return Condition.Binary(ComparisonOp.GreaterOrEqual);
        if (t.Contains("<=")) return Condition.Binary(ComparisonOp.LessOrEqual);
        if (t.Contains(">")) return Condition.Binary(ComparisonOp.Greater);
        if (t.Contains("<")) return Condition.Binary(ComparisonOp.Less);

        // fallback to stack condition
        return Condition.Stack();
    }

    private void ParseInstructionsIntoList(MethodDefinition? methodDef, List<string> lines, int startInclusive, int endExclusive, InstructionList target)
    {
        int i = startInclusive;
        while (i < endExclusive)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            if (line.StartsWith("local "))
            {
                var localMatch = Regex.Match(line, @"local\s+(\w+)\s*:\s*(.+)$");
                if (localMatch.Success && methodDef != null)
                {
                    string localName = localMatch.Groups[1].Value;
                    string localType = localMatch.Groups[2].Value.Trim();
                    methodDef.DefineLocal(localName, ParseTypeReference(localType));
                }
                i++; continue;
            }

            if (line.StartsWith("if ") || line.StartsWith("if("))
            {
                var condMatch = Regex.Match(line, @"if\s*\((.*)\)\s*\{?");
                Condition condition = Condition.Stack();
                if (condMatch.Success) condition = ParseConditionFromText(condMatch.Groups[1].Value.Trim());
                int ifEnd = FindBlockEnd(lines, i);
                var ifInst = new IfInstruction(condition);
                ParseInstructionsIntoList(methodDef, lines, i + 1, ifEnd, ifInst.ThenBlock);
                int nextIndex = ifEnd + 1;
                if (nextIndex < lines.Count && lines[nextIndex].TrimStart().StartsWith("else"))
                {
                    int elseEnd = FindBlockEnd(lines, nextIndex);
                    ifInst.ElseBlock = new InstructionList();
                    ParseInstructionsIntoList(methodDef, lines, nextIndex + 1, elseEnd, ifInst.ElseBlock);
                    i = elseEnd + 1;
                }
                else { i = ifEnd + 1; }
                target.Add(ifInst);
                continue;
            }

            if (line.StartsWith("while ") || line.StartsWith("while("))
            {
                var condMatch = Regex.Match(line, @"while\s*\((.*)\)\s*\{?");
                Condition condition = Condition.Stack();
                if (condMatch.Success) condition = ParseConditionFromText(condMatch.Groups[1].Value.Trim());
                int wEnd = FindBlockEnd(lines, i);
                var whileInst = new WhileInstruction(condition);
                ParseInstructionsIntoList(methodDef, lines, i + 1, wEnd, whileInst.Body);
                target.Add(whileInst);
                i = wEnd + 1; continue;
            }

            if (line.StartsWith("foreach") || line.StartsWith("for("))
            {
                var feMatch = Regex.Match(line, @"foreach\s*\((\w+)\s+in\s+(\w+)\)\s*\{?");
                if (feMatch.Success)
                {
                    var item = feMatch.Groups[1].Value; var coll = feMatch.Groups[2].Value;
                    int feEnd = FindBlockEnd(lines, i);
                    var feInst = new ForEachInstruction(item, coll);
                    ParseInstructionsIntoList(methodDef, lines, i + 1, feEnd, feInst.Body);
                    target.Add(feInst);
                    i = feEnd + 1; continue;
                }
            }

            var instr = ParseSingleInstruction(line);
            if (instr != null) target.Add(instr);
            i++;
        }
    }

    private Instruction? ParseSingleInstruction(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        // ldc (type) value or shorthand
        var m = Regex.Match(line, @"^ldc(?:\s+(\w+))?\s+(.+)$");
        if (m.Success)
        {
            var t = m.Groups[1].Success ? m.Groups[1].Value.Trim() : "int32";
            var v = m.Groups[2].Value.Trim();
            if (t == "string" || t == "System.String") return new LoadConstantInstruction(v.Trim('"'), TypeReference.String);
            if (t == "int32") return new LoadConstantInstruction(int.Parse(v), TypeReference.Int32);
            if (t == "int64") return new LoadConstantInstruction(long.Parse(v), TypeReference.Int64);
            if (t == "float32") return new LoadConstantInstruction(float.Parse(v), TypeReference.Float32);
            if (t == "float64") return new LoadConstantInstruction(double.Parse(v), TypeReference.Float64);
            return new LoadConstantInstruction(v, TypeReference.FromName(t));
        }

        if (line.StartsWith("ldstr "))
        {
            var val = line.Substring("ldstr ".Length).Trim().Trim('"');
            return new LoadConstantInstruction(val, TypeReference.String);
        }

        if (line.StartsWith("ldnull")) return new LoadNullInstruction();

        var argMatch = Regex.Match(line, @"^ldarg\s+(\w+)$");
        if (argMatch.Success) return new LoadArgInstruction(0) { };

        var ldloc = Regex.Match(line, @"^ldloc\s+(\w+)$");
        if (ldloc.Success) return new LoadLocalInstruction(ldloc.Groups[1].Value);

        var stloc = Regex.Match(line, @"^stloc\s+(\w+)$");
        if (stloc.Success) return new StoreLocalInstruction(stloc.Groups[1].Value);

        var ldfldMatch = Regex.Match(line, @"^ldfld\s+(.+)$");
        if (ldfldMatch.Success)
        {
            var full = ldfldMatch.Groups[1].Value.Trim();
            string declName; string fieldName;
            if (full.Contains("::"))
            {
                var idx = full.LastIndexOf("::", StringComparison.Ordinal);
                declName = full.Substring(0, idx);
                fieldName = full.Substring(idx + 2);
            }
            else if (full.Contains('.'))
            {
                var idx = full.LastIndexOf('.');
                declName = full.Substring(0, idx);
                fieldName = full.Substring(idx + 1);
            }
            else
            {
                declName = full; fieldName = "";
            }

            if (!string.IsNullOrEmpty(fieldName))
            {
                var decl = TypeReference.FromName(declName);
                return new LoadFieldInstruction(new FieldReference(decl, fieldName, TypeReference.FromName("object")));
            }
        }

        var stfldMatch = Regex.Match(line, @"^stfld\s+(.+)$");
        if (stfldMatch.Success)
        {
            var full = stfldMatch.Groups[1].Value.Trim();
            string declName; string fieldName;
            if (full.Contains("::"))
            {
                var idx = full.LastIndexOf("::", StringComparison.Ordinal);
                declName = full.Substring(0, idx);
                fieldName = full.Substring(idx + 2);
            }
            else if (full.Contains('.'))
            {
                var idx = full.LastIndexOf('.');
                declName = full.Substring(0, idx);
                fieldName = full.Substring(idx + 1);
            }
            else
            {
                declName = full; fieldName = "";
            }

            if (!string.IsNullOrEmpty(fieldName))
            {
                var decl = TypeReference.FromName(declName);
                return new StoreFieldInstruction(new FieldReference(decl, fieldName, TypeReference.FromName("object")));
            }
        }

        if (line.StartsWith("newobj "))
        {
            var typeName = line.Substring("newobj ".Length).Trim();
            return new NewObjectInstruction(TypeReference.FromName(typeName));
        }

        if (line == "add") return new ArithmeticInstruction(ArithmeticOp.Add);
        if (line == "sub") return new ArithmeticInstruction(ArithmeticOp.Sub);
        if (line == "mul") return new ArithmeticInstruction(ArithmeticOp.Mul);
        if (line == "div") return new ArithmeticInstruction(ArithmeticOp.Div);
        if (line == "rem") return new ArithmeticInstruction(ArithmeticOp.Rem);

        if (line == "ceq" || line == "cne" || line == "cgt" || line == "clt" || line == "cge" || line == "cle")
        {
            var map = line switch
            {
                "ceq" => ComparisonOp.Equal,
                "cne" => ComparisonOp.NotEqual,
                "cgt" => ComparisonOp.Greater,
                "cge" => ComparisonOp.GreaterOrEqual,
                "clt" => ComparisonOp.Less,
                "cle" => ComparisonOp.LessOrEqual,
                _ => ComparisonOp.Equal
            };
            return new ComparisonInstruction(map);
        }

        if (line == "dup") return new DupInstruction();
        if (line == "pop") return new PopInstruction();
        if (line == "ret") return new ReturnInstruction(null);

        // call and callvirt: support both 'Type::Method(...)' and 'Type.Method(...)' and optional '-> returnType'
        var callMatch = Regex.Match(line, @"^(callvirt|call)\s+(.+?)\s*\((.*)\)\s*(?:->\s*([\w\.<>]+))?$");
        if (callMatch.Success)
        {
            var kind = callMatch.Groups[1].Value;
            var methodSpec = callMatch.Groups[2].Value.Trim();
            var paramList = callMatch.Groups[3].Value.Trim();
            var returnTypeName = callMatch.Groups[4].Success ? callMatch.Groups[4].Value.Trim() : "void";

            // split methodSpec into declaring type and method name by last '::' or '.'
            string declTypeName; string methodName;
            if (methodSpec.Contains("::"))
            {
                var idx = methodSpec.LastIndexOf("::", StringComparison.Ordinal);
                declTypeName = methodSpec.Substring(0, idx);
                methodName = methodSpec.Substring(idx + 2);
            }
            else if (methodSpec.Contains('.'))
            {
                var idx = methodSpec.LastIndexOf('.');
                declTypeName = methodSpec.Substring(0, idx);
                methodName = methodSpec.Substring(idx + 1);
            }
            else
            {
                declTypeName = methodSpec; methodName = "";
            }

            if (!string.IsNullOrEmpty(methodName))
            {
                var decl = TypeReference.FromName(declTypeName);
                var paramTypes = new List<TypeReference>();
                if (!string.IsNullOrWhiteSpace(paramList))
                {
                    var parts = paramList.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p));
                    foreach (var p in parts) paramTypes.Add(TypeReference.FromName(p));
                }
                var returnType = TypeReference.FromName(returnTypeName);
                var methodRef = new MethodReference(decl, methodName, returnType, paramTypes);
                return kind == "callvirt" ? new CallVirtualInstruction(methodRef) as Instruction : new CallInstruction(methodRef);
            }
        }

        return null;
    }

    private bool IsInstruction(string line)
    {
        var instructionKeywords = new[] 
        { 
            "ldarg", "ldloc", "ldfld", "stloc", "stfld", "ldc", "ldstr", "ldnull",
            "add", "sub", "mul", "div", "rem", "neg", "ceq", "cgt", "clt",
            "call", "callvirt", "newobj", "dup", "pop", "ret", "br", "beq",
            "if", "while", "for", "switch"
        };

        return instructionKeywords.Any(keyword => line.StartsWith(keyword));
    }
}
