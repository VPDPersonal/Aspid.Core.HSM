---
name: asp-xmldoc
description: XML documentation (`///`) and Unity `[Tooltip]` conventions for C# code. Load it before you write any `///` comment, also for new public API you add while coding, and when you edit or review XML docs; phrases like "document this", "add XML docs", "write a summary", «summary», «докс», «xml-доки». Project skills may add rules on top.
user-invocable: false
---

# C# XML Documentation

Base: .NET conventions (dotnet/runtime, StyleCop SA16xx); deviations are marked. Write XML docs in English.

**Main rule: brevity.** No filler ("This class provides...", "is used to..."), no restating the declaration, no motivation. Every sentence adds what the signature lacks.

## Scope

- Document only API reachable from another assembly: `public`, `protected`, `protected internal`.
  `InternalsVisibleTo` does not count: friend assemblies (tests, editor) are not API users.
- Do not add or keep XML docs on `internal`, `private` or `private protected` members. Delete existing ones on the members you edit, and in the code you review.
- A `public` or `protected` member inside an `internal` or `private` type is not reachable: no XML docs.
- Explicit interface implementations are reachable through the interface: give them `<inheritdoc/>`.
- Partial type: put the type's `<summary>` on one part only.

## Tag order

`<summary>`, `<remarks>`, `<typeparam>`, `<param>`, `<returns>`, `<exception>`, `<example>`.

`<value>` is not used: a property is described in its `<summary>`.

## `<summary>`

- One sentence that ends with a period. Exceptions: property defaults and hooks below add a second sentence.
- Multi-line form: `<summary>` and `</summary>` on their own lines.
- **Type in an inheritance chain** (deviation from .NET): start with the parent cref, never `A` / `The` / `This`. State only what this type adds.
  - Form: `<see cref="Parent"/>` and then `that ...`, `for ...` or a participle: `<see cref="EditorWindow"/> for selecting a type from a filtered hierarchy.`, `<see cref="TypeField"/> pre-styled as an Inspector property row.`
  - Do not restate the base class, interfaces or generic parameters.
  - A type that only closes the parent's type parameters starts with `Concrete`: `Concrete <see cref="Parent{T, TBoxed}"/> that fixes <c>TBoxed</c> to <see cref="Enum"/>.`
  - A type that wraps a member of another type: cref the member, not its type (`<see cref="Transform.parent"/>`, not `<see cref="Transform"/>`).
  - The parent is an abstract base of the same family (`SerializableTypeBase`): describe the type as a root type.
- **Root or flat type**: class `Represents ...` or a noun phrase without an article (`Unity-serializable wrapper around a <see cref="System.Type"/> ...`), interface `Defines ...`, enum `Specifies ...`, delegate `Represents the method that ...`. No "implementing IFoo".
- Extension and utility classes: `Provides extension methods for <see cref="X"/>.` / `Provides utility methods for ...`.
- Attributes: third-person verb for the effect (`Draws the field with the type-selector window.`). This form wins over the inheritance-chain form.
- Properties: `Gets ...`, `Gets or sets ...`, boolean `Gets a value indicating whether ...`. Non-obvious default or `null` meaning: second sentence or after a semicolon (`...; <see cref="object"/> when unconstrained.`).
- Methods: third-person verb (`Returns`, `Creates`, `Sets`).
- Events: `Occurs when ...`. Raisers `OnX`: `Raises the <see cref="X"/> event.`
- Hooks (`virtual` / `abstract`): `Called [when]. Override to [purpose].`
- Override of a framework callback: `Called when [event] to [what this override does].`
- Other override with different behavior: full `<summary>` for this type, no "Overrides ...".

## `<param>`, `<typeparam>`, `<returns>`

- Every parameter, including those added in overrides.
- Boolean flag: `When <see langword="true"/>, [effect].` Optional nullable: what replaces `null` (`The field label; <see langword="null"/> for none.`).
- `<typeparam>`: which type is expected, not "The type".
- `<returns>`: success and fallback (`...; otherwise, <see langword="null"/>`). Boolean: `<see langword="true"/> if ...; otherwise, <see langword="false"/>.`
- Fluent method that returns its receiver: `<returns>The element, for chaining.</returns>`.
- `Task<T>` and `ValueTask<T>`: describe the result as for a synchronous method, not "A task that represents...". Non-generic `Task`: no `<returns>`. Deviation from .NET.
- Parameters in text only via `<paramref name="x"/>` / `<typeparamref name="T"/>`.
- `<typeparamref>` works only for type parameters of this member or its declaring type. Name a parent's parameter with `<c>`, otherwise CS1735.

## Constructors

`<summary>` starts with `Creates` and states what this overload sets up (deviation from .NET): `Creates an unbound field without a label.`, `Creates an attribute constrained to a single base type.` No "Initializes a new instance...". Write `<param>` for every parameter.

## `<inheritdoc/>`

- Use it only when nothing is added: explicit interface implementations, overrides exactly per contract, delegating
  `sealed override`, pass-through constructors.
- Write a full block when the behavior differs, or when the constructor changes the effective API (fewer parameters,
  hard-coded arguments).
- `<inheritdoc cref="..."/>` borrows from a non-base member, for example an alias overload with the same parameters.
  `path` is not used.
- Do not combine it with a partial set of `<param>`. Roslyn does not expand `<inheritdoc>`, and CS1573 follows.

## `<remarks>`

