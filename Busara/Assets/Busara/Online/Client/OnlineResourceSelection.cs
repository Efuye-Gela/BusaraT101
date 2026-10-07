using System;
using System.Collections.Generic;

namespace Busara.Online.Client
{
    public sealed class OnlineResourceSelection
    {
        private readonly List<int> slots = new List<int>();
        public string Kind { get; private set; }
        public bool Active => Kind != null;
        public int[] Slots => slots.ToArray();

        public void Begin(string kind)
        {
            if (kind != "forgeChain" && kind != "weapon")
                throw new ArgumentException("Choose a forge chain or a weapon.", nameof(kind));
            slots.Clear();
            Kind = kind;
        }

        public void Clear()
        {
            slots.Clear();
            Kind = null;
        }

        public int IndexOf(int slot) => slots.IndexOf(slot);

        public void Toggle(int slot)
        {
            if (!Active) throw new InvalidOperationException("Choose an action before selecting resources.");
            int index = slots.IndexOf(slot);
            if (index >= 0) slots.RemoveRange(index, slots.Count - index);
            else slots.Add(slot);
        }

        public string Error(ClientView view) =>
            OrdinarySelection.Error(view.board, view.seat, slots, Kind == "weapon");
    }
}
