using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace Stashie
{
    public class StashieSettings : ISettings
    {
        public List<string> AllStashNames = new List<string>();
        

        public StashieSettings()
        {
            Enable = new ToggleNode(false);
            DropHotkey = Keys.F3;
            ExtraDelay = new RangeNode<int>(0, 0, 2000);
            HoverItemDelay = new RangeNode<int>(5, 0, 2000);
            StashItemDelay = new RangeNode<int>(5, 0, 2000);
            BlockInput = new ToggleNode(false);
        }


        [Menu("Stash Hotkey")] 
        public HotkeyNode DropHotkey { get; set; }

        [Menu("Extra Delay", "Delay to wait after each inventory clearing attempt(in ms).")]
        public RangeNode<int> ExtraDelay { get; set; }
        [Menu("HoverItem Delay", "Delay used to wait inbetween checks for the Hoveritem (in ms).")]
        public RangeNode<int> HoverItemDelay { get; set; }
        [Menu("StashItem Delay", "Delay used to wait after moving the mouse on an item to Stash until clicking it(in ms).")]
        public RangeNode<int> StashItemDelay { get; set; }

        [Menu("Block Input", "Block user input (except: Ctrl+Alt+Delete) when dropping items to stash.")]
        public ToggleNode BlockInput { get; set; }

        public ToggleNode Enable { get; set; }

        public int[,] IgnoredCells { get; set; } =
        {
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}
        };
        
    }
}