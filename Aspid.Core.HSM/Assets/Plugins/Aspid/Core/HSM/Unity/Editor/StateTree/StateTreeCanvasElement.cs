using System;
using Aspid.FastTools.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Канвас графа в духе Shader Graph: тёмный фон с сеткой, панорамирование
	/// перетаскиванием мыши и зум колесом вокруг курсора. Дочерние элементы
	/// добавляются в трансформируемый слой контента. Клик по пустому фону
	/// (без перетаскивания) поднимает <see cref="onBackgroundClicked"/>.
	/// </summary>
	public sealed class StateTreeCanvasElement : VisualElement
	{
		private const float GridStep = 48f;
		private const int MajorLineEvery = 5;
		private const float MinZoom = 0.2f;
		private const float MaxZoom = 2.5f;
		private const float ZoomStepBase = 1.02f;
		private const float ClickDragThreshold = 4f;

		public event Action onBackgroundClicked;

		private readonly VisualElement m_content;

		private Vector2 m_offset;
		private float m_zoom = 1f;
		private int m_panPointerId = -1;
		private Vector2 m_lastPointerPosition;
		private float m_panDistance;
		private Vector2 m_pendingFrameSize;

		public override VisualElement contentContainer => m_content;

		public StateTreeCanvasElement()
		{
			m_content = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f);
			m_content.style.transformOrigin = new TransformOrigin(0f, 0f);

			this.SetFlexGrow(1f)
				.SetOverflow(Overflow.Hidden)
				.SetBackgroundColor(StateTreePalette.canvasBackground);
			hierarchy.Add(m_content);

			generateVisualContent += OnGenerateVisualContent;
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<WheelEvent>(OnWheel);
			RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
		}

		/// <summary>
		/// Вписывает и центрирует контент указанного размера во вьюпорте, подбирая зум
		/// так, чтобы контент влезал целиком (после первого лейаута, если размер
		/// вьюпорта ещё неизвестен).
		/// </summary>
		public void FrameContent(Vector2 contentSize)
		{
			if (float.IsNaN(contentRect.width) || contentRect.width <= 0f)
			{
				m_pendingFrameSize = contentSize;
				return;
			}

			m_pendingFrameSize = Vector2.zero;
			m_zoom = Mathf.Clamp(
				Mathf.Min(contentRect.width / contentSize.x, contentRect.height / contentSize.y),
				MinZoom,
				1f);
			m_offset = new Vector2(
				(contentRect.width - contentSize.x * m_zoom) * 0.5f,
				Mathf.Max((contentRect.height - contentSize.y * m_zoom) * 0.5f, GridStep * 0.5f));
			ApplyTransform();
		}

		private void OnGeometryChanged(GeometryChangedEvent geometryEvent)
		{
			if (m_pendingFrameSize != Vector2.zero)
			{
				FrameContent(m_pendingFrameSize);
			}
		}

		private void OnPointerDown(PointerDownEvent pointerEvent)
		{
			if (pointerEvent.button != 0 && pointerEvent.button != 2)
			{
				return;
			}

			m_panPointerId = pointerEvent.pointerId;
			m_lastPointerPosition = pointerEvent.position;
			m_panDistance = 0f;
			this.CapturePointer(m_panPointerId);
			pointerEvent.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_panPointerId)
			{
				return;
			}

			Vector2 position = pointerEvent.position;
			Vector2 delta = position - m_lastPointerPosition;
			m_panDistance += delta.magnitude;
			m_offset += delta;
			m_lastPointerPosition = position;
			ApplyTransform();
		}

		private void OnPointerUp(PointerUpEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_panPointerId)
			{
				return;
			}

			this.ReleasePointer(m_panPointerId);
			m_panPointerId = -1;

			if (m_panDistance < ClickDragThreshold && pointerEvent.button == 0)
			{
				onBackgroundClicked?.Invoke();
			}
		}

		private void OnWheel(WheelEvent wheelEvent)
		{
			float newZoom = Mathf.Clamp(m_zoom * Mathf.Pow(ZoomStepBase, -wheelEvent.delta.y), MinZoom, MaxZoom);
			if (Mathf.Approximately(newZoom, m_zoom))
			{
				return;
			}

			// Зум вокруг курсора: точка мира под мышью остаётся на месте.
			Vector2 pointer = this.WorldToLocal(wheelEvent.mousePosition);
			Vector2 worldPoint = (pointer - m_offset) / m_zoom;
			m_zoom = newZoom;
			m_offset = pointer - worldPoint * m_zoom;

			ApplyTransform();
			wheelEvent.StopPropagation();
		}

		private void ApplyTransform()
		{
			m_content.style.translate = new Translate(m_offset.x, m_offset.y);
			m_content.style.scale = new Scale(new Vector3(m_zoom, m_zoom, 1f));
			MarkDirtyRepaint();
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			float step = GridStep * m_zoom;
			if (step < 4f)
			{
				step *= MajorLineEvery;
			}

			DrawGridLines(painter, step, isVertical: true);
			DrawGridLines(painter, step, isVertical: false);
		}

		private void DrawGridLines(Painter2D painter, float step, bool isVertical)
		{
			float viewportSize = isVertical ? contentRect.width : contentRect.height;
			float offset = isVertical ? m_offset.x : m_offset.y;
			float first = Mathf.Repeat(offset, step);

			for (float position = first; position <= viewportSize; position += step)
			{
				int worldIndex = Mathf.RoundToInt((position - offset) / step);
				painter.strokeColor = worldIndex % MajorLineEvery == 0
					? StateTreePalette.gridMajorLine
					: StateTreePalette.gridLine;
				painter.lineWidth = 1f;
				painter.BeginPath();
				painter.MoveTo(isVertical ? new Vector2(position, 0f) : new Vector2(0f, position));
				painter.LineTo(isVertical
					? new Vector2(position, contentRect.height)
					: new Vector2(contentRect.width, position));
				painter.Stroke();
			}
		}
	}
}
