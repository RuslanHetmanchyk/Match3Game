using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Helpers;
using MVVM.Model;
using MVVM.View;
using MVVM.ViewModel;
using Pools;
using UnityEngine;

namespace GameControllers
{
    public class BoardController : MonoBehaviour
    {
        [SerializeField] private Transform gemParent;
        [SerializeField] private GemView gemPrefab;
        [SerializeField] private Sprite[] gemSprites;

        private readonly Dictionary<GemViewModel, GemView> vmToView = new ();


        private BoardViewModel boardVM;
        private GemPool pool;

        private void Awake()
        {
            boardVM = new BoardViewModel(GameConst.BoardWidth, GameConst.BoardHeight * 2);
            pool = new GemPool(gemPrefab, gemParent, GameConst.BoardWidth * GameConst.BoardHeight);

            InitializeBoard().Forget();
        }

        private Vector2 WorldPosFromIndex(int x, int y)
        {
            Vector2 origin = transform.position;
            var startX = origin.x - (GameConst.BoardWidth - 1) * GameConst.BoardCellSize * 0.5f;
            var startY = origin.y - (GameConst.BoardHeight - 1) * GameConst.BoardCellSize * 0.5f;
            return new Vector2(startX + x * GameConst.BoardCellSize, startY + y * GameConst.BoardCellSize);
        }

        private async UniTaskVoid InitializeBoard()
        {
            for (var x = 0; x < GameConst.BoardWidth; x++)
            {
                for (var y = 0; y < GameConst.BoardHeight; y++)
                {
                    var type = GetSafeRandomType(x, y);
                    var model = new GemModel(type, new Vector2Int(x, y));
                    var vm = new GemViewModel(model);
                    var view = pool.Rent();
                    view.transform.position = WorldPosFromIndex(x, y + GameConst.BoardHeight + 2);
                    view.Bind(vm, SpriteForType(type));
                    boardVM.SetGem(x, y, vm);
                    vmToView[vm] = view;

                    var target = WorldPosFromIndex(x, y);
                    vm.MoveTo(target, 0.25f).Forget();
                }
            }

            await UniTask.Delay(250);
            await ResolveMatchesLoop();
        }

        private Sprite SpriteForType(GemType t)
        {
            var index = Mathf.Clamp((int)t, 0, gemSprites.Length - 1);
            return gemSprites[index];
        }

        private GemType RandomGemType()
        {
            var count = Enum.GetValues(typeof(GemType)).Length;
            return (GemType)UnityEngine.Random.Range(0, Mathf.Max(1, count - 1));
        }

        private GemType GetSafeRandomType(int x, int y)
        {
            while (true)
            {
                var randomGemType = RandomGemType();

                if (x >= 2)
                {
                    var g1 = boardVM.GetGem(x - 1, y);
                    var g2 = boardVM.GetGem(x - 2, y);
                    if (g1 != null && g2 != null &&
                        g1.Model.Type == randomGemType &&
                        g2.Model.Type == randomGemType)
                    {
                        continue;
                    }
                }

                if (y >= 2)
                {
                    var g1 = boardVM.GetGem(x, y - 1);
                    var g2 = boardVM.GetGem(x, y - 2);
                    if (g1 != null && g2 != null &&
                        g1.Model.Type == randomGemType &&
                        g2.Model.Type == randomGemType)
                    {
                        continue;
                    }
                }

                return randomGemType;
            }
        }

        public async UniTask<bool> SwapAndResolve(Vector2Int a, Vector2Int b)
        {
            var gemA = boardVM.GetGem(a.x, a.y);
            var gemB = boardVM.GetGem(b.x, b.y);

            if (gemA == null || gemB == null)
            {
                return false;
            }

            var posA = WorldPosFromIndex(a.x, a.y);
            var posB = WorldPosFromIndex(b.x, b.y);

            await UniTask.WhenAll(
                gemA.MoveTo(posB, GameConst.GemSwapSec),
                gemB.MoveTo(posA, GameConst.GemSwapSec)
            );

            boardVM.Swap(a, b);

            var matches = MatchFinder.FindAllMatches(boardVM);

            if (matches.Count == 0)
            {
                boardVM.Swap(a, b);

                await UniTask.WhenAll(
                    gemA.MoveTo(posA, GameConst.GemSwapSec),
                    gemB.MoveTo(posB, GameConst.GemSwapSec)
                );

                return false;
            }

            await DestroyMatches(matches);
            await CollapseAndRefill();
            return true;
        }

