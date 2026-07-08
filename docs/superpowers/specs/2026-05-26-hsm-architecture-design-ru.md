# Aspid.Core.HSM — Архитектура иерархической машины состояний

## Обзор

Aspid.Core.HSM — это фреймворк иерархической машины состояний для игровой архитектуры. Он предоставляет компонуемую, типобезопасную систему для управления игровыми состояниями через контроллеры, состояния, переходы и скоупы. Ядро фреймворка платформо-независимо, с отдельным слоем интеграции для Unity.

### Цели

- **Компонуемость**: Контроллеры как мельчайшие единицы, группируемые в ControllerGroup, из которых составляются состояния
- **Иерархические состояния**: Отношения «родитель-потомок», при которых поведение родителя сохраняется во время выполнения потомка
- **Динамическое расширение**: Extension State, наслаивающие поведение на состояние-хост в рантайме
- **Безопасные переходы**: Полноценный пайплайн с гардами, асинхронной поддержкой и опциональными визуальными состояниями перехода
- **DI-агностичные скоупы**: Каждое состояние владеет своим скоупом; интеграция через адаптеры (VContainer)
- **Конфигурируемость**: Точки расширения для отключения контроллеров/состояний, замены переходов (реализация отложена)
- **Claude-first DX**: Спроектировано для AI-ассистированной разработки с инструментарием Claude (отдельная спецификация)

### Не-цели (данной спецификации)

- Детальный дизайн инструментария Claude (отдельная спецификация после Фазы 6)
- Реализация системы конфигурации (только точки расширения)
- Инструментарий редактора (инспектор, визуализатор отладки)

---

## 1. Иерархия типов: Controller → ControllerGroup → State

### 1.1 Controller

Наименьшая единица логики. Контроллер — это класс, реализующий один или несколько интерфейсов контроллера. Контроллеры ничего не знают о машине состояний или состояниях — они содержат только свою доменную логику.

```csharp
public interface IController { }

public class PlayerMovementController : IUpdateController, IEnterController
{
    void IEnterController.OnEnter() { /* инициализация ввода */ }
    void IUpdateController.Update(float dt) { /* перемещение игрока */ }
}
```

### 1.2 Интерфейсы контроллеров

Контроллеры реализуют интерфейсы жизненного цикла, вызываемые машиной состояний:

| Интерфейс | Метод | Когда вызывается | Порядок |
|---|---|---|---|
| `IEnterController` | `OnEnter()` | Вход в состояние | Прямой |
| `IExitController` | `OnExit()` | Выход из состояния | Обратный |
| `IUpdateController` | `Update(float)` | Unity Update() | Прямой |
| `ILateUpdateController` | `LateUpdate(float)` | Unity LateUpdate() | Прямой |
| `IFixedUpdateController` | `FixedUpdate(float)` | Unity FixedUpdate() | Прямой |
| `IDisposableController` | `Dispose()` | Уничтожение машины состояний | Обратный |
| `IAsyncEnterController` | `OnEnterAsync(CancellationToken)` | Асинхронный вход в состояние | Прямой |
| `IAsyncExitController` | `OnExitAsync(CancellationToken)` | Асинхронный выход из состояния | Обратный |

Асинхронные интерфейсы спарены с синхронными через `[AsyncOf(typeof(ISyncInterface))]`. Ядро использует `Task`/`ValueTask`; Unity-слой — `UniTask`.

### 1.3 ControllerGroup

Составной контроллер (паттерн Composite). Сам является `IController` и агрегирует дочерние контроллеры и/или другие ControllerGroup. Генератор `[ControllerGroup]` реализует все интерфейсы контроллеров от дочерних элементов, делегируя вызовы.

```csharp
[ControllerGroup]
public partial class InputControllerGroup : IController
{
    public InputControllerGroup()
    {
        AddControllers(
            new InputReadController(),
            new InputMappingController()
        );
    }
}
```

ControllerGroup могут быть вложенными. ControllerGroup внутри другой ControllerGroup или State рассматривается как единый контроллер с точки зрения родителя.

