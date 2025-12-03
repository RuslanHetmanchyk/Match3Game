using System.Collections.Generic;
using Match3.ViewModel;
using Match3.Model;
using UnityEngine;

namespace Match3.Services
{
    public static class MatchFinder
    {
        // простая реализация: по строкам и столбцам ищем подряд >=3 одинаковых типов
        public static List<GemViewModel> FindAllMatches(BoardViewModel board)
        {
            List<GemViewModel> matches = new List<GemViewModel>();
            // horizontal
            for (int y = 0; y < board.Height; y++)
            {
                int run = 1;
                for (int x = 1; x < board.Width; x++)
                {
                    var prev = board.GetGem(x - 1, y);
                    var cur = board.GetGem(x, y);
                    if (prev != null && cur != null && prev.Model.Type == cur.Model.Type)
                    {
                        run++;
                    }
                    else
                    {
                        if (run >= 3)
                        {
                            for (int k = 0; k < run; k++) matches.Add(board.GetGem(x - 1 - k, y));
                        }
                        run = 1;
                    }
                }
                if (run >= 3)
                    for (int k = 0; k < run; k++) matches.Add(board.GetGem(board.Width - 1 - k, y));
            }

            // vertical
            for (int x = 0; x < board.Width; x++)
            {
                int run = 1;
                for (int y = 1; y < board.Height; y++)
                {
                    var prev = board.GetGem(x, y - 1);
                    var cur = board.GetGem(x, y);
                    if (prev != null && cur != null && prev.Model.Type == cur.Model.Type)
                    {
                        run++;
                    }
                    else
                    {
                        if (run >= 3)
                        {
                            for (int k = 0; k < run; k++) matches.Add(board.GetGem(x, y - 1 - k));
                        }
                        run = 1;
                    }
                }
                if (run >= 3)
                    for (int k = 0; k < run; k++) matches.Add(board.GetGem(x, board.Height - 1 - k));
            }

            // unique
            var distinct = new HashSet<GemViewModel>(matches);
            return new List<GemViewModel>(distinct);
        }
    }
}