- Absent by default.
- Usually one sentence on a non-obvious constraint: conditional compilation, defaults, editor versus player,
  threading, mandatory `base.Method()`, pitfalls.
- Several topics: give each topic its own `<para>`.
- Do not repeat `<exception>`, `<param>` or `<returns>`. No history or motivation.

## `<exception>`

`<exception cref="T">` with the condition only, no "Thrown when": `<paramref name="property"/> is <see langword="null"/>.` When a build symbol changes this behavior, list every case in this order:

1. When it throws.
2. What it does instead, and under which symbol (`<c>UNITY_2020_3_OR_NEWER</c>`).
3. What it logs under `<c>DEBUG</c>`.

## `<example>`

Only on attributes and serializable field types that users declare in their own code. Give one `<code>` per variant: the field declaration with its attribute, a few lines, no prose.

## Structural tags

- `<para>`: paragraphs in `<remarks>`, one topic each. Not in `<summary>`.
- `<list type="bullet">` / `type="table"`: three or more items.
- `<seealso cref="..."/>`: related types not mentioned in the text.

## References and keywords

- C# keywords (`null`, `true`, `false`, `this`, `void`, `default`, `new`, `static`, `abstract`, `sealed`, `async`, `await` and others): `<see langword="..."/>`, not `<c>`.
- Types with keyword aliases (`object`, `string`, `int`): `<see cref="object"/>`, not `langword`.
- `<c>`: names without a cref target, for example literals, file names, build symbols, UI captions (`<c>&lt;None&gt;</c>`), attribute usages (`<c>[SerializeReference]</c>`), command-line flags. Also types this assembly cannot reference, for example `<c>MonoScript</c>` in runtime code.
- Enum members: `<see cref="Enum.Member"/>`. Label `<see cref="X">label</see>` when the name alone is unclear.
- Generics: cref only the open type with its declared parameter names: `<see cref="BaseField{TValueType}"/>`.
  - For a closed type, add the argument with `of`: `<see cref="BaseField{TValueType}"/> of <see cref="int"/>`.
  - A closed type in cref is an error: `<see cref="IBinder{string}"/>` gives CS1584 and CS1658.
  - An identifier in braces silently becomes a parameter name: `<see cref="BaseField{Type}"/>` links to the open `BaseField{TValueType}`.
- Overloads: cref with parameter types.

## Unity: `[Tooltip]`

- Above every serialized field: `[SerializeField]`, `[SerializeReference]` and `public` fields of a serializable type.
- A serialized field that is reachable (see [Scope](#scope)) also gets a `<summary>`. The Tooltip is for the Inspector, the `<summary>` is for the API.
- One or two phrases. A field that holds a value: a noun phrase (`The value returned when no entry matches the lookup key.`). No mechanics, defaults or enum listings.
- A field behavior caveat (clamping, "ignored when", fallback) goes into the Tooltip. Repeat it in the `<param>` of the constructor parameter that sets the field.

## Examples

Type in an inheritance chain, constructor, property with a default:

```csharp
/// <summary>
/// <see cref="BaseField{TValueType}"/> of <see cref="Type"/> for selecting a type.
/// </summary>
public partial class TypeField : BaseField<Type>
{
    /// <summary>
    /// Gets or sets the predicate applied to each candidate after the <see cref="Types"/> and
    /// <see cref="Allow"/> checks. <see langword="null"/> keeps every matching type.
    /// </summary>
    public Func<Type, bool> Predicate { get; set; }

    /// <summary>
    /// Creates a field bound to <paramref name="property"/>, labeled with its display name.
    /// </summary>
    /// <param name="property">A string property holding the assembly-qualified type name.</param>
    public TypeField(SerializedProperty property)
```

Not: `This class provides a field that is used to select a type.` Not: `Initializes a new instance of the <see cref="TypeField"/> class.`

Fluent extension method:

```csharp
/// <summary>
/// Sets the label of the field via <see cref="BaseField{TValueType}.label"/>.
/// </summary>
/// <typeparam name="T">The field type.</typeparam>
/// <param name="element">The element to modify.</param>
/// <param name="value">The label text to set.</param>
/// <returns>The element, for chaining.</returns>
public static T SetLabel<T>(this T element, string value)
```

Hook:

```csharp
/// <summary>
/// Called after binding is established and the first value is applied. Override to subscribe to the component.
/// </summary>
protected virtual void OnBound() { }
```

Exception that depends on build symbols:

```csharp
/// <exception cref="BindSafelyNullReferenceException">
/// Any binder in the array is <see langword="null"/>.
/// In Unity (<c>UNITY_2020_3_OR_NEWER</c>), skips the <see langword="null"/> binder instead of throwing.
/// When <c>DEBUG</c> is also defined, additionally logs an error.
/// </exception>
```

Attribute with `<example>`:

```csharp
/// <summary>
/// Draws the field with the type-selector window.
/// </summary>
/// <example>
/// <code>
/// [TypeSelector(typeof(MonoBehaviour))]
/// [SerializeField] private string _behaviorType;
/// </code>
/// </example>
public sealed class TypeSelectorAttribute : PropertyAttribute
```

Tooltip caveat repeated in the constructor `<param>`:

```csharp
[Tooltip("The volume applied to the AudioSource, clamped to 0..1.")]
[SerializeField] private float _volume;

/// <param name="volume">The volume to apply, clamped to 0..1.</param>
```
