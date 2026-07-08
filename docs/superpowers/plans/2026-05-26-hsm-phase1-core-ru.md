# HSM Фаза 1: План реализации ядра

> **Для агентных воркеров:** ОБЯЗАТЕЛЬНЫЙ ПОД-СКИЛЛ: Используйте superpowers:subagent-driven-development (рекомендуется) или superpowers:executing-plans для реализации этого плана задача за задачей. Шаги используют синтаксис чекбоксов (`- [ ]`) для отслеживания.

**Цель:** Верифицировать и укрепить ядро фреймворка HSM — Controller, ControllerGroup (как самостоятельная переиспользуемая единица), State, иерархия через `[ParentState]`, StateMachineBase, MonoStateMachine и генераторы исходного кода.

**Архитектура:** Большая часть Фазы 1 уже существует в кодовой базе. Основные пробелы: (1) тест генератора, подтверждающий корректную генерацию для вложенных ControllerGroup (ControllerGroup внутри ControllerGroup), (2) рантайм-тест, подтверждающий работу диспатча через вложенные ControllerGroup, (3) пример, демонстрирующий переиспользование автономной ControllerGroup между состояниями. Существующие 68+ тестов и два генератора (ChildStateGenerator, ControllersGroupGenerator) формируют базовую линию.

**Стек технологий:** C# 12, .NET (netstandard2.0 для генератора, net10.0 для тестов), xUnit, инкрементальные генераторы исходного кода Roslyn, Unity 2022.3+

---

**Псевдонимы путей, используемые в этом плане:**

| Псевдоним | Полный путь |
|---|---|
| `$RUNTIME` | `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM` |
| `$GEN` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators` |
| `$TESTS` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests` |
| `$SAMPLE` | `Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample` |

Все пути относительно корня репозитория: `/Users/vladislavpanin/Documents/Docs/Aspid/Packages/Aspid.Core.HSM/Projects/Aspid.Core.HSM/`

---

## Структура файлов

### Существующие файлы (изменения не требуются)

| Файл | Ответственность |
|---|---|
| `$RUNTIME/Source/IController.cs` | Маркерный интерфейс для всех контроллеров |
| `$RUNTIME/Source/IState.cs` | Интерфейс состояния с Enter/Exit |
| `$RUNTIME/Source/IChildState.cs` | Предоставляет тип ParentState для иерархии |
| `$RUNTIME/Source/IStateMachine.cs` | Публичный API машины состояний |
| `$RUNTIME/Source/EmptyState.cs` | Заглушка начального состояния |
| `$RUNTIME/Source/StateFactory.cs` | Абстрактная фабрика, построитель цепочек |
| `$RUNTIME/Source/Extensions/StateExtensions.cs` | Хелпер GetController |
| `$RUNTIME/Source/Extensions/StateMachineExtensions.cs` | GetParentState/GetChildState |
| `$RUNTIME/Source/Generation/ParentStateAttribute.cs` | Триггерит ChildStateGenerator |
| `$RUNTIME/Source/Generation/ControllerGroupAttribute.cs` | Триггерит ControllersGroupGenerator |
| `$RUNTIME/Source/Generation/ReverseExecuteAttribute.cs` | Помечает методы с обратным порядком выполнения |
| `$RUNTIME/Unity/Runtime/Controllers/*.cs` | Все 9 интерфейсов контроллеров |
| `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` | Логика ядра машины состояний |
| `$RUNTIME/Unity/Runtime/StateMachines/MonoStateMachine.cs` | Обёртка Unity MonoBehaviour |
| `$GEN/ChildState/**` | ChildStateGenerator + данные + тело |
| `$GEN/ControllerGroup/**` | ControllersGroupGenerator + данные + фабрики + тело |
| `$TESTS/StateMachineTests/TestStates.cs` | Тестовая иерархия состояний |
| `$TESTS/StateMachineTests/TestStateFactory.cs` | Тестовая реализация фабрики |
| `$TESTS/StateMachineTests/TestableStateMachine.cs` | Тестируемый подкласс StateMachineBase |
| `$TESTS/StateMachineTests/StateMachineBaseTests.cs` | 30+ существующих тестов |

### Файлы для создания

