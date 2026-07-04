using GameCore.Economy;
using GameCore.Build;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCore.Core.FSM.States
{
    /// <summary>
    /// Specialized state for placing walls in straight lines.
    /// Overrides standard drag placement to force orthogonal lines.
    /// </summary>
    public class WallBuildState : BuildState
    {
        public WallBuildState(
            PlacementActions actions,
            PreviewController preview,
            PlacementValidator validator,
            PlacementFinalizer finalizer,
            PlacementGrid grid,
            PlacementStateMachine fsm,
            RaycastController raycast,
            CellIndicatorController indicator,
            MoneyService money,
            PreviewCostUI costUI,
            BuildMenuUI buildMenuUI) 
            : base(actions, preview, validator, finalizer, grid, fsm, raycast, indicator, money, costUI, buildMenuUI)
        {
        }

        protected override void HandleDragPlacement(Vector2Int currentCell)
        {
            // Clear previous visuals
            _dragCells.Clear();
            _preview.ClearMultiGhosts();
            _indicatorBuffer.Clear();

            // Force orthogonal dragging (only X or only Y axis change)
            Vector2Int startCell = _dragStartCell;
            Vector2Int diff = currentCell - startCell;
            
            Vector2Int targetCell = currentCell;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
            {
                targetCell = new Vector2Int(currentCell.x, startCell.y);
            }
            else
            {
                targetCell = new Vector2Int(startCell.x, currentCell.y);
            }

            // Generate cells along the line
            int minX = Mathf.Min(startCell.x, targetCell.x);
            int maxX = Mathf.Max(startCell.x, targetCell.x);
            int minY = Mathf.Min(startCell.y, targetCell.y);
            int maxY = Mathf.Max(startCell.y, targetCell.y);

            Vector2Int[] offsets = CurrentData.GetFootprintOffsets(-_currentRotation);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool valid = _validator.IsValidPlacement(cell, offsets, CurrentData);
                    
                    _indicatorBuffer.Add(cell);
                    foreach (var o in offsets) _indicatorBuffer.Add(cell + o);

                    if (valid)
                    {
                        _dragCells.Add(cell);
                        _preview.ShowMultiGhost(cell, true, _currentRotation);
                    }
                    else
                    {
                        _preview.ShowMultiGhost(cell, false, _currentRotation);
                    }
                }
            }

            _indicator.ShowCells(_indicatorBuffer, cell => IsFootprintValid(cell));

            int totalCost = _dragCells.Count * CurrentData.cost;
            _costUI.ShowCost(totalCost, _money.CanAfford(totalCost));

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDragPlacement();
            }
        }
    }
}
