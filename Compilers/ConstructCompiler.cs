namespace ObjectIR.Core.Compilers;

using ObjectIR.Core.Builder;
using ObjectIR.Core.AST;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Compiles Construct language AST to ObjectIR modules
/// </summary>
public class ConstructCompiler
{
    private IRBuilder? _moduleBuilder;
    private Dictionary<string, string> _variables = new(); // Variable name -> type
    private Dictionary<string, TypeRef> _typeMap = new();

    public ConstructCompiler()
    {
        InitializeTypeMap();
    }

    private void InitializeTypeMap()
    {
        _typeMap["Int"] = TypeRef.Int32;
        _typeMap["String"] = TypeRef.String;
        _typeMap["Bool"] = TypeRef.Bool;
        _typeMap["void"] = TypeRef.Void;
    }

    public ModuleNode Compile(Program program)
    {
        var contract = program.Contract;
        _moduleBuilder = new IRBuilder(contract.Name);
        _variables.Clear();

        // Create a main class to hold all functions
        var classBuilder = _moduleBuilder.Class(contract.Name);

        foreach (var function in contract.Functions)
        {
            CompileFunction(classBuilder, function);
        }

        classBuilder.EndClass();
        return _moduleBuilder.Build();
    }

    private void CompileFunction(ClassBuilder classBuilder, FunctionDeclaration function)
    {
        // Determine return type
        TypeRef returnType = function.ReturnType != null
            ? ResolveType(function.ReturnType.Name)
            : TypeRef.Void;

        // Create method with return type
        var methodBuilder = classBuilder.Method(function.Name, returnType)
            .Access(AccessModifier.Public)
            .Static();

        // Add parameters
        foreach (var param in function.Parameters)
        {
            TypeRef paramType = ResolveType(param.Type.Name);
            methodBuilder.Parameter(param.Name, paramType);
        }

        methodBuilder.EndMethod();

        // Note: Full instruction generation would require more detailed implementation
        // This is a simplified version that creates the structure
    }

    private TypeRef ResolveType(string typeName)
    {
        if (_typeMap.TryGetValue(typeName, out var typeRef))
            return typeRef;

        // For custom types, return a reference using a qualified name
        return new TypeRef(typeName);
    }
}

/// <summary>
/// High-level Construct to ObjectIR compiler
/// </summary>
public class ConstructLanguageCompiler
{
    /// <summary>
    /// Compiles Construct source code to an ObjectIR module node
    /// </summary>
    public ModuleNode CompileSource(string sourceCode)
    {
        // Lexical analysis
        var lexer = new ConstructLexer(sourceCode);
        var tokens = lexer.Tokenize();

        // Syntax analysis
        var parser = new ConstructParser(tokens);
        var program = parser.Parse();

        // Code generation
        var compiler = new ConstructCompiler();
        var module = compiler.Compile(program);

        return module;
    }
}