| Файл | Ответственность |
|---|---|
| `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs` | Верификация корректной генерации кода для вложенных ControllerGroup |
| `$TESTS/StateMachineTests/NestedControllerGroupTests.cs` | Верификация рантайм-диспатча через вложенные группы контроллеров |
| `$SAMPLE/Sample/Controllers/InputControllerGroup.cs` | Пример автономной ControllerGroup (переиспользуемой между состояниями) |

---

### Задача 1: Верификация базовой линии — все тесты проходят

**Файлы:**
- Прочитать: `$TESTS/Aspid.Core.HSM.Generators.Tests.csproj`

- [ ] **Шаг 1: Собрать решение**

Запуск из `Aspid.Core.HSM.Generators/`:
```bash
dotnet build Aspid.Core.HSM.Generators.slnx
```
Ожидаемый результат: Build succeeded.

- [ ] **Шаг 2: Запустить все существующие тесты**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```
Ожидаемый результат: Все тесты проходят. Если какие-то падают, исправить их перед продолжением.

- [ ] **Шаг 3: Коммит верификации базовой линии**

Изменения кода не нужны — это просто контрольная точка. Если потребовались исправления:
```bash
git add -A
git commit -m "fix: resolve baseline test failures"
```

---

### Задача 2: Тест генератора — генерация для вложенных ControllerGroup

Этот тест верифицирует, что когда класс с `[ControllerGroup]` агрегирует другой класс с `[ControllerGroup]`, внешняя группа генерирует корректный код делегирования.

**Файлы:**
- Создать: `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs`

- [ ] **Шаг 1: Написать тест генерации**

Создать `$TESTS/GeneratorTests/ControllerGroupNestingEmitTests.cs`:

```csharp
using Aspid.Core.HSM.Generators.ControllerGroup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.GeneratorTests;

public class ControllerGroupNestingEmitTests
{
    [Fact]
    public void Outer_group_delegates_to_inner_group_that_implements_interface()
    {
        var source = """
            using Aspid.Core.HSM;

            namespace Test;

            public interface IMyController : IController
            {
                void DoWork();
            }

            public class LeafController : IMyController
            {
                public void DoWork() { }
            }

            [ControllerGroup]
            public partial class InnerGroup : IMyController
            {
                public InnerGroup()
                {
                    AddControllers(new LeafController());
                }
            }

            [ControllerGroup]
            public partial class OuterGroup : IMyController
            {
                public OuterGroup()
                {
                    AddControllers(new InnerGroup());
                }
            }
            """;

        var generated = RunGenerator(source, "OuterGroup");

        Assert.Contains("void IMyController.DoWork()", generated);
        Assert.Contains("((IMyController)__controller0).DoWork()", generated);
    }

    [Fact]
    public void Outer_group_with_mixed_inner_group_and_leaf_delegates_to_both()
    {
        var source = """
            using Aspid.Core.HSM;

            namespace Test;

            public interface IMyController : IController
            {
                void DoWork();
            }

            public class LeafController : IMyController
            {
                public void DoWork() { }
            }

            [ControllerGroup]
            public partial class InnerGroup : IMyController
            {
                public InnerGroup()
                {
                    AddControllers(new LeafController());
                }
            }

            [ControllerGroup]
            public partial class OuterGroup : IMyController
            {
                public OuterGroup()
                {
                    AddControllers(new InnerGroup(), new LeafController());
                }
            }
            """;

        var generated = RunGenerator(source, "OuterGroup");

        Assert.Contains("((IMyController)__controller0).DoWork()", generated);
        Assert.Contains("((IMyController)__controller1).DoWork()", generated);
    }

    private static string RunGenerator(string source, string targetClassName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ControllersGroupGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var results = driver.GetRunResult();
        var targetResult = results.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(targetClassName));

        Assert.NotNull(targetResult);
        return targetResult!.GetText().ToString();
    }
}
```

- [ ] **Шаг 2: Запустить тест для верификации**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~ControllerGroupNestingEmitTests -v normal
```
Ожидаемый результат: PASS. Генератор уже обрабатывает вложенные группы, потому что трактует любой класс с `[ControllerGroup]` так же, как обычный контроллер — ему достаточно реализовать интерфейс в объявлении типа.

Если тест ПАДАЕТ (например, генератор не видит InnerGroup как реализующий IMyController), исправление в `ControllerInterfaceDataFactory.Create()` — убедиться, что интерфейсы разрешаются из семантической модели, которая включает сгенерированные partial-объявления.

