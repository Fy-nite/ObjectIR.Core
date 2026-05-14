namespace ObjectIR.Core.Builder;

using System;
using System.Collections.Generic;
using ObjectIR.Core.AST;

/// <summary>
/// Fluent API for building IR modules using AST nodes
/// </summary>
public sealed class IRBuilder
{
    private ModuleNode _module;

    public IRBuilder(string moduleName)
    {
        _module = new ModuleNode(moduleName);
    }

    public ModuleNode Build() => _module;

    // ========================================================================
    // Type Building
    // ========================================================================

    public ClassBuilder Class(string name)
    {
        var classNode = new ClassNode(name);
        _module.Classes.Add(classNode);
        return new ClassBuilder(this, classNode);
    }

    public InterfaceBuilder Interface(string name)
    {
        var interfaceNode = new InterfaceNode(name);
        _module.Interfaces.Add(interfaceNode);
        return new InterfaceBuilder(this, interfaceNode);
    }

    public StructBuilder Struct(string name)
    {
        var structNode = new StructNode(name);
        _module.Structs.Add(structNode);
        return new StructBuilder(this, structNode);
    }
}

/// <summary>
/// Builder for class definitions
/// </summary>
public sealed class ClassBuilder
{
    private readonly IRBuilder _builder;
    private readonly ClassNode _class;

    internal ClassBuilder(IRBuilder builder, ClassNode classNode)
    {
        _builder = builder;
        _class = classNode;
    }

    public ClassBuilder Access(AccessModifier access)
    {
        _class.Modifiers.Add(access);
        return this;
    }

    public ClassBuilder Namespace(string ns)
    {
        _class.Namespace = ns;
        return this;
    }

    public ClassBuilder Extends(string baseType)
    {
        _class.BaseType = baseType;
        return this;
    }

    public ClassBuilder Implements(params string[] interfaces)
    {
        _class.Interfaces.AddRange(interfaces);
        return this;
    }

    public ClassBuilder Abstract()
    {
        _class.IsAbstract = true;
        return this;
    }

    public ClassBuilder Sealed()
    {
        _class.IsSealed = true;
        return this;
    }

    public ClassBuilder Generic(params string[] parameters)
    {
        foreach (var param in parameters)
        {
            _class.GenericParameters.Add(new GenericParameterNode(param));
        }
        return this;
    }

    public FieldBuilder Field(string name, TypeRef type)
    {
        var field = new FieldNode(name, type);
        _class.Fields.Add(field);
        return new FieldBuilder(this, field);
    }

    public MethodBuilder Method(string name, TypeRef returnType)
    {
        var method = new MethodNode(name) { ReturnType = returnType };
        _class.Methods.Add(method);
        return new MethodBuilder(this, method);
    }

    public MethodBuilder Constructor()
    {
        var ctor = new ConstructorNode();
        _class.Constructors.Add(ctor);
        return new MethodBuilder(this, ctor);
    }

    public IRBuilder EndClass()
    {
        return _builder;
    }
}

/// <summary>
/// Builder for interface definitions
/// </summary>
public sealed class InterfaceBuilder
{
    private readonly IRBuilder _builder;
    private readonly InterfaceNode _interface;

    internal InterfaceBuilder(IRBuilder builder, InterfaceNode interfaceNode)
    {
        _builder = builder;
        _interface = interfaceNode;
    }

    public InterfaceBuilder Access(AccessModifier access)
    {
        _interface.Access = access;
        return this;
    }

    public InterfaceBuilder Namespace(string ns)
    {
        _interface.Namespace = ns;
        return this;
    }

    public InterfaceBuilder Method(string name, TypeRef returnType)
    {
        _interface.Methods.Add(new MethodSignature(name) { ReturnType = returnType });
        return this;
    }

    public IRBuilder EndInterface() => _builder;
}

/// <summary>
/// Builder for struct definitions
/// </summary>
public sealed class StructBuilder
{
    private readonly IRBuilder _builder;
    private readonly StructNode _struct;

    internal StructBuilder(IRBuilder builder, StructNode structNode)
    {
        _builder = builder;
        _struct = structNode;
    }

    public StructBuilder Access(AccessModifier access)
    {
        _struct.Access = access;
        return this;
    }

    public StructBuilder Field(string name, TypeRef type)
    {
        _struct.Fields.Add(new FieldNode(name, type));
        return this;
    }

    public IRBuilder EndStruct() => _builder;
}

/// <summary>
/// Builder for field definitions
/// </summary>
public sealed class FieldBuilder
{
    private readonly ClassBuilder _classBuilder;
    private readonly FieldNode _field;

    internal FieldBuilder(ClassBuilder classBuilder, FieldNode field)
    {
        _classBuilder = classBuilder;
        _field = field;
    }