### 1.4 State

State — это ControllerGroup с дополнительной семантикой:

- `Enter()` / `Exit()` вызываются машиной состояний во время переходов
- Родительское состояние объявляется через `[ParentState(typeof(ParentType))]`
- Единица скоупа (для DI, конфигурации)
- Может содержать отдельные контроллеры и ControllerGroup

```csharp
[ControllerGroup]
[ParentState(typeof(GameplayState))]
public partial class FreerideState : IState
{
    public FreerideState()
    {
        AddControllers(
            new PlayerMovementController(),
            new InputControllerGroup(),   // переиспользуемая группа
            new FreerideHudController()
        );
    }
}
```

### 1.5 Дерево состояний

Дерево формируется статически при запуске через атрибуты `[ParentState]`. При переходе от родителя к потомку родитель НЕ выходит — его контроллеры продолжают работать.

```
RootState
├── MainMenuState
├── GameplayState
│   ├── FreerideState
│   │   └── FreerideInventoryState
│   └── RaceState
│       └── RaceResultState
└── SettingsState
```

---

## 2. Пайплайн переходов

### 2.1 Сущность перехода

Transition описывает, как осуществить переход между состояниями. Каждый Transition содержит:

- **Source/Target** — исходное и целевое типы состояний
- **Guards** — условия, которые должны быть выполнены (`CanTransition()`)
- **Pipeline** — последовательность действий во время перехода
- **Опциональный TransitionState** — для визуально тяжёлых переходов (загрузка, затемнение)

```csharp
public interface ITransition
{
    Type SourceState { get; }
    Type TargetState { get; }
    bool CanTransition();
}

public interface ITransition<TSource, TTarget> : ITransition
    where TSource : IState
    where TTarget : IState
{ }
```

### 2.2 Порядок выполнения пайплайна

```
1. CanTransition() → false? → отмена
2. OnBeforeTransition()
3. [Если существует TransitionState] → Вход в TransitionState
4. Выход из Source-состояний (обратный порядок, от листа к общему предку)
5. [Асинхронная точка — загрузка сцены, ресурсов и т.д.]
6. Вход в Target-состояния (прямой порядок, от общего предка к листу)
7. [Если существует TransitionState] → Выход из TransitionState
8. OnAfterTransition()
```

### 2.3 TransitionState

Опциональное состояние, существующее между выходом из Source и входом в Target. Используется для экранов загрузки, эффектов затемнения, промежуточных анимаций.

```csharp
[ControllerGroup]
public partial class LoadingTransitionState : IState
{
    public LoadingTransitionState()
    {
        AddControllers(
            new LoadingScreenController(),
            new ProgressBarController()
        );
    }
}
```

### 2.4 Разрешение переходов (прямой vs цепочка)

Когда запрашивается переход, машина состояний разрешает его с приоритетом:

1. **Прямой переход**: Если зарегистрирован конкретный `ITransition<Source, Target>`, используется он. Один Transition, один пайплайн, один TransitionState (например, один экран загрузки вместо трёх).

2. **Цепочка как фоллбэк**: Если прямой Transition не зарегистрирован, составляется цепочка переходов вдоль пути по дереву состояний. Каждый сегмент использует свой зарегистрированный Transition (или Transition по умолчанию без действий). Все гарды в цепочке должны пройти до начала какого-либо Exit — если любой гард не пройдёт, весь переход отменяется.

Это позволяет определять кастомные переходы «большого прыжка» с единым экраном загрузки, сохраняя автоматическое разрешение для простых переходов.

### 2.5 Регистрация

```csharp
public class GameStateMachine : StateMachineBase
{
    protected override void RegisterTransitions()
    {
        Register<MainMenuState, GameplayState>(
            new MainMenuToGameplayTransition(
                transitionState: new LoadingTransitionState()
            )
        );
        Register<FreerideState, RaceState>(new FreerideToRaceTransition());
    }
}
```

### 2.6 Вызов