- [ ] **Шаг 3: Коммит**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/GeneratorTests/ControllerGroupNestingEmitTests.cs
git commit -m "test: add generator emit tests for nested ControllerGroups"
```

---

### Задача 3: Рантайм-тест — диспатч через вложенные ControllerGroup

Этот тест верифицирует, что в рантайме диспатч Update/Enter через состояние, содержащее вложенную ControllerGroup, вызывает все листовые контроллеры.

**Файлы:**
- Создать: `$TESTS/StateMachineTests/NestedControllerGroupTests.cs`

- [ ] **Шаг 1: Написать тест рантайм-диспатча**

Создать `$TESTS/StateMachineTests/NestedControllerGroupTests.cs`:

```csharp
using Xunit;

namespace Aspid.Core.HSM.Generators.Tests.StateMachineTests;

public class InnerGroupController : IEnterController, IUpdateController
{
    public int OnEnterCalled { get; private set; }
    public int UpdateCallCount { get; private set; }
    public float LastDeltaTime { get; private set; }

    public void OnEnter() => OnEnterCalled++;

    public void Update(float deltaTime)
    {
        UpdateCallCount++;
        LastDeltaTime = deltaTime;
    }
}

public class OuterGroupState : BaseTestState, IEnterController, IUpdateController
{
    public InnerGroupController Inner { get; } = new();

    public void OnEnter() => Inner.OnEnter();

    public void Update(float deltaTime) => Inner.Update(deltaTime);
}

public class NestedControllerGroupTests
{
    [Fact]
    public void Update_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();
        sm.CallUpdate(0.016f);

        Assert.Equal(1, state.Inner.UpdateCallCount);
        Assert.Equal(0.016f, state.Inner.LastDeltaTime);
    }

    [Fact]
    public void Enter_dispatches_through_nested_controller_group()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();

        Assert.Equal(1, state.Inner.OnEnterCalled);
    }

    [Fact]
    public void Multiple_updates_dispatch_correctly_through_nesting()
    {
        var factory = new TestStateFactory();
        var state = new OuterGroupState();
        factory.RegisterState(() => state);
        var sm = new TestableStateMachine(factory);

        sm.ChangeState<OuterGroupState>();
        sm.CallUpdate(0.016f);
        sm.CallUpdate(0.033f);
        sm.CallUpdate(0.016f);

        Assert.Equal(3, state.Inner.UpdateCallCount);
        Assert.Equal(0.016f, state.Inner.LastDeltaTime);
    }
}
```

- [ ] **Шаг 2: Запустить тесты для верификации**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj --filter FullyQualifiedName~NestedControllerGroupTests -v normal
```
Ожидаемый результат: Все 3 теста PASS.

- [ ] **Шаг 3: Коммит**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/StateMachineTests/NestedControllerGroupTests.cs
git commit -m "test: add runtime dispatch tests for nested ControllerGroups"
```

---

### Задача 4: Пример — автономная ControllerGroup, переиспользуемая между состояниями

Добавить пример, показывающий автономную `InputControllerGroup`, используемую в нескольких состояниях (пример из раздела 1.3 спецификации).

**Файлы:**
- Создать: `$SAMPLE/Sample/Controllers/InputControllerGroup.cs`
- Изменить: `$SAMPLE/Sample/States/Singleplayers/SingleplayerState.cs`
- Изменить: `$SAMPLE/Sample/States/Multiplayers/MultiplayerState.cs`

- [ ] **Шаг 1: Создать InputControllerGroup**

Создать `$SAMPLE/Sample/Controllers/InputControllerGroup.cs`:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class InputControllerGroup : IEnterController, IUpdateController
{
    public InputControllerGroup()
    {
        AddControllers(new SomeUpdateController(), new SomeUpdateController());
    }
}
```

- [ ] **Шаг 2: Обновить SingleplayerState для использования InputControllerGroup**

В `$SAMPLE/Sample/States/Singleplayers/SingleplayerState.cs` обновить конструктор:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class SingleplayerState : IState
{
    public SingleplayerState()
    {
        AddControllers(new PlayerController(), new InputControllerGroup());
    }
}
```

- [ ] **Шаг 3: Обновить MultiplayerState для использования InputControllerGroup**

В `$SAMPLE/Sample/States/Multiplayers/MultiplayerState.cs` обновить конструктор:

```csharp
using Aspid.Core.HSM;

