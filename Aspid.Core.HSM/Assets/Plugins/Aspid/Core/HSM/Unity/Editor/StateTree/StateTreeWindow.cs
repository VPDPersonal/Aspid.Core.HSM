using System;
using System.Collections.Generic;
using System.Reflection;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Editor-окно визуализации дерева состояний HSM: нодовый граф на VisualElement
	/// с тремя режимами раскладки и перетаскиванием нод, инспектор выбранной ноды,
	/// история переходов и управление работающей <see cref="MonoStateMachine"/>
	/// (подсветка активной цепочки, ChangeState / TransitionTo) в Play Mode.
	/// </summary>
	public sealed class StateTreeWindow : EditorWindow
	{
		private const long ActivePollIntervalMs = 200;
		private const string DirectionPrefsKey = "Aspid.HSM.StateTree.Direction";
		private const string HistoryPrefsKey = "Aspid.HSM.StateTree.HistoryVisible";

		private readonly Dictionary<Type, StateTreeNodeElement> m_nodeElements = new();
		private readonly HashSet<Type> m_activeTypes = new();
		private List<StateTreeNode> m_roots;
		private StateTreeEdgesElement m_edges;
		private StateTreeCanvasElement m_canvas;
		private StateTreeInspectorElement m_inspector;
		private StateTreeHistoryElement m_history;
		private Label m_statusLabel;
		private Label m_extensionsLabel;
		private Button m_directionButton;
		private MonoStateMachine m_stateMachine;
		private StateTreeLayoutDirection m_direction;
		private Type m_selectedType;
		private string m_lastLeafName;

		private bool canControlMachine =>
			EditorApplication.isPlaying && m_stateMachine != null && m_stateMachine.IsInitialized;

		[MenuItem("Tools/Aspid/HSM State Tree")]
		public static void Open() =>
			GetWindow<StateTreeWindow>("HSM State Tree");

		private void CreateGUI()
		{
			m_direction = (StateTreeLayoutDirection)EditorPrefs.GetInt(DirectionPrefsKey, 0);

			BuildUi();
			RebuildGraph();
			rootVisualElement.schedule.Execute(PollActiveStates).Every(ActivePollIntervalMs);
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private void OnDestroy() =>
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

		private void BuildUi()
		{
			m_canvas = new StateTreeCanvasElement();
			m_canvas.onBackgroundClicked += () => SelectNode(null);

			m_inspector = new StateTreeInspectorElement();
			m_inspector.onChangeStateRequested += type => ExecuteMachineCommand("ChangeState", type);
			m_inspector.onTransitionToRequested += type => ExecuteMachineCommand("TransitionTo", type);
			m_inspector.onNavigateRequested += SelectNode;

			m_history = new StateTreeHistoryElement();
			m_history.SetDisplay(EditorPrefs.GetBool(HistoryPrefsKey, true) ? DisplayStyle.Flex : DisplayStyle.None);

			m_extensionsLabel = new Label()
				.SetPosition(Position.Absolute)
				.SetLeft(8f)
				.SetBottom(8f)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textSecondary)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColor(StateTreePalette.panelBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(8f)
				.SetPaddingX(8f)
				.SetPaddingY(3f)
				.SetDisplay(DisplayStyle.None);

			var canvasContainer = new VisualElement()
				.SetFlexGrow(1f)
				.AddChildren(m_canvas, m_extensionsLabel);

			rootVisualElement.AddChildren(
				BuildToolbar(),
				new VisualElement()
					.SetFlexDirection(FlexDirection.Row)
					.SetFlexGrow(1f)
					.AddChildren(canvasContainer, m_inspector),
				m_history);
		}

		private VisualElement BuildToolbar()
		{
			m_statusLabel = new Label()
				.SetFontSize(11)
				.SetMarginX(8f)
				.SetAlignSelf(Align.Center)
				.SetColor(StateTreePalette.textSecondary);

			m_directionButton = new Button()
				.SetText(GetDirectionCaption())
				.AddClicked(CycleDirection);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetHeight(24f)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.AddChildren(
					new Button()
						.SetText("Refresh")
						.AddClicked(RebuildGraph),
					m_directionButton,
					new Button()
						.SetText("Сброс разметки")
						.AddClicked(ClearCustomLayout),
					new Button()
						.SetText("История")
						.AddClicked(ToggleHistory),
					new VisualElement().SetFlexGrow(1f),
					m_statusLabel);
		}

		private void RebuildGraph()
		{
			m_roots = StateTreeGraphBuilder.Build();
			Vector2 canvasSize = StateTreeLayout.Arrange(m_roots, m_direction);
			ApplyCustomLayout(m_roots);

			m_canvas.Clear();
			m_nodeElements.Clear();
			m_activeTypes.Clear();

			m_edges = new StateTreeEdgesElement(m_roots, m_direction);
			m_canvas.AddChild(m_edges
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f)
				.SetWidth(canvasSize.x)
				.SetHeight(canvasSize.y));

			foreach (StateTreeNode root in m_roots)
			{
				CreateNodeElements(root);
			}

			RestoreSelection();
			m_canvas.FrameContent(canvasSize);
			PollActiveStates();
		}

		private void ApplyCustomLayout(List<StateTreeNode> roots)
		{
			Dictionary<string, Vector2> customPositions = StateTreeLayoutStorage.Load(m_direction);
			if (customPositions.Count == 0)
			{
				return;
			}

			var pending = new Stack<StateTreeNode>(roots);
			while (pending.Count > 0)
			{
				StateTreeNode node = pending.Pop();
				if (customPositions.TryGetValue(node.stateType.FullName, out Vector2 position))
				{
					node.position = new Rect(position, node.position.size);
				}

				foreach (StateTreeNode child in node.children)
				{
					pending.Push(child);
				}
			}
		}

		private void CreateNodeElements(StateTreeNode node)
		{
			var element = new StateTreeNodeElement(node);
			element.onSelected += selected => SelectNode(selected.stateType);
			element.onMoved += _ => m_edges.MarkDirtyRepaint();
			element.onDragCompleted += moved =>
				StateTreeLayoutStorage.Save(m_direction, moved.stateType, moved.position.position);
			element.AddManipulator(new ContextualMenuManipulator(populate => PopulateNodeMenu(populate, node)));

			m_nodeElements[node.stateType] = element;
			m_canvas.AddChild(element);

			foreach (StateTreeNode child in node.children)
			{
				CreateNodeElements(child);
			}
		}

		private void PopulateNodeMenu(ContextualMenuPopulateEvent populate, StateTreeNode node)
		{
			DropdownMenuAction.Status controlStatus = canControlMachine
				? DropdownMenuAction.Status.Normal
				: DropdownMenuAction.Status.Disabled;

			populate.menu.AppendAction(
				"Change State",
				_ => ExecuteMachineCommand("ChangeState", node.stateType),
				_ => controlStatus);
			populate.menu.AppendAction(
				"Transition To",
				_ => ExecuteMachineCommand("TransitionTo", node.stateType),
				_ => controlStatus);
			populate.menu.AppendSeparator();
			populate.menu.AppendAction("Открыть скрипт", _ => node.stateType.OpenInScriptEditor());
		}

		private void SelectNode(Type stateType)
		{
			m_selectedType = stateType;

			foreach (KeyValuePair<Type, StateTreeNodeElement> pair in m_nodeElements)
			{
				pair.Value.SetSelected(pair.Key == stateType);
			}

			if (stateType != null && m_nodeElements.TryGetValue(stateType, out StateTreeNodeElement element))
			{
				m_inspector.Show(element.node);
				UpdateInspectorRuntime();
			}
			else
			{
				m_inspector.ShowPlaceholder();
			}
		}

		private void RestoreSelection()
		{
			if (m_selectedType != null && !m_nodeElements.ContainsKey(m_selectedType))
			{
				m_selectedType = null;
			}

			SelectNode(m_selectedType);
		}

		private void CycleDirection()
		{
			m_direction = (StateTreeLayoutDirection)(((int)m_direction + 1) % 3);
			EditorPrefs.SetInt(DirectionPrefsKey, (int)m_direction);
			m_directionButton.SetText(GetDirectionCaption());
			RebuildGraph();
		}

		private string GetDirectionCaption() =>
			m_direction switch
			{
				StateTreeLayoutDirection.TopDown => "Вид: сверху вниз",
				StateTreeLayoutDirection.LeftToRight => "Вид: слева направо",
				_ => "Вид: радиальный"
			};

		private void ClearCustomLayout()
		{
			StateTreeLayoutStorage.Clear(m_direction);
			RebuildGraph();
		}

		private void ToggleHistory()
		{
			bool isVisible = m_history.resolvedStyle.display == DisplayStyle.Flex;
			m_history.SetDisplay(isVisible ? DisplayStyle.None : DisplayStyle.Flex);
			EditorPrefs.SetBool(HistoryPrefsKey, !isVisible);
		}

		private void ExecuteMachineCommand(string methodName, Type stateType)
		{
			if (!canControlMachine)
			{
				return;
			}

			try
			{
				MethodInfo method = typeof(MonoStateMachine)
					.GetMethod(methodName, Type.EmptyTypes)
					.MakeGenericMethod(stateType);
				method.Invoke(m_stateMachine, null);
				m_history.AddMessage($"Команда {methodName}<{stateType.Name}>");
			}
			catch (Exception exception)
			{
				m_history.AddMessage($"Ошибка {methodName}<{stateType.Name}>: {exception.InnerException?.Message ?? exception.Message}");
				Debug.LogException(exception);
			}

			PollActiveStates();
		}

		private void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode)
			{
				m_history.AddMessage("Play Mode запущен");
			}
			else if (change == PlayModeStateChange.EnteredEditMode)
			{
				m_history.AddMessage("Play Mode остановлен");
				m_stateMachine = null;
				m_lastLeafName = null;
				PollActiveStates();
			}
		}

		private void PollActiveStates()
		{
			IReadOnlyList<IState> currentStates = ResolveCurrentStates();

			UpdateExtensionsOverlay();
			UpdateInspectorRuntime();

			var activeTypes = new HashSet<Type>();
			foreach (IState state in currentStates)
			{
				activeTypes.Add(state.GetType());
			}

			if (m_activeTypes.SetEquals(activeTypes))
			{
				return;
			}

			LogTransition(currentStates);
			m_activeTypes.Clear();
			m_activeTypes.UnionWith(activeTypes);

			foreach (KeyValuePair<Type, StateTreeNodeElement> pair in m_nodeElements)
			{
				pair.Value.SetActive(m_activeTypes.Contains(pair.Key));
			}

			m_edges.SetActiveTypes(m_activeTypes);
			m_statusLabel.SetText(BuildStatusText(currentStates));
		}

		private void LogTransition(IReadOnlyList<IState> currentStates)
		{
			string leafName = currentStates.Count > 0 ? currentStates[^1].GetType().Name : null;
			if (leafName == m_lastLeafName)
			{
				return;
			}

			if (leafName != null)
			{
				m_history.AddTransition(m_lastLeafName ?? "∅", leafName);
			}

			m_lastLeafName = leafName;
		}

		private IReadOnlyList<IState> ResolveCurrentStates()
		{
			if (!EditorApplication.isPlaying)
			{
				m_stateMachine = null;
				return Array.Empty<IState>();
			}

			if (m_stateMachine == null)
			{
				m_stateMachine = FindFirstObjectByType<MonoStateMachine>(FindObjectsInactive.Include);
				if (m_stateMachine != null)
				{
					m_history.AddMessage($"MonoStateMachine найдена: {m_stateMachine.name}");
				}
			}

			return m_stateMachine == null ? Array.Empty<IState>() : m_stateMachine.CurrentStates;
		}

		private void UpdateExtensionsOverlay()
		{
			IReadOnlyList<IExtensionState> extensions = m_stateMachine != null
				? m_stateMachine.ActiveExtensions
				: Array.Empty<IExtensionState>();

			if (!EditorApplication.isPlaying || extensions.Count == 0)
			{
				m_extensionsLabel.SetDisplay(DisplayStyle.None);
				return;
			}

			var names = new List<string>(extensions.Count);
			foreach (IExtensionState extension in extensions)
			{
				names.Add(extension.GetType().Name);
			}

			m_extensionsLabel
				.SetText($"Extensions: {string.Join(", ", names)}")
				.SetDisplay(DisplayStyle.Flex);
		}

		private void UpdateInspectorRuntime()
		{
			bool isActive = m_selectedType != null && m_activeTypes.Contains(m_selectedType);
			m_inspector.UpdateRuntime(EditorApplication.isPlaying, canControlMachine, isActive);
		}

		private string BuildStatusText(IReadOnlyList<IState> currentStates)
		{
			if (!EditorApplication.isPlaying)
			{
				return "Edit Mode — машина не запущена";
			}

			if (m_stateMachine == null)
			{
				return "Play Mode — MonoStateMachine не найдена";
			}

			if (currentStates.Count == 0)
			{
				return "Play Mode — машина не инициализирована";
			}

			var names = new List<string>(currentStates.Count);
			foreach (IState state in currentStates)
			{
				names.Add(state.GetType().Name);
			}

			return string.Join(" → ", names);
		}
	}
}