```csharp
// Машина состояний находит зарегистрированный Transition от текущего состояния к RaceState
stateMachine.TransitionTo<RaceState>();

// Или указать конкретный Transition (когда существует несколько)
stateMachine.TransitionVia<FreerideToRaceTransition>();
```

---

## 3. Extension State

### 3.1 Концепция

Extension State динамически присоединяется к состоянию-хосту, расширяя его поведение. Состояние-хост продолжает работать; Extension State добавляет свои контроллеры.

Ключевые свойства:
- **Не самостоятельный** — может существовать только при присоединении к состоянию-хосту
- **Динамический** — присоединяется/отсоединяется в рантайме, не является частью статического дерева
- **Ограниченный** — объявляет, к каким состояниям может присоединяться
- **Стекируемый** — несколько Extension State могут быть присоединены к одному хосту одновременно

### 3.2 Интерфейс

```csharp
public interface IExtensionState : IState
{
    bool CanAttachTo(IState hostState);
    void OnAttached(IState hostState);
    void OnDetached(IState hostState);
}
```

### 3.3 Ограничения совместимости

Через атрибут + генерируемый `CanAttachTo`:

```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
[ControllerGroup]
public partial class MiniGameExtensionState : IExtensionState
{
    public MiniGameExtensionState()
    {
        AddControllers(
            new MiniGameTimerController(),
            new MiniGameHudController()
        );
    }
    
    // Генерируется [ExtensionFor]:
    // bool IExtensionState.CanAttachTo(IState host) =>
    //     host is FreerideState or RaceState;
}
```

Кастомная логика может быть добавлена ручной реализацией `CanAttachTo` (генератор пропускает, если уже реализовано).

### 3.4 Жизненный цикл

```
Состояние-хост активно
    ↓
stateMachine.AttachExtension<MiniGameExtensionState>()
    ↓
CanAttachTo(currentState) → true?
    ↓
MiniGameExtensionState.Enter()
Контроллеры начинают получать Update/LateUpdate/и т.д.
    ↓
... время проходит ...
    ↓
stateMachine.DetachExtension<MiniGameExtensionState>()
    ↓
MiniGameExtensionState.Exit()
Контроллеры останавливаются
```

### 3.5 Поведение при смене состояния-хоста

Когда состояние-хост меняется через переход, Extension State проверяются:

- Если новое состояние совместимо (`CanAttachTo` → true) → Extension остаётся присоединённым
- Если несовместимо → автоматическое отсоединение (`Exit()` вызывается до `Exit()` хоста)

```
Freeride (+ MiniGameExtension) → Race
MiniGameExtension.CanAttachTo(RaceState) → true → остаётся присоединённым

Freeride (+ MiniGameExtension) → MainMenu
MiniGameExtension.CanAttachTo(MainMenuState) → false → автоматическое отсоединение
```

### 3.6 Порядок диспатча контроллеров

```
Update():
  1. Контроллеры состояния-хоста (в порядке AddControllers)
  2. Контроллеры Extension State 1
  3. Контроллеры Extension State 2
  ... (в порядке присоединения)
```

---

## 4. Асинхронная поддержка

### 4.1 Два режима

Асинхронный диспатч контроллеров поддерживает два режима выполнения:

- **Последовательный** — контроллеры выполняются один за другим, каждый ожидает предыдущий
- **Параллельный** — все контроллеры запускаются одновременно, ожидание через `WhenAll`

### 4.2 AsyncMode для каждого интерфейса

Режим настраивается для каждого интерфейса контроллера, а не для каждого State:

```csharp
[ControllerGroup]
[AsyncMode(typeof(IAsyncEnterController), AsyncExecutionMode.Sequential)]
[AsyncMode(typeof(IAsyncExitController), AsyncExecutionMode.Parallel)]
public partial class GameplayState : IState { ... }
```

Если не указано, по умолчанию — **Параллельный** (контроллеры не должны зависеть от порядка инициализации друг друга).

### 4.3 Генерируемый код