        private async UniTask DestroyMatches(List<GemViewModel> matches)
        {
            foreach (var m in matches)
            {
                m.MarkDestroy();
                vmToView.Remove(m);

                int x = m.Model.Position.x;
                int y = m.Model.Position.y;
                boardVM.Grid[x, y] = null;
            }

            await UniTask.Delay((int)(GameConst.GemDestroyDelaySec * 1000));

            foreach (var m in matches)
            {
                if (vmToView.TryGetValue(m, out var view))
                {
                    await UniTask.Delay((int)(GameConst.GemDestroySec * 1000));
                    pool.Return(view);
                }
            }
        }

        private async UniTask CollapseAndRefill()
        {
            var destroyCount = new int[GameConst.BoardWidth];

            for (var x = 0; x < GameConst.BoardWidth; x++)
            {
                var holes = 0;
                for (var y = 0; y < GameConst.BoardHeight; y++)
                {
                    if (boardVM.Grid[x, y] == null)
                    {
                        holes++;
                    }
                }

                destroyCount[x] = holes;
            }

            for (var x = 0; x < GameConst.BoardWidth; x++)
            {
                var write = 0;

                for (var y = 0; y < GameConst.BoardHeight; y++)
                {
                    var gemViewModel = boardVM.Grid[x, y];
                    if (gemViewModel != null)
                    {
                        if (y != write)
                        {
                            boardVM.Grid[x, write] = gemViewModel;
                            boardVM.Grid[x, y] = null;

                            gemViewModel.Model.Position = new Vector2Int(x, write);
                        }

                        write++;
                    }
                }
            }

            for (var x = 0; x < GameConst.BoardWidth; x++)
            {
                for (var i = 0; i < destroyCount[x]; i++)
                {
                    var spawnY = GameConst.BoardHeight + i; // над верхом колонки

                    var type = RandomGemType();
                    var model = new GemModel(type, new Vector2Int(x, spawnY));
                    var vm = new GemViewModel(model);

                    var view = pool.Rent();
                    view.transform.position = WorldPosFromIndex(x, spawnY);
                    view.Bind(vm, SpriteForType(type));
                    vmToView[vm] = view;

                    boardVM.Grid[x, spawnY] = vm;
                }
            }

            await UniTask.Delay((int)(GameConst.CascadeStartDelaySec * 1000));

            var fallTasks = new List<UniTask>();

            for (var x = 0; x < GameConst.BoardWidth; x++)
            {
                fallTasks.Add(ProcessColumnFall(x));
            }

            await UniTask.WhenAll(fallTasks);

            var matches = MatchFinder.FindAllMatches(boardVM);

            if (matches.Count > 0)
            {
                await DestroyMatches(matches);
                await CollapseAndRefill();
            }
        }

        private async UniTask ProcessColumnFall(int x)
        {
            var topY = GameConst.BoardHeight - 1;

            while (topY + 1 < boardVM.Grid.GetLength(1) &&
                   boardVM.Grid[x, topY + 1] != null)
            {
                topY++;
            }

            var gems = new List<GemViewModel>();

            for (var y = 0; y <= topY; y++)
            {
                var gemViewModel = boardVM.Grid[x, y];
                if (gemViewModel != null)
                {
                    gems.Add(gemViewModel);
                }
            }

            for (var i = 0; i < gems.Count; i++)
            {
                var gem = gems[i];

                var targetY = i;
                gem.Model.Position = new Vector2Int(x, targetY);

                var target = WorldPosFromIndex(x, targetY);

                gem.MoveTo(target, GameConst.CascadeFallDurationSec).Forget();

                await UniTask.Delay((int)(GameConst.CascadeStepDelaySec * 1000));
            }

            for (int y = 0; y < gems.Count; y++)
            {
                boardVM.Grid[x, y] = gems[y];
            }

            for (int y = gems.Count; y <= topY; y++)
            {
                boardVM.Grid[x, y] = null;
            }
        }

        private async UniTask ResolveMatchesLoop()
        {
            var matches = MatchFinder.FindAllMatches(boardVM);
            if (matches.Count > 0)
            {
                await DestroyMatches(matches);
                await CollapseAndRefill();
            }
        }
    }
}