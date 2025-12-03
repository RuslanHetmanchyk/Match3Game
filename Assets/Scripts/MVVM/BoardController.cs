using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Match3.Model;
using Match3.Services;
using Match3.View;
using Match3.ViewModel;
using MVVM;
using UnityEngine;

namespace Match3.Controllers
{
    public class BoardController : MonoBehaviour
    {
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Transform gemParent;
        [SerializeField] private GemView gemPrefab;
        [SerializeField] private Sprite[] gemSprites; // map by GemType enum order (excluding bomb maybe)

        private BoardViewModel boardVM;
        private GemPool pool;

        private void Awake()
        {
            boardVM = new BoardViewModel(width, height);
            pool = new GemPool(gemPrefab, gemParent, width * height);

            InitializeBoard().Forget();
        }

        private Vector2 WorldPosFromIndex(int x, int y)
        {
            // center the board around controller position
            Vector2 origin = (Vector2)transform.position;
            float startX = origin.x - (width - 1) * cellSize * 0.5f;
            float startY = origin.y - (height - 1) * cellSize * 0.5f;
            return new Vector2(startX + x * cellSize, startY + y * cellSize);
        }

        private async UniTaskVoid InitializeBoard()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var type = GetSafeRandomType(x, y);
                    var model = new GemModel(type, new Vector2Int(x, y));
                    var vm = new GemViewModel(model);
                    var view = pool.Rent();
                    view.transform.position = WorldPosFromIndex(x, y + height + 2); // spawn above for drop effect
                    view.Bind(vm, SpriteForType(type));
                    boardVM.SetGem(x, y, vm);
                    // animate drop
                    var target = WorldPosFromIndex(x, y);
                    vm.MoveTo(target, 0.25f).Forget();
                }
            }

            await UniTask.Delay(250);
            await ResolveMatchesLoop();
        }

        private Sprite SpriteForType(GemType t)
        {
            int idx = Mathf.Clamp((int)t, 0, gemSprites.Length - 1);
            return gemSprites[idx];
        }

        private GemType RandomGemType()
        {
            // include bomb chance? keep simple
            int count = Enum.GetValues(typeof(GemType)).Length;
            return (GemType)UnityEngine.Random.Range(0, Mathf.Max(1, count - 1)); // optional: exclude bomb for simplicity
        }
        
        private GemType GetSafeRandomType(int x, int y)
        {
            while (true)
            {
                GemType t = RandomGemType();

                // Проверка на горизонтальный матч (левее)
                if (x >= 2)
                {
                    var g1 = boardVM.GetGem(x - 1, y);
                    var g2 = boardVM.GetGem(x - 2, y);
                    if (g1 != null && g2 != null &&
                        g1.Model.Type == t &&
                        g2.Model.Type == t)
                    {
                        continue; // пробуем новый тип
                    }
                }

                // Проверка на вертикальный матч (внизу)
                if (y >= 2)
                {
                    var g1 = boardVM.GetGem(x, y - 1);
                    var g2 = boardVM.GetGem(x, y - 2);
                    if (g1 != null && g2 != null &&
                        g1.Model.Type == t &&
                        g2.Model.Type == t)
                    {
                        continue; // пробуем новый
                    }
                }

                return t; // безопасный тип
            }
        }


        // Example public swap API (called from input handler)
        public async UniTask<bool> SwapAndResolve(Vector2Int a, Vector2Int b)
        {
            var gemA = boardVM.GetGem(a.x, a.y);
            var gemB = boardVM.GetGem(b.x, b.y);

            if (gemA == null || gemB == null)
                return false;

            // Мировые позиции
            var posA = WorldPosFromIndex(a.x, a.y);
            var posB = WorldPosFromIndex(b.x, b.y);

            // ------------------------------------------
            // 1️⃣ Полный визуальный swap (A -> B, B -> A)
            // ------------------------------------------
            await UniTask.WhenAll(
                gemA.MoveTo(posB, GameConst.GemSwapSec),
                gemB.MoveTo(posA, GameConst.GemSwapSec)
            );

            // ------------------------------------------
            // 2️⃣ После завершения анимации - меняем логику
            // ------------------------------------------
            boardVM.Swap(a, b);

            // ------------------------------------------
            // 3️⃣ Ищем матчи
            // ------------------------------------------
            var matches = MatchFinder.FindAllMatches(boardVM);

            if (matches.Count == 0)
            {
                // ❌ Матчей НЕТ → Rollback

                // логика назад
                boardVM.Swap(a, b);

                // ------------------------------------------
                // 4️⃣ ПОЛНЫЙ rollback (сначала доехать → потом назад)
                // ------------------------------------------
                await UniTask.WhenAll(
                    gemA.MoveTo(posA, GameConst.GemSwapSec),
                    gemB.MoveTo(posB, GameConst.GemSwapSec)
                );

                return false;
            }

            // ✔ Матч есть — продолжаем
            await DestroyMatches(matches);
            await CollapseAndRefill();
            return true;
        }

        private async UniTask DestroyMatches(List<GemViewModel> matches)
        {
            foreach (var m in matches)
            {
                m.MarkDestroy();
                // find view to return to pool after animation: views deactivate themselves on animation complete
                var view = FindViewByVM(m);
                if (view != null)
                {
                    // schedule return after short delay
                    await UniTask.Delay(260);
                    pool.Return(view);
                }
                // clear spot in board VM
                boardVM.SetGem(m.Model.Position.x, m.Model.Position.y, null);
            }
        }

        private GemView FindViewByVM(GemViewModel vm)
        {
            // naive search through parent children (could keep a map vm->view)
            foreach (Transform t in gemParent)
            {
                var v = t.GetComponent<GemView>();
                if (v != null && v.ViewModel == vm) return v;
            }
            return null;
        }

        private async UniTask CollapseAndRefill()
        {
            // simple collapse: for each column, drop down existing gems and spawn new at top
            for (int x = 0; x < width; x++)
            {
                int write = 0;
                for (int y = 0; y < height; y++)
                {
                    var g = boardVM.GetGem(x, y);
                    if (g != null)
                    {
                        if (y != write)
                        {
                            boardVM.SetGem(x, write, g);
                            boardVM.SetGem(x, y, null);
                            var view = FindViewByVM(g);
                            if (view != null)
                            {
                                var target = WorldPosFromIndex(x, write);
                                g.MoveTo(target, 0.12f).Forget();
                            }
                        }
                        write++;
                    }
                }

                // spawn new for remaining
                for (int y = write; y < height; y++)
                {
                    var type = RandomGemType();
                    var model = new GemModel(type, new Vector2Int(x, y));
                    var vm = new GemViewModel(model);
                    var view = pool.Rent();
                    view.transform.position = WorldPosFromIndex(x, y + height + 2); // above
                    view.Bind(vm, SpriteForType(type));
                    boardVM.SetGem(x, y, vm);
                    vm.MoveTo(WorldPosFromIndex(x, y), 0.18f).Forget();
                }
            }

            await UniTask.Delay(200);

            // check new matches recursively
            var matches = MatchFinder.FindAllMatches(boardVM);
            if (matches.Count > 0)
            {
                await DestroyMatches(matches);
                await CollapseAndRefill();
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