```csharp
// Последовательный Enter:
async UniTask IAsyncEnterController.OnEnterAsync(CancellationToken ct)
{
    await __controller0.OnEnterAsync(ct);
    await __controller1.OnEnterAsync(ct);
}

// Параллельный Exit:
async UniTask IAsyncExitController.OnExitAsync(CancellationToken ct)
{
    await UniTask.WhenAll(
        __controller0.OnExitAsync(ct),
        __controller1.OnExitAsync(ct)
    );
}
```

### 4.4 Смешанные синхронные/асинхронные контроллеры

State может содержать как синхронные, так и асинхронные контроллеры. Во время асинхронного диспатча:
- Если контроллер реализует асинхронный интерфейс → await
- Если реализует только синхронный интерфейс → вызов синхронного метода (без await)

Это поведение фоллбэка уже реализовано в текущем генераторе.

### 4.5 Асинхронность в пайплайне переходов

Пайплайн переходов асинхронно-осведомлён:

```
1. CanTransition() → синхронный гард
2. await OnBeforeTransitionAsync(ct)
3. [Если TransitionState] → Вход в TransitionState
4. await Выход из Source (асинхронные контроллеры в настроенном режиме)
5. await [Кастомная асинхронная логика — загрузка сцен, ресурсов]
6. await Вход в Target (асинхронные контроллеры в настроенном режиме)
7. [Если TransitionState] → Выход из TransitionState
8. await OnAfterTransitionAsync(ct)
```

### 4.6 CancellationToken

- Реентрантные асинхронные переходы отменяют предыдущий
- Уничтожение машины состояний отменяет все ожидающие операции
- TransitionState может предоставить UI отмены через токен

### 4.7 Ядро vs Unity

Ядро (без зависимостей от Unity): асинхронные интерфейсы используют `Task`/`ValueTask`.
Unity-слой: предоставляет `UniTask`-варианты через спаривание атрибутом `[AsyncOf]`.

---

## 5. Машина состояний

### 5.1 Обязанности

1. **Управление деревом состояний** — поддерживает текущую иерархию активных состояний
2. **Диспатч контроллеров** — вызывает Update/LateUpdate/FixedUpdate на активных контроллерах
3. **Управление переходами** — хранит реестр переходов, выполняет пайплайн
4. **Управление Extension State** — управляет присоединёнными расширениями
5. **Точки расширения** — виртуальные хуки для будущей системы конфигурации

### 5.2 Интерфейс

```csharp
public interface IStateMachine
{
    IReadOnlyList<IState> CurrentStates { get; }
    IReadOnlyList<IExtensionState> ActiveExtensions { get; }
    
    // Переход по целевому состоянию — ищет зарегистрированный переход от текущего состояния
    void TransitionTo<TTarget>() where TTarget : IState;
    
    // Переход по явному типу перехода — использует конкретный зарегистрированный переход
    void TransitionVia<TTransition>() where TTransition : ITransition;
    
    void AttachExtension<T>() where T : IExtensionState;
    void DetachExtension<T>() where T : IExtensionState;
    
    bool IsTransitioning { get; }
}
```

### 5.3 Диспатч контроллеров

| Интерфейс | Жизненный цикл Unity |
|---|---|
| `IEnterController` | При входе в состояние |
| `IExitController` | При выходе из состояния |
| `IUpdateController` | `MonoBehaviour.Update()` |
| `ILateUpdateController` | `MonoBehaviour.LateUpdate()` |
| `IFixedUpdateController` | `MonoBehaviour.FixedUpdate()` |
| `IDisposableController` | При уничтожении машины состояний |
| Асинхронные варианты | Аналогично, с await |

Порядок: все состояния в иерархии (от корня к листу), затем Extension State. Внутри каждого состояния — в порядке AddControllers.

### 5.4 Точки расширения (реализация отложена)

```csharp
protected virtual bool IsControllerEnabled(IController controller, IState state) => true;
protected virtual bool IsStateEnabled(Type stateType) => true;
protected virtual ITransition? ResolveTransition(Type source, Type target) => /* поиск в реестре */;
```

Сейчас возвращают значения по умолчанию, но позволяют подключить систему конфигурации без изменения ядра.

---

