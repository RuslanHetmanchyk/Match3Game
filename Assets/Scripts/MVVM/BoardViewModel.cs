using System;
using Match3.Model;
using MVVM;
using UnityEngine;

namespace Match3.ViewModel
{
    public class BoardViewModel
    {
        public readonly int Width;
        public readonly int Height;
        public GemViewModel[,] Grid { get; }

        public event Action OnBoardChanged;

        public BoardViewModel(int width, int height)
        {
            Width = width;
            Height = height;
            Grid = new GemViewModel[width, height];
        }

        public void SetGem(int x, int y, GemViewModel gem)
        {
            var current = Grid[x, y];
            if (current == gem) return;

            Grid[x, y] = gem;

            if (gem != null)
            {
                gem.Model.Position = new Vector2Int(x, y);
            }

            OnBoardChanged?.Invoke();
        }

        public GemViewModel GetGem(int x, int y) => Grid[x, y];

        public void Swap(Vector2Int a, Vector2Int b)
        {
            var ga = Grid[a.x, a.y];
            var gb = Grid[b.x, b.y];
            Grid[a.x, a.y] = gb;
            Grid[b.x, b.y] = ga;
            if (ga != null) ga.Model.Position = b;
            if (gb != null) gb.Model.Position = a;
            OnBoardChanged?.Invoke();
        }

        // Utility to find empty cells etc.
    }
}