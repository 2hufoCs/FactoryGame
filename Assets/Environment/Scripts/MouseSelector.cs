using UnityEngine;
using UnityEngine.InputSystem;

public class MouseSelector : MonoBehaviour
{
    ISelectableBasic GetSelectable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            return null;

        if (hit.collider.gameObject.TryGetComponent(out ISelectableBasic selectable))
        {
            return selectable;
        }

        return null;
    }

    ISelectableAdvanced GetSelectable(bool advanced)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            return null;

        if (hit.collider.gameObject.TryGetComponent(out ISelectableAdvanced selectable))
            return selectable;

        return null;
    }

    public void OnLeftClick(InputAction.CallbackContext ctxt)
    {
        ISelectableAdvanced advancedSelectable = GetSelectable(true);
        bool advanced = advancedSelectable != null;

        ISelectableBasic basicSelectable = null;
        if (advanced)
            basicSelectable = GetSelectable();

        if (ctxt.performed && advanced)
        {
            advancedSelectable?.OnSelectPerformed(); 
        }
        else if (ctxt.performed) basicSelectable?.OnSelectPerformed();
        else if (ctxt.canceled && advanced) advancedSelectable?.OnSelectCanceled();
        else if (ctxt.canceled) basicSelectable?.OnSelectCanceled();
    }

    public void OnRightClick(InputAction.CallbackContext ctxt)
    {
        if (ctxt.performed) GetSelectable(true)?.OnRightClickPerformed();
    }

    public void OnScroll(InputAction.CallbackContext ctxt)
    {
        bool scrollUp = ctxt.ReadValue<float>() > 0;
        //if (ctxt.performed) GetSelectable(true)?.OnScroll(scrollUp);
    }
}

public interface ISelectableBasic
{
    public abstract void OnSelectPerformed();
    public abstract void OnSelectCanceled();
}

public interface ISelectableAdvanced : ISelectableBasic
{
    public abstract void OnRightClickPerformed();
    public abstract void OnRightClickCanceled();

    //public abstract void OnHovered();
}