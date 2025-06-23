using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

public class InputHandler : MonoBehaviour
{
    private Camera _mainCamera;

    public void SetCamera(Transform camera)
    {
        _mainCamera = camera.GetComponent<Camera>();
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        // Block clicks if over UI
        if (IsPointerOverUI()) return;

        var rayHit = Physics2D.GetRayIntersection(_mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        if (!rayHit.collider) return;

        var clickable = rayHit.collider.GetComponent<IClickable>();
        if (clickable != null)
        {
            clickable.OnClick();
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();

        // Look through all active GraphicRaycasters
        foreach (var canvas in GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isActiveAndEnabled) continue;

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) continue;

            raycaster.Raycast(eventData, results);
            if (results.Count > 0)
                return true;
        }

        return false;
    }
}