## 6. Скоупы и интеграция с DI

### 6.1 Концепция

Каждое состояние создаёт собственный скоуп. Дочерние состояния наследуют зависимости родительского скоупа. При выходе из состояния скоуп уничтожается.

```
Скоуп RootState
    ├── Скоуп GameplayState (наследует Root)
    │   ├── Скоуп FreerideState (наследует Gameplay)
    │   └── Скоуп RaceState (наследует Gameplay)
    └── Скоуп MainMenuState (наследует Root)
```

### 6.2 Абстракция ядра (DI-агностичная)

```csharp
public interface IStateScope : IDisposable
{
    IStateScope? Parent { get; }
    IStateScope CreateChildScope();
}

public abstract class StateFactory
{
    protected abstract IStateScope CreateScope(IStateScope? parentScope, Type stateType);
    protected abstract IState CreateStateInstance(IStateScope scope, Type stateType);
}
```

### 6.3 Политика времени жизни скоупа

Состояния могут настраивать кэширование скоупа для избежания аллокаций при часто посещаемых состояниях:

```csharp
public enum ScopeLifetime
{
    Transient,   // По умолчанию — скоуп создаётся при Enter, уничтожается при Exit
    Cached       // Скоуп создаётся один раз, переиспользуется при повторных Enter
}

[ScopeLifetime(ScopeLifetime.Cached)]
[ControllerGroup]
public partial class GlobalMapState : IState { ... }
```

**Кэшированный скоуп**: Создаётся при первом Enter, не уничтожается при Exit. Переиспользуется при повторных Enter. Контроллеры по-прежнему получают OnEnter/OnExit. Уничтожается только при уничтожении машины состояний.

**Transient-скоуп** (по умолчанию): Стандартное поведение — создание и уничтожение при каждом переходе.

### 6.4 Интеграция с VContainer

Отдельный пакет: `Aspid.Core.HSM.VContainer`

```csharp
public class VContainerStateFactory : StateFactory
{
    private readonly LifetimeScope _rootScope;
    
    protected override IStateScope CreateScope(IStateScope? parentScope, Type stateType)
    {
        var parent = parentScope as VContainerStateScope;
        var childScope = parent?.LifetimeScope.CreateChild(builder =>
        {
            RegisterStateDependencies(builder, stateType);
        });
        return new VContainerStateScope(childScope);
    }
}
```

### 6.5 Скоупы Extension State

Extension State получают дочерний скоуп от состояния-хоста. Они регистрируют свои зависимости, но не создают отдельного уровня иерархии.

```
Скоуп FreerideState
    ├── Скоуп MiniGameExtension (потомок Freeride)
    └── Скоуп ChatExtension (потомок Freeride)
```

---

## 7. Генераторы исходного кода

### 7.1 Существующие генераторы (расширенные)

**ChildStateGenerator**: Генерирует `IChildState.ParentState` из атрибута `[ParentState]`. Изменения не требуются.

**ControllersGroupGenerator**: Расширен для:
- Вложенных ControllerGroup (ControllerGroup внутри ControllerGroup)
- `[AsyncMode]` для каждого интерфейса (последовательный/параллельный диспатч)
- Смешанного синхронного/асинхронного диспатча контроллеров (уже реализовано)
- Маркеров профайлера (уже реализовано)

### 7.2 Новые генераторы

**ExtensionStateGenerator**: Для атрибута `[ExtensionFor]`:
```csharp
[ExtensionFor(typeof(FreerideState), typeof(RaceState))]
public partial class MiniGameExtensionState : IExtensionState { ... }

// Генерирует:
bool IExtensionState.CanAttachTo(IState host) =>
    host is FreerideState or RaceState;
```

**TransitionGenerator**: Для декларативных переходов:
```csharp
[Transition(typeof(FreerideState), typeof(RaceState))]
public partial class FreerideToRaceTransition : ITransition { ... }

// Генерирует:
Type ITransition.SourceState => typeof(FreerideState);
Type ITransition.TargetState => typeof(RaceState);
```