namespace Aspid.Core.HSM.Generators.Sample.Sample;

[ControllerGroup]
public partial class MultiplayerState : IState
{
    public MultiplayerState()
    {
        AddControllers(new PlayerController(), new SomeUpdateController(), new InputControllerGroup());
    }

    public void Enter()
    {
    }

    public void Exit()
    {
    }
}
```

- [ ] **Шаг 4: Собрать для проверки работы генератора с примерами**

```bash
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.slnx
```
Ожидаемый результат: Build succeeded. Генератор должен сгенерировать корректный код для обоих состояний, которые теперь включают `InputControllerGroup`.

- [ ] **Шаг 5: Коммит**

```bash
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/Controllers/InputControllerGroup.cs
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/States/Singleplayers/SingleplayerState.cs
git add Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Sample/Sample/States/Multiplayers/MultiplayerState.cs
git commit -m "feat: add InputControllerGroup sample showing reusable group across states"
```

---

### Задача 5: Финальная верификация — все тесты проходят

**Файлы:** Нет (только верификация)

- [ ] **Шаг 1: Запустить полный набор тестов**

```bash
dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests/Aspid.Core.HSM.Generators.Tests.csproj -v normal
```
Ожидаемый результат: Все тесты проходят (существующие 68+ плюс новые из Задач 2 и 3).

- [ ] **Шаг 2: Собрать полное решение**

```bash
dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.slnx
```
Ожидаемый результат: Чистая сборка, без предупреждений из нашего кода.

---

## Наброски Фаз 2–7

### Фаза 2: AsyncMode для каждого интерфейса

**Что существует:** `IAsyncEnterController`, `IAsyncExitController`, `[AsyncOf]`, `ChangeStateAsync`, генератор обрабатывает смешанный синхронный/асинхронный диспатч.

**Что нового:**
1. `AsyncModeAttribute` — новый атрибут: `[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]`
2. Перечисление `AsyncExecutionMode` — `Sequential`, `Parallel`
3. Обновление генератора — `ControllerGroupBody.AppendAsyncInterfaceMethod()` считывает `[AsyncMode]` из класса и генерирует либо последовательный `await`, либо `UniTask.WhenAll()`
4. Тест генератора — верификация генерации Sequential vs Parallel
5. Рантайм-тест — верификация параллельного диспатча

**Ключевые файлы:**
- Создать: `$RUNTIME/Source/Generation/AsyncModeAttribute.cs`
- Создать: `$RUNTIME/Source/Generation/AsyncExecutionMode.cs`
- Изменить: `$GEN/ControllerGroup/Factories/ControllerInterfaceDataFactory.cs` — чтение атрибута `[AsyncMode]`
- Изменить: `$GEN/ControllerGroup/Bodies/ControllerGroupBody.cs` — условная генерация Sequential/Parallel
- Создать: `$TESTS/GeneratorTests/AsyncModeEmitTests.cs`
- Создать: `$TESTS/StateMachineTests/AsyncModeRuntimeTests.cs`

---

### Фаза 3: Пайплайн переходов

**Что существует:** Ничего — это полностью новая функциональность.

**Что нового:**
1. Интерфейсы `ITransition` / `ITransition<TSource, TTarget>` (типы Source/Target, гард `CanTransition()`)
2. `TransitionAttribute` — `[Transition(typeof(Source), typeof(Target))]`
3. `TransitionGenerator` — генерирует свойства `ITransition.SourceState` / `TargetState`
4. `TransitionRegistry` — хранит зарегистрированные переходы по ключу (source, target)
5. Обновление `StateMachineBase` — методы `TransitionTo<T>()` и `TransitionVia<T>()`, выполнение пайплайна
6. Разрешение переходов — прямой поиск, затем цепочка как фоллбэк с предварительной проверкой гардов
7. `TransitionState` — выполняется в отдельном облегчённом контексте выполнения
8. Асинхронный пайплайн — `OnBeforeTransitionAsync`, выход из source, вход в target, `OnAfterTransitionAsync`
9. Обновление `MonoStateMachine` — предоставить `TransitionTo`/`TransitionVia`
10. Обратная совместимость — сохранить `ChangeState<T>()` как низкоуровневый API, `TransitionTo<T>()` использует пайплайн

**Ключевые файлы:**
- Создать: `$RUNTIME/Source/ITransition.cs`
- Создать: `$RUNTIME/Source/Generation/TransitionAttribute.cs`
- Создать: `$GEN/Transition/TransitionGenerator.cs` (+ Data, Body, Factory)
- Изменить: `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` — добавить реестр переходов, выполнение пайплайна
- Изменить: `$RUNTIME/Source/IStateMachine.cs` — добавить `TransitionTo`, `TransitionVia`, `IsTransitioning`
- Создать: `$TESTS/StateMachineTests/TransitionPipelineTests.cs`
- Создать: `$TESTS/GeneratorTests/TransitionEmitTests.cs`

---

### Фаза 4: Extension State

**Что существует:** Ничего — это полностью новая функциональность.

**Что нового:**
1. Интерфейс `IExtensionState` — `CanAttachTo`, `OnAttached`, `OnDetached`
2. `ExtensionForAttribute` — `[ExtensionFor(typeof(State1), typeof(State2))]`
3. `ExtensionStateGenerator` — генерирует реализацию `CanAttachTo`
4. Обновление `StateMachineBase` — `AttachExtension<T>()`, `DetachExtension<T>()`, список `ActiveExtensions`
5. Автоматическое отсоединение при несовместимом переходе
6. Порядок диспатча контроллеров — сначала состояния-хосты, затем расширения в порядке присоединения

**Ключевые файлы:**
- Создать: `$RUNTIME/Source/IExtensionState.cs`
- Создать: `$RUNTIME/Source/Generation/ExtensionForAttribute.cs`
- Создать: `$GEN/ExtensionState/ExtensionStateGenerator.cs` (+ Data, Body, Factory)
- Изменить: `$RUNTIME/Unity/Runtime/StateMachines/StateMachineBase.cs` — управление расширениями
- Изменить: `$RUNTIME/Source/IStateMachine.cs` — добавить API расширений
- Создать: `$TESTS/StateMachineTests/ExtensionStateTests.cs`
- Создать: `$TESTS/GeneratorTests/ExtensionStateEmitTests.cs`

---

### Фаза 5: Скоупы + VContainer

**Что существует:** `StateFactory` с `CreateStateInternal` / `Release` / `MarkInitialized`.

**Что нового:**
1. Интерфейс `IStateScope` в ядре — `Parent`, `CreateChildScope()`, `IDisposable`
2. `ScopeLifetimeAttribute` — `[ScopeLifetime(ScopeLifetime.Cached)]`
3. Перечисление `ScopeLifetime` — `Transient`, `Cached`
4. Обновление `StateFactory` — интеграция создания/кэширования скоупов в `CreateState`
5. Отдельный пакет `Aspid.Core.HSM.VContainer` — `VContainerStateFactory`, `VContainerStateScope`
6. Скоупы Extension State — дочерние от скоупа состояния-хоста

**Ключевые файлы:**
- Создать: `$RUNTIME/Source/IStateScope.cs`
- Создать: `$RUNTIME/Source/Generation/ScopeLifetimeAttribute.cs`
- Изменить: `$RUNTIME/Source/StateFactory.cs` — поддержка скоупов
- Создать новый пакет: `Aspid.Core.HSM.VContainer/`
- Создать: `$TESTS/StateMachineTests/ScopeLifecycleTests.cs`

---

### Фаза 6: Интеграции и полировка

1. Пакет `Aspid.Core.HSM.MVVM` — `ViewLifecycleController<TView, TViewModel>`
2. Диагностика на этапе компиляции в генераторах (циклическая иерархия, невалидные ссылки `[ParentState]`)
3. Заглушки точек расширения — `IsControllerEnabled()`, `IsStateEnabled()`, `ResolveTransition()`
4. Обновлённые примеры и документация

---

### Фаза 7: Инструментарий Claude (отдельная спецификация)

Отдельная спецификация дизайна, покрывающая:
1. Скиллы Claude Code для скаффолдинга HSM
2. MCP-сервер для интроспекции дерева HSM
3. Агенты для миграции FSM→HSM
4. Гайдлайны для XML-документации и организации CLAUDE.md в потребительских проектах
