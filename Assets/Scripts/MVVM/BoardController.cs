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

        private readonly Dictionary<GemViewModel, GemView> vmToView = new Dictionary<GemViewModel, GemView>();


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
                    vmToView[vm] = view;
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
            // 1️⃣ Запускаем анимацию уничтожения у всех сразу
            foreach (var m in matches)
            {
                m.MarkDestroy();
            }

            // 2️⃣ Ждём фиксированную длительность (анимация GEM view занимает 0.25 сек)
            await UniTask.Delay(260);

            // 3️⃣ После того как ВСЕ проиграли анимацию, чистим VM + возвращаем все View в pool
            foreach (var m in matches)
            {
                int x = m.Model.Position.x;
                int y = m.Model.Position.y;

                if (vmToView.TryGetValue(m, out var v))
                {
                    pool.Return(v);
                    vmToView.Remove(m);
                }

                boardVM.Grid[x, y] = null;
            }
        }

        private GemView FindViewByVM(GemViewModel vm)
        {
            vmToView.TryGetValue(vm, out var view);
            return view;
        }

        [SerializeField] private float cascadeStaggerDelay = 0.05f;

        private async UniTask CollapseAndRefill()
        {
            for (int x = 0; x < width; x++)
            {
                int write = 0;

                // 🟦 1. Логическое сжатие (без анимации)
                for (int y = 0; y < height; y++)
                {
                    var g = boardVM.Grid[x, y];
                    if (g != null)
                    {
                        if (y != write)
                        {
                            boardVM.Grid[x, write] = g;
                            boardVM.Grid[x, y] = null;

                            g.Model.Position = new Vector2Int(x, write);
                        }

                        write++;
                    }
                }

                // 🟦 2. Анимируем каскад СТАГГЕРОМ
                int staggerIndex = 0;

                for (int y = 0; y < write; y++)
                {
                    var g = boardVM.Grid[x, y];
                    if (g == null) continue;

                    if (vmToView.TryGetValue(g, out var view))
                    {
                        float delay = cascadeStaggerDelay * staggerIndex;
                        var target = WorldPosFromIndex(x, y);

                        // запустить MoveTo с задержкой (не дожидаемся!)
                        AnimateWithStagger(g, target, 0.15f, delay).Forget();

                        staggerIndex++;
                    }
                }

                // 🟦 3. Добавление новых (тоже со stagger)
                for (int y = write; y < height; y++)
                {
                    var type = GetSafeRandomType(x, y);
                    var model = new GemModel(type, new Vector2Int(x, y));
                    var vm = new GemViewModel(model);

                    boardVM.Grid[x, y] = vm;

                    var view = pool.Rent();
                    view.transform.position = WorldPosFromIndex(x, y + height + 2);
                    view.Bind(vm, SpriteForType(type));
                    vmToView[vm] = view;

                    float delay = cascadeStaggerDelay * (staggerIndex++);

                    AnimateWithStagger(vm, WorldPosFromIndex(x, y), 0.20f, delay).Forget();
                }
            }

            // Ждём максимальный потенциальный stagger
            int maxHeight = height;
            await UniTask.Delay((int)((maxHeight * cascadeStaggerDelay + 0.25f) * 1000));

            // 🟦 4. Проверяем продолжение каскада
            var matches = MatchFinder.FindAllMatches(boardVM);
            if (matches.Count > 0)
            {
                await DestroyMatches(matches);
                await CollapseAndRefill();
            }
        }

        private async UniTask AnimateWithStagger(GemViewModel vm, Vector2 target, float duration, float delay)
        {
            if (delay > 0)
                await UniTask.Delay((int)(delay * 1000));

            await vm.MoveTo(target, duration);
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