using System;
using Aspid.FastTools.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Панель истории переходов HSM: лента записей «время: from → to» и служебных
	/// сообщений (старт/стоп Play Mode, поиск машины) с автопрокруткой вниз
	/// и ограничением количества записей.
	/// </summary>
	public sealed class StateTreeHistoryElement : VisualElement
	{
		private const int MaxEntries = 200;

		private readonly ScrollView m_scroll;

		public StateTreeHistoryElement()
		{
			var title = new Label("ИСТОРИЯ ПЕРЕХОДОВ")
				.SetFontSize(9)
				.SetLetterSpacing(1f)
				.SetColor(StateTreePalette.textDim)
				.SetAlignSelf(Align.Center);

			var clearButton = new Button()
				.SetText("Очистить")
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
				.AddChildren(header, m_scroll);
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
			m_scroll.AddChild(row.SetMarginBottom(1f));

			while (m_scroll.childCount > MaxEntries)
			{
				m_scroll.RemoveAt(0);
			}

			m_scroll.schedule.Execute(() => m_scroll.scrollOffset = new Vector2(0f, float.MaxValue));
		}

		private void ClearEntries() =>
			m_scroll.Clear();

		private Label BuildTimeLabel() =>
			BuildText(DateTime.Now.ToString("HH:mm:ss"), StateTreePalette.textDim)
				.SetMarginRight(8f);

		private Label BuildText(string text, Color color) =>
			new Label(text)
				.SetFontSize(10)
				.SetColor(color);
	}
}
