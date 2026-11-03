using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberCity
{
    public sealed class MenuButton : Button
    {
        public MenuSurface surface;
        public Color restingStroke;
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (surface == null) return;
            surface.stroke = restingStroke;
            if (state == SelectionState.Highlighted || state == SelectionState.Selected)
                surface.stroke = new Color(restingStroke.r, restingStroke.g, restingStroke.b, 1);
            surface.SetVerticesDirty();
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable()) EventSystem.current?.SetSelectedGameObject(gameObject);
        }
    }
}