    public FieldBuilder Access(AccessModifier access)
    {
        _field.Access = access;
        return this;
    }

    public FieldBuilder Static()
    {
        _field.IsStatic = true;
        return this;
    }

    public FieldBuilder ReadOnly()
    {
        _field.IsReadOnly = true;
        return this;
    }

    public FieldBuilder InitialValue(object value)
    {
        _field.InitialValue = value;
        return this;
    }

    public ClassBuilder EndField() => _classBuilder;
}

/// <summary>
/// Builder for method definitions
/// </summary>
public sealed class MethodBuilder
{
    private readonly ClassBuilder? _classBuilder;
    private readonly MethodNode? _method;
    private readonly ConstructorNode? _ctor;

    internal MethodBuilder(ClassBuilder classBuilder, MethodNode method)
    {
        _classBuilder = classBuilder;
        _method = method;
    }

    internal MethodBuilder(ClassBuilder classBuilder, ConstructorNode ctor)
    {
        _classBuilder = classBuilder;
        _ctor = ctor;
    }

    public MethodBuilder Access(AccessModifier access)
    {
        if (_method != null) _method.Access = access;
        return this;
    }

    public MethodBuilder Static()
    {
        if (_method != null) _method.IsStatic = true;
        return this;
    }

    public MethodBuilder Virtual()
    {
        if (_method != null) _method.IsVirtual = true;
        return this;
    }

    public MethodBuilder Override()
    {
        if (_method != null) _method.IsOverride = true;
        return this;
    }

    public MethodBuilder Abstract()
    {
        if (_method != null) _method.IsAbstract = true;
        return this;
    }

    public MethodBuilder Parameter(string name, TypeRef type)
    {
        var param = new ParameterNode(name, type);
        if (_method != null) _method.Parameters.Add(param);
        else if (_ctor != null) _ctor.Parameters.Add(param);
        return this;
    }

    public MethodBuilder Local(string name, TypeRef type)
    {
        _method?.Locals.Add(new LocalDeclarationStatement(name, type));
        return this;
    }

    public InstructionBuilder Body()
    {
        var body = _method?.Body ?? _ctor?.Body ?? new BlockStatement(new());
        if (_method != null) _method.Body = body;
        else if (_ctor != null) _ctor.Body = body;
        return new InstructionBuilder(this, body.Statements);
    }

    public ClassBuilder? EndMethod()
    {
        return _classBuilder;
    }
}

/// <summary>
/// Builder for emitting instructions into an AST BlockStatement
/// </summary>
public sealed class InstructionBuilder
{
    private readonly MethodBuilder _methodBuilder;
    private readonly List<Statement> _statements;

    internal InstructionBuilder(MethodBuilder methodBuilder, List<Statement> statements)
    {
        _methodBuilder = methodBuilder;
        _statements = statements;
    }

    internal SourceLocation? _currentLocation;

    public InstructionBuilder SetLocation(int line, int column, string? sourceLine = null)
    {
        _currentLocation = new SourceLocation(line, column, sourceLine);
        return this;
    }

    public InstructionBuilder ClearLocation()
    {
        _currentLocation = null;
        return this;
    }

