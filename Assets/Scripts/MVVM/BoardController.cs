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
        [SerializeField] private int width = 7;
        [SerializeField] private int height = 7;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Transform gemParent;
        [SerializeField] private GemView gemPrefab;
        [SerializeField] private Sprite[] gemSprites; // map by GemType enum order (excluding bomb maybe)

        private readonly Dictionary<GemViewModel, GemView> vmToView = new Dictionary<GemViewModel, GemView>();


        private BoardViewModel boardVM;
        private GemPool pool;

        private void Awake()
        {
            boardVM = new BoardViewModel(width, height * 2);
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
            // 1️⃣ Немедленно запускаем анимацию уничтожения
            foreach (var m in matches)
            {
                m.MarkDestroy();
                vmToView.Remove(m);
        
                int x = m.Model.Position.x;
                int y = m.Model.Position.y;
                boardVM.Grid[x, y] = null;     // освобождаем слот СРАЗУ
            }

            // 2️⃣ Ждём время визуальной анимации уничтожения
            await UniTask.Delay((int)(GameConst.GemDestroyDelaySec * 1000));

            // 3️⃣ Когда анимация полностью закончилась — возвращаем view в пул
            foreach (var m in matches)
            {
                if (vmToView.TryGetValue(m, out var view))
                {
                    await UniTask.Delay((int)(GameConst.GemDestroySec * 1000));
                    pool.Return(view);
                }
            }
        }

        private GemView FindViewByVM(GemViewModel vm)
        {
            vmToView.TryGetValue(vm, out var view);
            return view;
        }

        [SerializeField] private float cascadeFallDuration = 0.10f;
        [SerializeField] private float cascadeStepDelay = 0.05f; // задержка между падениями элементов в колонке
        [SerializeField] private float cascadeStartDelay = 0.01f; // задержка перед каскадом после матча


        private async UniTask CollapseAndRefill()
        {
            int[] destroyCount = new int[width];

            // 1️⃣ Считаем пустоты после DestroyMatches
            for (int x = 0; x < width; x++)
            {
                int holes = 0;
                for (int y = 0; y < height; y++)
                {
                    if (boardVM.Grid[x, y] == null)
                        holes++;
                }

                destroyCount[x] = holes;
            }

            // 2️⃣ Сжимаем существующие гемы вниз (логически)
            for (int x = 0; x < width; x++)
            {
                int write = 0;

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
            }

            // 3️⃣ Создаём новые гемы СВЕРХУ СТОЛЬКО, СКОЛЬКО БЫЛО УДАЛЕНО
            for (int x = 0; x < width; x++)
            {
                for (int i = 0; i < destroyCount[x]; i++)
                {
                    int spawnY = height + i; // над верхом колонки

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

            // 4️⃣ Ждём start delay — разрушение уже идёт параллельно
            await UniTask.Delay((int)(cascadeStartDelay * 1000));

            // 5️⃣ Запускаем ПОШАГОВЫЙ каскад (stagger)
            List<UniTask> fallTasks = new List<UniTask>();

            for (int x = 0; x < width; x++)
                fallTasks.Add(ProcessColumnFall(x));

            await UniTask.WhenAll(fallTasks);

            // 6️⃣ Проверяем новые матчи
            var matches = MatchFinder.FindAllMatches(boardVM);

            if (matches.Count > 0)
            {
                await DestroyMatches(matches);
                await CollapseAndRefill();
            }
        }

        private async UniTask ProcessColumnFall(int x)
        {
            // 1️⃣ Находим ВЕСЬ диапазон элементов, включая наращенные сверху
            int topY = height - 1;

            // ищем highest spawnY
            while (topY + 1 < boardVM.Grid.GetLength(1) &&
                   boardVM.Grid[x, topY + 1] != null)
            {
                topY++;
            }

            // 2️⃣ Собираем ВСЕ гемы сверху вниз
            List<GemViewModel> gems = new List<GemViewModel>();

            for (int y = 0; y <= topY; y++)
            {
                var g = boardVM.Grid[x, y];
                if (g != null)
                    gems.Add(g);
            }

            // 3️⃣ Теперь эти гемы должны оказаться на позициях 0..gems.Count-1
            for (int i = 0; i < gems.Count; i++)
            {
                var gem = gems[i];

                int targetY = i;
                gem.Model.Position = new Vector2Int(x, targetY);

                Vector2 target = WorldPosFromIndex(x, targetY);

                // Запускаем падение
                gem.MoveTo(target, cascadeFallDuration).Forget();

                // Задержка между падениями
                await UniTask.Delay((int)(cascadeStepDelay * 1000));
            }

            // 4️⃣ Физически обновляем сетку
            for (int y = 0; y < gems.Count; y++)
                boardVM.Grid[x, y] = gems[y];

            // очищаем верх колонны
            for (int y = gems.Count; y <= topY; y++)
                boardVM.Grid[x, y] = null;
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