### 7.3 Диагностика на этапе компиляции

Генераторы выдают предупреждения/ошибки для:
- `[ParentState]`, ссылающегося на несуществующее состояние
- `[ExtensionFor]`, ссылающегося на несуществующее состояние
- Циклических зависимостей в иерархии состояний
- Контроллера, реализующего асинхронный интерфейс, но состояние без `[AsyncMode]` для него

---

## 8. Интеграции

### 8.1 Aspid.MVVM

HSM управляет игровым состоянием → MVVM управляет представлением внутри этого состояния.

**Точки интеграции:**

1. **ViewModel как контроллер** — ViewModel реализует интерфейсы контроллера, получает жизненный цикл:
```csharp
[ViewModel]
public partial class FreerideHudViewModel : IEnterController, IUpdateController
{
    [Bind] private int _score;
    void IEnterController.OnEnter() { /* инициализация */ }
    void IUpdateController.Update(float dt) { Score = ...; }
}
```

2. **Жизненный цикл View через State** — `IView.Initialize()` при Enter, `Deinitialize()` при Exit. Управляется выделенным `ViewLifecycleController<TView, TViewModel>`.

3. **Команды как триггеры переходов** — `IRelayCommand.Execute()` вызывает `stateMachine.Transition<T>()`, `CanExecute` проверяет `transition.CanTransition()`.

Отдельный пакет: `Aspid.Core.HSM.MVVM`

### 8.2 Aspid.FastTools

- **ProfilerMarkers**: Уже используются в генераторе. `.Marker()` для профилирования в рантайме.
- **IdRegistry**: Стабильные идентификаторы для состояний и переходов (конфигурация, аналитика, сериализация).
- **SerializableType**: Выбор типов состояний/контроллеров в инспекторе.
- **EnumValues**: Маппинг состояний на данные конфигурации (таймауты, параметры).

FastTools используется напрямую (существующая зависимость через ProfilerMarkers).

---

## 9. Структура проекта

### 9.1 Структура пакета

```
Aspid.Core.HSM/
├── Source/                          # Ядро (без зависимостей от Unity)
│   ├── IController.cs
│   ├── IState.cs
│   ├── IChildState.cs
│   ├── IStateMachine.cs
│   ├── ITransition.cs
│   ├── IExtensionState.cs
│   ├── IStateScope.cs
│   ├── StateFactory.cs
│   ├── EmptyState.cs
│   ├── Generation/                  # Атрибуты
│   │   ├── ParentStateAttribute.cs
│   │   ├── ControllerGroupAttribute.cs
│   │   ├── AsyncOfAttribute.cs
│   │   ├── AsyncModeAttribute.cs
│   │   ├── ExtensionForAttribute.cs
│   │   ├── TransitionAttribute.cs
│   │   ├── ScopeLifetimeAttribute.cs
│   │   ├── ReverseExecuteAttribute.cs
│   │   └── AsyncAttribute.cs
│   └── Extensions/
│       ├── StateExtensions.cs
│       └── StateMachineExtensions.cs
├── Unity/
│   └── Runtime/
│       ├── Controllers/             # Unity-специфичные интерфейсы контроллеров
│       │   ├── IEnterController.cs
│       │   ├── IExitController.cs
│       │   ├── IUpdateController.cs
│       │   ├── ILateUpdateController.cs
│       │   ├── IFixedUpdateController.cs
│       │   ├── IDisposableController.cs
│       │   ├── IAsyncEnterController.cs
│       │   └── IAsyncExitController.cs
│       └── StateMachines/
│           ├── StateMachineBase.cs
│           ├── StateMachineBase.Async.cs
│           ├── MonoStateMachine.cs
│           └── MonoStateMachine.Async.cs
```

### 9.2 Отдельные пакеты

- `Aspid.Core.HSM.VContainer` — Интеграция DI-скоупов VContainer
- `Aspid.Core.HSM.MVVM` — Интеграция жизненного цикла view/viewmodel с Aspid.MVVM

---

## 10. Фазы реализации