    private InstructionBuilder Emit(string opCode, string? operand = null)
    {
        var instruction = new SimpleInstruction(opCode, operand) { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instruction) { Location = _currentLocation });
        return this;
    }

    // Load/Store
    public InstructionBuilder Ldarg(int index) => Emit("ldarg", index.ToString());
    public InstructionBuilder Starg(int index) => Emit("starg", index.ToString());
    public InstructionBuilder Ldloc(string name) => Emit("ldloc", name);
    public InstructionBuilder Stloc(string name) => Emit("stloc", name);
    
    public InstructionBuilder Local(string name, TypeRef type)
    {
        _methodBuilder.Local(name, type);
        return this;
    }
    
    public InstructionBuilder Ldfld(FieldReference field)
    {
        var instr = new SimpleInstruction("ldfld", $"{field.DeclaringType.Name}::{field.Name}") { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    public InstructionBuilder Ldsfld(FieldReference field)
    {
        var instr = new SimpleInstruction("ldsfld", $"{field.DeclaringType.Name}::{field.Name}") { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    public InstructionBuilder LdcI4(int value) => Emit("ldc.i4", value.ToString());
    public InstructionBuilder LdcR4(float value) => Emit("ldc.r4", value.ToString());
    public InstructionBuilder Ldstr(string value) => Emit("ldstr", value);
    public InstructionBuilder Ldnull() => Emit("ldnull");

    public InstructionBuilder Stfld(FieldReference field)
    {
        var instr = new SimpleInstruction("stfld", $"{field.DeclaringType.Name}::{field.Name}") { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    // Arithmetic
    public InstructionBuilder Add() => Emit("add");
    public InstructionBuilder Sub() => Emit("sub");
    public InstructionBuilder Mul() => Emit("mul");
    public InstructionBuilder Div() => Emit("div");
    public InstructionBuilder Rem() => Emit("rem");
    public InstructionBuilder Neg() => Emit("neg");

    // Logical
    public InstructionBuilder And() => Emit("and");
    public InstructionBuilder Or() => Emit("or");
    public InstructionBuilder Xor() => Emit("xor");
    public InstructionBuilder Not() => Emit("not");

    // Comparison
    public InstructionBuilder Ceq() => Emit("ceq");
    public InstructionBuilder Cgt() => Emit("cgt");
    public InstructionBuilder Clt() => Emit("clt");
    
    public InstructionBuilder Cne() => Ceq().Not();
    public InstructionBuilder Cge() => Clt().Not();
    public InstructionBuilder Cle() => Cgt().Not();

    // Calls
    public InstructionBuilder Call(MethodReference method) 
    {
        var instr = new CallInstruction(method, new List<TypeRef>(), false) { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    public InstructionBuilder Callvirt(MethodReference method)
    {
        var instr = new CallInstruction(method, new List<TypeRef>(), true) { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    // Object operations
    public InstructionBuilder Ldelem() => Emit("ldelem");
    public InstructionBuilder Stelem() => Emit("stelem");

    public InstructionBuilder Newobj(TypeRef type, MethodReference? constructor = null)
    {
        var instr = new NewObjInstruction(type, constructor, new List<TypeRef>()) { Location = _currentLocation };
        _statements.Add(new InstructionStatement(instr) { Location = _currentLocation });
        return this;
    }

    // Stack
    public InstructionBuilder Dup() => Emit("dup");
    public InstructionBuilder Pop() => Emit("pop");

    // Control flow
    public InstructionBuilder Ret() => Emit("ret");

    public InstructionBuilder If(string condition, Action<InstructionBuilder> thenBlock, Action<InstructionBuilder>? elseBlock = null)
    {
        var thenStatements = new List<Statement>();
        var thenBuilder = new InstructionBuilder(_methodBuilder, thenStatements) { _currentLocation = _currentLocation };
        thenBlock(thenBuilder);
        
        BlockStatement? elseStmt = null;
        if (elseBlock != null)
        {
            var elseStatements = new List<Statement>();
            var elseBuilder = new InstructionBuilder(_methodBuilder, elseStatements) { _currentLocation = _currentLocation };
            elseBlock(elseBuilder);
            elseStmt = new BlockStatement(elseStatements) { Location = _currentLocation };
        }

        var ifStmt = new IfStatement(condition, new BlockStatement(thenStatements) { Location = _currentLocation }, elseStmt) { Location = _currentLocation };
        _statements.Add(ifStmt);
        return this;
    }

    public InstructionBuilder While(string condition, Action<InstructionBuilder> body)
    {
        var bodyStatements = new List<Statement>();
        var bodyBuilder = new InstructionBuilder(_methodBuilder, bodyStatements) { _currentLocation = _currentLocation };
        body(bodyBuilder);
        var whileStmt = new WhileStatement(condition, new BlockStatement(bodyStatements) { Location = _currentLocation }) { Location = _currentLocation };
        _statements.Add(whileStmt);
        return this;
    }

    public InstructionBuilder Switch(string expression, Action<SwitchBuilder> cases)
    {
        var switchBuilder = new SwitchBuilder(_methodBuilder, _currentLocation);
        cases(switchBuilder);
        var switchStmt = new SwitchStatement(expression, switchBuilder.Cases) { Location = _currentLocation };
        _statements.Add(switchStmt);
        return this;
    }

    public MethodBuilder EndBody() => _methodBuilder;
}

public sealed class SwitchBuilder
{
    private readonly MethodBuilder _methodBuilder;
    private readonly SourceLocation? _location;
    public List<SwitchCase> Cases { get; } = new();

    internal SwitchBuilder(MethodBuilder methodBuilder, SourceLocation? location)
    {
        _methodBuilder = methodBuilder;
        _location = location;
    }

    public SwitchBuilder Case(int? value, Action<InstructionBuilder> body)
    {
        var bodyStatements = new List<Statement>();
        var bodyBuilder = new InstructionBuilder(_methodBuilder, bodyStatements) { _currentLocation = _location };
        body(bodyBuilder);
        Cases.Add(new SwitchCase(value, new BlockStatement(bodyStatements) { Location = _location }) { Location = _location });
        return this;
    }
}
