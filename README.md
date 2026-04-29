# ObjectIR.Core

A .NET 8 library for building, manipulating, and serialising an **object-oriented intermediate representation (IR)**. Think LLVM IR, but designed from the ground up for OO languages — classes, interfaces, structs, generics, virtual dispatch, and all.


## Lifecycle

Support for using ObjectIR.Core.IR has ended, please migrate any tooling to consume ObjectIR.Core.AST instead.
documentation will take time to update but is straightfoward to use when reading related files in `Core/*` and `Core/AST/*`
for legacy codebases, please use commit `d54358e28075f656511b14db0e409d4c1500da84` to maintain your codebases.

thank you: `charlie`

---

## Why ObjectIR?

Most compiler toolkits either target a low-level bytecode (too far from OO semantics) or tie you to a specific runtime (too opinionated). ObjectIR.Core gives you a strongly-typed in-memory graph of your program's types and methods that you can:

- **Build** with a fluent API or parse from a simple text format
- **Query** for dependencies and do topological analysis
- **Compose** multiple modules into one
- **Serialize** to JSON, BSON, or the compact FOB binary format

---

## Quick start

```bash
dotnet add package ObjectIR.Core
```

```csharp
using ObjectIR.Core.Builder;
using ObjectIR.Core.IR;

var module = new IRBuilder("MyApp")
    .Class("Animal")
        .Field("name", TypeReference.String)
            .Access(AccessModifier.Private)
            .EndField()
        .Method("Speak", TypeReference.String)
            .Virtual()
            .Body()
                .Ldarg(0)
                .Ldfld(new FieldReference(TypeReference.FromName("Animal"), "name", TypeReference.String))
                .Ret()
            .EndBody()
        .EndMethod()
    .EndClass()
    .Build();

Console.WriteLine(module.Name);  // MyApp
Console.WriteLine(module.Types[0].Name);  // Animal
```

---

## Documentation

| Page | What's covered |
|------|----------------|
| [Getting Started](docs/getting-started.md) | Install, first example, what to read next |
| [Architecture](docs/architecture.md) | Namespaces, layer diagram, design decisions |
| [IR Model](docs/ir-model.md) | `Module`, type definitions, members, instructions |
| [Builder API](docs/builder-api.md) | Fluent `IRBuilder` walkthrough with examples |
| [Serialization](docs/serialization.md) | Text format, JSON/BSON, `ModuleLoader` |
| [Composition](docs/composition.md) | Multi-module merging and dependency resolution |
| [FOB Format](docs/fob-format.md) | Compact binary format spec |

---

## Project layout

```
ObjectIR.Core/
├── IR/               # Core data model (Module, TypeDefinition, Instructions …)
├── Builder/          # Fluent IRBuilder API
├── Serialization/    # Text, JSON, BSON serializers and ModuleLoader
├── Composition/      # ModuleComposer, DependencyResolver
├── Compilers/        # Construct-language front-end compiler
├── Parsing/          # Text IR parser
├── Ast/              # AST node types
└── docs/             # This documentation
```

---

## License

MIT — see [LICENSE](LICENSE) for details.
