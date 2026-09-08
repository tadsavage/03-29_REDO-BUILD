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

            // Generate cells along the line, walking OUTWARD FROM THE DRAG START rather than from
            // min to max. The order is what decides which end of the run gets clipped when the
            // budget runs out below — from the start outward, the segments you dragged over first
            // keep their money and the far end nearest the cursor goes red.
            int stepX = targetCell.x >= startCell.x ? 1 : -1;
            int stepY = targetCell.y >= startCell.y ? 1 : -1;

            Vector2Int[] offsets = CurrentData.GetFootprintOffsets(-_currentRotation);

            _unaffordableCells.Clear();
            int budget = AffordableCount();
            bool clipped = false;

            for (int x = startCell.x; stepX > 0 ? x <= targetCell.x : x >= targetCell.x; x += stepX)
            {
                for (int y = startCell.y; stepY > 0 ? y <= targetCell.y : y >= targetCell.y; y += stepY)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool valid = _validator.IsValidPlacement(cell, offsets, CurrentData);

                    _indicatorBuffer.Add(cell);
                    foreach (var o in offsets) _indicatorBuffer.Add(cell + o);

                    if (valid && _dragCells.Count >= budget)
                    {
                        valid = false;
                        clipped = true;
                        _unaffordableCells.Add(cell);
                        foreach (var o in offsets) _unaffordableCells.Add(cell + o);
                    }

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

            _indicator.ShowCells(_indicatorBuffer, cell => IsFootprintValid(cell) && !_unaffordableCells.Contains(cell));

            int totalCost = _dragCells.Count * CurrentData.cost;
            _costUI.ShowCost(totalCost, !clipped, clipped ? "max affordable" : null);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDragPlacement();
            }
        }
    }
}
