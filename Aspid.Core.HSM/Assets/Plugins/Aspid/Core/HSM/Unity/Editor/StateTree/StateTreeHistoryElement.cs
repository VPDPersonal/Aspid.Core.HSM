using System;
using Aspid.FastTools.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// HSM transition history panel: a feed of "time: from → to" entries and status
	/// messages (Play Mode start/stop, machine lookup) with auto-scroll to the bottom
	/// and a cap on the number of entries.
	/// </summary>
	public sealed class StateTreeHistoryElement : VisualElement
	{
		private const int MaxEntries = 200;

		private readonly ScrollView m_scroll;
		private readonly Label m_placeholder;

		public StateTreeHistoryElement()
		{
			m_placeholder = new Label("No events yet — transitions will appear here in Play Mode")
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetRight(0f)
				.SetTop(30f)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textDim)
				.SetUnityTextAlign(TextAnchor.MiddleCenter);
			m_placeholder.pickingMode = PickingMode.Ignore;

			var title = new Label("TRANSITION HISTORY")
				.SetFontSize(9)
				.SetLetterSpacing(1f)
				.SetColor(StateTreePalette.textDim)
				.SetAlignSelf(Align.Center);

			var clearButton = new Button()
				.SetText("Clear")
				.SetFontSize(10)
				.SetMarginLeft(new StyleLength(StyleKeyword.Auto))
				.AddClicked(ClearEntries);

			var header = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetHeight(22f)
				.SetPaddingX(8f)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.AddChildren(title, clearButton);

			m_scroll = new ScrollView(ScrollViewMode.Vertical);
			m_scroll.SetFlexGrow(1f).SetPaddingX(8f).SetPaddingY(4f);

			this.SetHeight(140f)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorTop(StateTreePalette.panelBorder)
				.SetBorderWidthTop(1f)
				.AddChildren(header, m_scroll, m_placeholder);
		}

		public void AddTransition(string fromState, string toState)
		{
			var row = new VisualElement().SetFlexDirection(FlexDirection.Row);
			row.AddChildren(
				BuildTimeLabel(),
				BuildText(fromState, StateTreePalette.textSecondary),
				BuildText(" → ", StateTreePalette.textDim),
				BuildText(toState, StateTreePalette.activeText));

			AddRow(row);
		}

		public void AddMessage(string message)
		{
			var row = new VisualElement().SetFlexDirection(FlexDirection.Row);
			row.AddChildren(
				BuildTimeLabel(),
				BuildText(message, StateTreePalette.textDim));

			AddRow(row);
		}

		private void AddRow(VisualElement row)
		{
			m_placeholder.SetDisplay(DisplayStyle.None);
			m_scroll.AddChild(row.SetMarginBottom(1f));

			while (m_scroll.childCount > MaxEntries)
			{
				m_scroll.RemoveAt(0);
			}

			m_scroll.schedule.Execute(() => m_scroll.scrollOffset = new Vector2(0f, float.MaxValue));
		}

		private void ClearEntries()
		{
			m_scroll.Clear();
			m_placeholder.SetDisplay(DisplayStyle.Flex);
		}

		private Label BuildTimeLabel() =>
			BuildText(DateTime.Now.ToString("HH:mm:ss"), StateTreePalette.textDim)
				.SetMarginRight(8f);

		private Label BuildText(string text, Color color) =>
			new Label(text)
				.SetFontSize(10)
				.SetColor(color);
	}
}
