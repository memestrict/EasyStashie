using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using SharpDX;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace Stashie
{
    public class StashieCore : BaseSettingsPlugin<StashieSettings>
    {
        private readonly Stopwatch _debugTimer = new Stopwatch();

        private Vector2 _clickWindowOffset;
        private List<ItemData> _dropItems;
        private uint _coroutineIteration;
        private Coroutine _coroutineWorker;

        public StashieCore()
        {
            Name = "Stashie";
        }

        public override bool Initialise()
        {

            Input.RegisterKey(Settings.DropHotkey);

            Settings.DropHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.DropHotkey); };

            return true;
        }
        public override void DrawSettings()
        {
            DrawIgnoredCellsSettings();
            base.DrawSettings();
        }

        public void SaveIgnoredSLotsFromInventoryTemplate()
        {
            Settings.IgnoredCells = new[,]
            {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
            };
            try
            {
                var inventory_server = GameController.IngameState.Data.ServerData.PlayerInventories[0];

                foreach (var item in inventory_server.Inventory.InventorySlotItems)
                {
                    var baseC = item.Item.GetComponent<Base>();
                    var itemSizeX = baseC.ItemCellsSizeX;
                    var itemSizeY = baseC.ItemCellsSizeY;
                    var inventPosX = item.PosX;
                    var inventPosY = item.PosY;
                    for (var y = 0; y < itemSizeY; y++)
                    for (var x = 0; x < itemSizeX; x++)
                        Settings.IgnoredCells[y + inventPosY, x + inventPosX] = 1;
                }
            }
            catch (Exception e)
            {
                LogError($"{e}", 5);
            }
        }

        private void DrawIgnoredCellsSettings()
        {
            try
            {
                if (ImGui.Button("Copy Inventory")) SaveIgnoredSLotsFromInventoryTemplate();

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        $"Checked = Item will be ignored{Environment.NewLine}UnChecked = Item will be processed");
            }
            catch (Exception e)
            {
                DebugWindow.LogError(e.ToString(), 10);
            }

            var numb = 1;
            for (var i = 0; i < 5; i++)
            for (var j = 0; j < 12; j++)
            {
                var toggled = Convert.ToBoolean(Settings.IgnoredCells[i, j]);
                if (ImGui.Checkbox($"##{numb}IgnoredCells", ref toggled)) Settings.IgnoredCells[i, j] ^= 1;

                if ((numb - 1) % 12 < 11) ImGui.SameLine();

                numb += 1;
            }
        }

        public override Job Tick()
        {
            if (!stashingRequirementsMet() && Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") != null)
            {
                StopCoroutine("Stashie_DropItemsToStash");
                return null;
            }
            
            if (Settings.DropHotkey.PressedOnce())
            {
                if(Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
                {
                    StartDropItemsToStashCoroutine();
                }
                else
                {
                    StopCoroutine("Stashie_DropItemsToStash");
                }
            }
            return null;
        }

        private void StartDropItemsToStashCoroutine()
        {
            _debugTimer.Reset();
            _debugTimer.Start();
            Core.ParallelRunner.Run(new Coroutine(DropToStashRoutine(), this, "Stashie_DropItemsToStash"));
        }

        private void StopCoroutine(string routineName)
        {
            var routine = Core.ParallelRunner.FindByName(routineName);
            routine?.Done();
            _debugTimer.Stop();
            _debugTimer.Reset();
            CleanUp();
        }
        private IEnumerator DropToStashRoutine()
        {
            var cursorPosPreMoving = Input.ForceMousePosition; //saving cursorposition

            yield return ParseItems();
            for (int tries = 0; tries < 3 && _dropItems.Count > 0; ++tries)
            {
                if (_dropItems.Count > 0)
                    yield return StashItemsIncrementer();
                yield return ParseItems();
                yield return new WaitTime(Settings.ExtraDelay);
            }
                

            //restoring cursorposition
            Input.SetCursorPos(cursorPosPreMoving);
            Input.MouseMove();
            StopCoroutine("Stashie_DropItemsToStash");
        }

        private void CleanUp()
        {
            Input.KeyUp(Keys.LControlKey);
        }

        private bool stashingRequirementsMet()
        {
            return GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                    GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;
        }

        private IEnumerator ParseItems()
        {
            var inventory = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            var invItems = inventory.InventorySlotItems;

            yield return new WaitFunctionTimed(() => invItems != null,true,500, "ServerInventory->InventSlotItems is null!");
            _dropItems = new List<ItemData>();
            _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            foreach (var invItem in invItems)
            {
                if (invItem.Item == null || invItem.Address == 0) continue;
                if (CheckIgnoreCells(invItem)) continue;
                var baseItemType = GameController.Files.BaseItemTypes.Translate(invItem.Item.Path);

                var testItem = new ItemData(invItem, baseItemType, calculateClickPos(invItem));
                _dropItems.Add(testItem);
            }
        }
        private Vector2 calculateClickPos(InventSlotItem invItem)
        {
            //hacky clickpos calc work
            
            var InventoryPanelRectF = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].GetClientRect();
            var CellWidth = InventoryPanelRectF.Width / 12;
            var CellHeight = InventoryPanelRectF.Height / 5;
            var itemInventPosition = invItem.InventoryPosition;

            Vector2 clickpos = new Vector2(
                InventoryPanelRectF.Location.X + (CellWidth / 2) + (itemInventPosition.X * CellWidth), 
                InventoryPanelRectF.Location.Y + (CellHeight / 2) + (itemInventPosition.Y * CellHeight)
                );

            return clickpos;
        }

        private bool CheckIgnoreCells(InventSlotItem inventItem)
        {
            var inventPosX = inventItem.PosX;
            var inventPosY = inventItem.PosY;

            if (inventPosX < 0 || inventPosX >= 12) return true;
            if (inventPosY < 0 || inventPosY >= 5) return true;

            return Settings.IgnoredCells[inventPosY, inventPosX] != 0;
        }

        private IEnumerator StashItemsIncrementer()
        {
            _coroutineIteration++;

            yield return StashItems();
        }
        private IEnumerator StashItems()
        {
            PublishEvent("stashie_start_drop_items", null);

            Input.KeyDown(Keys.LControlKey);
            LogMessage($"Want to drop {_dropItems.Count} items.");
            foreach(var stashresult in _dropItems)
            {
                _coroutineIteration++;
                _coroutineWorker?.UpdateTicks(_coroutineIteration);
                var maxTryTime = _debugTimer.ElapsedMilliseconds + 2000;
                //move to correct tab

                yield return StashItem(stashresult);
                
                _debugTimer.Restart();
                PublishEvent("stashie_finish_drop_items_to_stash_tab", null);
            }
        }

        private IEnumerator StashItem(ItemData stashresult)
        {
            Input.SetCursorPos(stashresult.clientRect + _clickWindowOffset);
            yield return new WaitTime(Settings.HoverItemDelay);

            Input.Click(MouseButtons.Left);
            
            yield return new WaitTime(Settings.StashItemDelay);
        }
    }
}