using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Инспектор выбранной ноды графа HSM: имя и namespace состояния, иерархические
	/// связи с навигацией по клику, список контроллеров состояния (вложенные
	/// <see cref="IController"/>-классы и интерфейсы самого стейта), runtime-статус
	/// и команды <c>ChangeState</c> / <c>TransitionTo</c> в Play Mode.
	/// </summary>
	public sealed class StateTreeInspectorElement : VisualElement
	{
		public event Action<Type> onChangeStateRequested;
		public event Action<Type> onTransitionToRequested;
		public event Action<Type> onNavigateRequested;

		private StateTreeNode m_node;
		private Label m_runtimeStatus;
		private Button m_changeStateButton;
		private Button m_transitionToButton;

		public StateTreeInspectorElement()
		{
			this.SetWidth(280f)
				.SetPaddingX(12f)
				.SetPaddingY(10f)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorLeft(StateTreePalette.panelBorder)
				.SetBorderWidthLeft(1f);

			ShowPlaceholder();
		}

		public void ShowPlaceholder()
		{
			m_node = null;
			Clear();

			this.AddChild(new Label("Выберите ноду на графе")
				.SetFontSize(11)
				.SetColor(StateTreePalette.textDim)
				.SetMarginTop(16f)
				.SetAlignSelf(Align.Center));
		}

		public void Show(StateTreeNode node)
		{
			m_node = node;
			Clear();

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			this.AddChild(scroll.SetFlexGrow(1f));

			scroll.AddChildren(
				BuildHeader(node),
				new Label(node.stateType.FullName)
					.SetFontSize(10)
					.SetColor(StateTreePalette.textSecondary)
					.SetWhiteSpace(WhiteSpace.Normal)
					.SetMarginBottom(8f),
				new Button()
					.SetText("Открыть скрипт")
					.AddClicked(() => node.stateType.OpenInScriptEditor()),
				BuildSectionTitle("Иерархия"),
				BuildHierarchySection(node),
				BuildSectionTitle("Контроллеры"),
				BuildControllersSection(node.stateType),
				BuildSectionTitle("Runtime"),
				BuildRuntimeSection());
		}

		public void UpdateRuntime(bool isPlaying, bool isMachineReady, bool isActive)
		{
			if (m_node == null)
			{
				return;
			}

			bool canControl = isPlaying && isMachineReady;
			m_changeStateButton.SetEnabled(canControl);
			m_transitionToButton.SetEnabled(canControl);

			if (!isPlaying)
			{
				m_runtimeStatus.SetText("Edit Mode").SetColor(StateTreePalette.textDim);
			}
			else if (!isMachineReady)
			{
				m_runtimeStatus.SetText("Машина не найдена").SetColor(StateTreePalette.textDim);
			}
			else if (isActive)
			{
				m_runtimeStatus.SetText("● Активно").SetColor(StateTreePalette.activeBorder);
			}
			else
			{
				m_runtimeStatus.SetText("○ Неактивно").SetColor(StateTreePalette.textSecondary);
			}
		}

		private VisualElement BuildHeader(StateTreeNode node)
		{
			var accent = new VisualElement()
				.SetSize(10f, 10f)
				.SetBorderRadius(5f)
				.SetMarginRight(6f)
				.SetAlignSelf(Align.Center)
				.SetBackgroundColor(StateTreePalette.GetDepthColor(node.depth));

			var title = new Label(node.stateType.Name)
				.SetFontSize(13)
				.SetColor(StateTreePalette.textPrimary)
				.SetUnityFontStyleAndWeight(FontStyle.Bold);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetMarginBottom(2f)
				.AddChildren(accent, title);
		}

		private VisualElement BuildHierarchySection(StateTreeNode node)
		{
			var section = new VisualElement();

			section.AddChild(BuildInfoRow("Глубина", node.depth.ToString()));
			section.AddChild(node.parent != null
				? BuildLinkRow("Родитель", node.parent.stateType)
				: BuildInfoRow("Родитель", "— (корень)"));

			if (node.children.Count == 0)
			{
				section.AddChild(BuildInfoRow("Дети", "— (лист)"));
			}
			else
			{
				foreach (StateTreeNode child in node.children)
				{
					section.AddChild(BuildLinkRow(child == node.children[0] ? "Дети" : string.Empty, child.stateType));
				}
			}

			return section;
		}

		private VisualElement BuildControllersSection(Type stateType)
		{
			var section = new VisualElement();
			bool isEmpty = true;
			bool isControllerGroup = false;

			// [ControllerGroup] emits explicit interface implementations that dispatch to private
			// "__controllerN" fields holding the actual inner controllers (see ControllerGroupBody).
			// List those instead of the group class itself, otherwise controllers registered via
			// AddControllers(...) never show up.
			foreach (FieldInfo field in stateType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (!IsGeneratedControllerGroupField(field) || !typeof(IController).IsAssignableFrom(field.FieldType))
				{
					continue;
				}

				isEmpty = false;
				isControllerGroup = true;
				section.AddChild(BuildControllerRow(field.FieldType.Name, field.FieldType));
			}

			foreach (Type nested in stateType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (nested.IsAbstract || !typeof(IController).IsAssignableFrom(nested))
				{
					continue;
				}

				isEmpty = false;
				section.AddChild(BuildControllerRow(nested.Name, nested));
			}

			if (!isControllerGroup && typeof(IController).IsAssignableFrom(stateType))
			{
				isEmpty = false;
				section.AddChild(BuildControllerRow("(сам стейт)", stateType));
			}

			if (isEmpty)
			{
				section.AddChild(new Label("Нет контроллеров")
					.SetFontSize(11)
					.SetColor(StateTreePalette.textDim));
			}

			return section;
		}

		private static bool IsGeneratedControllerGroupField(FieldInfo field)
		{
			if (!Regex.IsMatch(field.Name, @"^__controller\d+$"))
			{
				return false;
			}

			foreach (var attribute in field.GetCustomAttributes(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), false))
			{
				if (attribute is System.CodeDom.Compiler.GeneratedCodeAttribute generated
					&& generated.Tool == "Aspid.Core.HSM.Generators.ControllerGroupGenerator")
				{
					return true;
				}
			}

			return false;
		}

		private VisualElement BuildControllerRow(string title, Type controllerType)
		{
			var badges = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetFlexWrap(Wrap.Wrap);

			foreach (Type controllerInterface in controllerType.GetInterfaces())
			{
				if (controllerInterface == typeof(IController)
					|| !typeof(IController).IsAssignableFrom(controllerInterface))
				{
					continue;
				}

				string role = controllerInterface.Name.TrimStart('I').Replace("Controller", string.Empty);
				MethodInfo[] methods = controllerInterface.GetMethods();

				badges.AddChild(BuildBadge(role,
					methods.Length > 0 ? () => OpenControllerMethod(controllerType, controllerInterface, methods[0]) : null));
			}

			var titleLabel = new Label(title)
				.SetFontSize(11)
				.SetColor(StateTreePalette.selectionBorder);
			titleLabel.RegisterCallback<ClickEvent>(_ => controllerType.OpenInScriptEditor());

			return new VisualElement()
				.SetMarginBottom(4f)
				.AddChildren(titleLabel, badges);
		}

		private VisualElement BuildRuntimeSection()
		{
			m_runtimeStatus = new Label()
				.SetFontSize(11)
				.SetMarginBottom(4f);

			m_changeStateButton = new Button()
				.SetText("Change State")
				.SetFlexGrow(1f)
				.AddClicked(() => onChangeStateRequested?.Invoke(m_node.stateType));

			m_transitionToButton = new Button()
				.SetText("Transition To")
				.SetFlexGrow(1f)
				.AddClicked(() => onTransitionToRequested?.Invoke(m_node.stateType));

			var buttons = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.AddChildren(m_changeStateButton, m_transitionToButton);

			return new VisualElement().AddChildren(m_runtimeStatus, buttons);
		}

		private VisualElement BuildInfoRow(string title, string value) =>
			BuildRow(title, new Label(value)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textSecondary));

		private VisualElement BuildLinkRow(string title, Type target)
		{
			var link = new Label(target.Name)
				.SetFontSize(11)
				.SetColor(StateTreePalette.selectionBorder);
			link.RegisterCallback<ClickEvent>(_ => onNavigateRequested?.Invoke(target));

			return BuildRow(title, link);
		}

		private VisualElement BuildRow(string title, VisualElement value)
		{
			var titleLabel = new Label(title)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textDim)
				.SetWidth(64f)
				.SetFlexShrink(0f);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetMarginBottom(2f)
				.AddChildren(titleLabel, value);
		}

		private Label BuildSectionTitle(string title) =>
			new Label(title.ToUpperInvariant())
				.SetFontSize(9)
				.SetLetterSpacing(1f)
				.SetColor(StateTreePalette.textDim)
				.SetMarginTop(12f)
				.SetMarginBottom(4f)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.SetPaddingBottom(2f);

		private VisualElement BuildBadge(string text, Action onClick = null)
		{
			var badge = new Label(text)
				.SetFontSize(9)
				.SetColor(StateTreePalette.textSecondary)
				.SetBackgroundColor(StateTreePalette.cardBackground)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(7f)
				.SetPaddingX(5f)
				.SetPaddingY(1f)
				.SetMarginRight(3f)
				.SetMarginTop(2f);

			if (onClick != null)
			{
				badge.RegisterCallback<ClickEvent>(_ => onClick());
			}

			return badge;
		}

		/// <summary>
		/// Opens the controller's script at the line where it implements the given controller
		/// interface method. Falls back to the type declaration line if the method line can't be
		/// located (e.g. minified/generated source, or the member is expression-bodied on one line
		/// shared with other text the regex doesn't expect).
		/// </summary>
		private static void OpenControllerMethod(Type controllerType, Type controllerInterface, MethodInfo method)
		{
			MonoScript script = controllerType.FindMonoScript();

			if (script is null)
			{
				Debug.LogWarning($"MonoScript for type {controllerType.AssemblyQualifiedName} not found.");
				return;
			}

			int line = FindMethodLineNumber(script.text, controllerInterface.Name, method.Name);
			AssetDatabase.OpenAsset(script, line);
		}

		private static int FindMethodLineNumber(string text, string interfaceName, string methodName)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 1;
			}

			string[] lines = text.Split('\n');

			// Prefer an explicit interface implementation (e.g. "void IUpdateController.Update(").
			var explicitRegex = new Regex($@"\b{Regex.Escape(interfaceName)}\.{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (explicitRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			// Fall back to a regular member declaration (e.g. "public void Update(").
			var declRegex = new Regex(
				$@"\b(public|private|protected|internal|static|virtual|override|async)\b[^;{{]*\b{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (declRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			// Last resort: first occurrence of the method name at all.
			var plainRegex = new Regex($@"\b{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (plainRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			return 1;
		}
	}
}