### Фаза 1: Ядро (Controller → ControllerGroup → State)
- Controller, ControllerGroup как самостоятельная переиспользуемая единица, State
- Иерархия состояний через `[ParentState]`
- StateMachineBase, MonoStateMachine
- Генераторы: `[ControllerGroup]` с вложенностью, `[ParentState]`
- Юнит-тесты

### Фаза 2: Асинхронная поддержка
- Асинхронные интерфейсы контроллеров с UniTask
- Спаривание `[AsyncOf]`
- `[AsyncMode]` для каждого интерфейса (последовательный/параллельный)
- `ChangeStateAsync`, CancellationToken
- Обновления генератора
- Тесты

### Фаза 3: Пайплайн переходов
- Интерфейс `ITransition` и базовая реализация
- Логика гардов, TransitionState
- Регистрация и цепочка как фоллбэк
- Асинхронно-осведомлённый пайплайн
- Тесты

### Фаза 4: Extension State
- `IExtensionState`, атрибут `[ExtensionFor]` и генератор
- Жизненный цикл присоединения/отсоединения, автоматическое отсоединение при несовместимом переходе
- Интеграция с диспатчем
- Тесты

### Фаза 5: Скоупы + VContainer
- Абстракция `IStateScope` в ядре
- `ScopeLifetime` (Transient/Cached)
- Обновления StateFactory для управления скоупами
- Пакет `Aspid.Core.HSM.VContainer`
- Тесты

### Фаза 6: Интеграции и полировка
- Пакет `Aspid.Core.HSM.MVVM`
- Диагностика на этапе компиляции в генераторах
- Точки расширения для конфигурации (заглушки, не полная реализация)
- Примеры, документация

### Фаза 7: Инструментарий Claude (отдельная спецификация)
- Скиллы для скаффолдинга (создание состояний/контроллеров/переходов)
- MCP-сервер для интроспекции дерева HSM
- Агенты для рефакторинга и миграции
- Гайдлайны для XML-документации и организации CLAUDE.md в потребительских проектах

---

## 11. Видение Claude-First DX

Фреймворк спроектирован для AI-ассистированной разработки. Потребительские проекты (например, CarX Street) будут использовать Claude как основной инструмент разработки для создания и модификации архитектуры на базе HSM.

### Принципы

- **XML-документация на конкретных контроллерах** — чтобы Claude понимал назначение каждого контроллера
- **CLAUDE.md для каждой фичи** — в папках фич потребительского проекта, описывающий организацию фичи через HSM
- **Предсказуемая структура файлов** — одна концепция на файл, именование соответствует типу, чтобы Claude мог навигировать по именам файлов
- **Паттерны скаффолдинга** — документированные паттерны «как добавить новое состояние», «как добавить контроллер», «как добавить переход»

### Планируемый инструментарий (отдельная спецификация)

- Скиллы Claude Code для скаффолдинга HSM
- MCP-сервер, предоставляющий дерево HSM, доступные переходы, зарегистрированные расширения
- Агенты для масштабной миграции существующих FSM-систем на HSM

---

## 12. Стратегия тестирования

### Юнит-тесты
- Диспатч контроллеров (порядок, обратный порядок, смешанные синхронные/асинхронные)
- Переходы между состояниями (навигация по иерархии, определение общего предка)
- Пайплайн переходов (гарды, жизненный цикл TransitionState)
- Extension State (присоединение, отсоединение, автоматическое отсоединение, совместимость)
- Жизненный цикл скоупов (создание, кэширование, уничтожение)
- Асинхронные режимы (последовательный, параллельный, отмена)

### Тесты генераторов
- Корректность генерации `[ControllerGroup]` (вложенные группы, асинхронные режимы)
- Корректность генерации `[ParentState]`
- Корректность генерации `[ExtensionFor]`
- Корректность генерации `[Transition]`
- Генерация диагностики (ошибки, предупреждения)

### Интеграционные тесты
- Полный жизненный цикл машины состояний со скоупами VContainer
- Интеграция жизненного цикла MVVM
- Пилот CarX Street (миграция DriftFsm)
