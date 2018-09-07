﻿using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public partial class TennisModule
{
    enum Tournament
    {
        FrenchOpenRolandGarros, // Clay — reduced tie breaks, 40–40
        USOpenFlushingMeadows, // Hardcourt — tie breaks in every set, always deuce
        Wimbledon, // Lawn — reduced tie breaks, always deuce
    }

    sealed class SetScores
    {
        public int Player1Score { get; private set; }
        public int Player2Score { get; private set; }
        public SetScores() { Player1Score = 0; Player2Score = 0; }
        public SetScores(int player1Score, int player2Score) { Player1Score = player1Score; Player2Score = player2Score; }
        public int ScoreOf(bool player1) { return player1 ? Player1Score : Player2Score; }
        public SetScores Inc(bool player1) { return new SetScores(Player1Score + (player1 ? 1 : 0), Player2Score + (player1 ? 0 : 1)); }
        public override string ToString() { return string.Format("[{0}-{1}]", Player1Score, Player2Score); }
    }
    abstract class GameState
    {
        public static GameState GetInitial(bool isMensPlay, Tournament tournament) { return new GameStateScores(isMensPlay, tournament, new SetScores[] { new SetScores() }); }
        public abstract void Setup(TennisModule module, string name1, string name2);
    }
    sealed class GameStateScores : GameState
    {
        public SetScores[] Sets { get; private set; }

        // In normal play: [0,2] = 0–30; or [3,3] = 40–40; [4,3] = advantage Player 1; [4,4] = deuce
        // In tie break: normal tiebreak scores
        public int Player1Score { get; private set; }
        public int Player2Score { get; private set; }
        public bool IsTieBreak { get; private set; }

        public bool IsMensPlay { get; private set; }
        public Tournament Tournament { get; private set; }

        public GameStateScores(bool isMensPlay, Tournament tournament, SetScores[] sets) { IsMensPlay = isMensPlay; Tournament = tournament; Sets = sets; }
        public GameStateScores(bool isMensPlay, Tournament tournament, SetScores[] sets, int player1Score, int player2Score, bool tiebreak = false) { IsMensPlay = isMensPlay; Tournament = tournament; Sets = sets; Player1Score = player1Score; Player2Score = player2Score; IsTieBreak = tiebreak; }

        public bool IsPlayer1Serving
        {
            get
            {
                return (Sets.Sum(set => set.Player1Score + set.Player2Score) % 2 == 0) ^ (IsTieBreak && (Player1Score + Player2Score + 1) % 4 >= 2);
            }
        }
        public GameState PlayerScores(bool server)
        {
            var isPlayer1 = !(server ^ IsPlayer1Serving);
            var thisPlayer = isPlayer1 ? Player1Score : Player2Score;
            var otherPlayer = isPlayer1 ? Player2Score : Player1Score;

            // Does player win a game?
            if ((IsTieBreak && thisPlayer >= 6 && thisPlayer > otherPlayer) ||  // winning a tie break
                (!IsTieBreak && thisPlayer == 3 && otherPlayer < 3) ||  // winning from 40–0 to 40–30
                (!IsTieBreak && thisPlayer == 4 && otherPlayer == 3))   // winning from Advantage
            {
                // Does player win a set?
                var thisPlayerSet = isPlayer1 ? Sets.Last().Player1Score : Sets.Last().Player2Score;
                var otherPlayerSet = isPlayer1 ? Sets.Last().Player2Score : Sets.Last().Player1Score;
                if ((thisPlayerSet >= 5 && thisPlayerSet > otherPlayerSet) || IsTieBreak)
                {
                    // Does player win the match?
                    if (Sets.Take(Sets.Length - 1).Count(set => isPlayer1 ? set.Player1Score > set.Player2Score : set.Player2Score > set.Player1Score) + 1 >= (IsMensPlay ? 3 : 2))
                        return new GameStateVictory { Player1Wins = isPlayer1 };

                    // Just the set
                    return new GameStateScores(IsMensPlay, Tournament, Sets.Take(Sets.Length - 1).Concat(new[] { Sets.Last().Inc(isPlayer1), new SetScores() }).ToArray());
                }

                // Just the game.
                // Does this start a tie break?
                if (thisPlayerSet + 1 == 6 && otherPlayerSet == 6 && (Tournament == Tournament.USOpenFlushingMeadows || Sets.Length < (IsMensPlay ? 5 : 3)))
                    return new GameStateScores(IsMensPlay, Tournament, Sets.Take(Sets.Length - 1).Concat(new[] { Sets.Last().Inc(isPlayer1) }).ToArray(), 0, 0, tiebreak: true);
                return new GameStateScores(IsMensPlay, Tournament, Sets.Take(Sets.Length - 1).Concat(new[] { Sets.Last().Inc(isPlayer1) }).ToArray());
            }

            // Just a point. Are we going from Deuce to Advantage?
            if (thisPlayer == 4 && otherPlayer == 4 && !IsTieBreak)
                return new GameStateScores(IsMensPlay, Tournament, Sets, isPlayer1 ? 4 : 3, isPlayer1 ? 3 : 4);
            return new GameStateScores(IsMensPlay, Tournament, Sets, Player1Score + (isPlayer1 ? 1 : 0), Player2Score + (isPlayer1 ? 0 : 1), IsTieBreak);
        }

        private static readonly string[] ScoreNames = new[] { "0", "15", "30", "40" };
        public override string ToString()
        {
            if (IsTieBreak)
                return string.Format("{4}•P{0} {1} Tie break {2}-{3}", IsPlayer1Serving ? "1" : "2", string.Join(" ", Sets.Select(s => s.ToString()).ToArray()), Player1Score, Player2Score, IsMensPlay ? "M" : "W");
            if (Player1Score == 4 && Player2Score == 4 || (Player1Score == 3 && Player2Score == 3 && Tournament != Tournament.FrenchOpenRolandGarros))
                return string.Format("{2}•P{0} {1} Deuce", IsPlayer1Serving ? "1" : "2", string.Join(" ", Sets.Select(s => s.ToString()).ToArray()), IsMensPlay ? "M" : "W");
            if (Player1Score == 4)
                return string.Format("{2}•P{0} {1} Advantage Player 1", IsPlayer1Serving ? "1" : "2", string.Join(" ", Sets.Select(s => s.ToString()).ToArray()), IsMensPlay ? "M" : "W");
            if (Player2Score == 4)
                return string.Format("{2}•P{0} {1} Advantage Player 2", IsPlayer1Serving ? "1" : "2", string.Join(" ", Sets.Select(s => s.ToString()).ToArray()), IsMensPlay ? "M" : "W");
            return string.Format("{4}•P{0} {1} {2}-{3}", IsPlayer1Serving ? "1" : "2", string.Join(" ", Sets.Select(s => s.ToString()).ToArray()), ScoreNames[Player1Score], ScoreNames[Player2Score], IsMensPlay ? "M" : "W");
        }

        public override void Setup(TennisModule module, string name1, string name2)
        {
            module.ScoresGroup.SetActive(true);
            module.TrophyGroup.SetActive(false);
            module.TieBreak.SetActive(IsTieBreak);

            module.Main.material.mainTexture = module.Courts[(int) Tournament];
            if (IsTieBreak || Player1Score < 3 || Player2Score < 3 || (Player1Score == 3 && Player2Score == 3 && Tournament == Tournament.FrenchOpenRolandGarros))
            {
                module.GameScore1.SetActive(true);
                module.GameScore2.SetActive(false);
                module.GameScore1.transform.Find("ScoreBox1").Find("Score").GetComponent<TextMesh>().text = IsTieBreak ? Player1Score.ToString() : ScoreNames[Player1Score];
                module.GameScore1.transform.Find("ScoreBox2").Find("Score").GetComponent<TextMesh>().text = IsTieBreak ? Player2Score.ToString() : ScoreNames[Player2Score];
            }
            else
            {
                module.GameScore1.SetActive(false);
                module.GameScore2.SetActive(true);
                module.GameScore2.transform.Find("Score").GetComponent<TextMesh>().text =
                    Player1Score == 3 && Player2Score == 4 ? (Tournament == Tournament.FrenchOpenRolandGarros ? "Avantage\n" : "Advantage\n") + name2 :
                    Player1Score == 4 && Player2Score == 3 ? (Tournament == Tournament.FrenchOpenRolandGarros ? "Avantage\n" : "Advantage\n") + name1 :
                    (Tournament == Tournament.FrenchOpenRolandGarros ? "Égalité" : "Deuce");
            }

            for (int i = 0; i < 5; i++)
            {
                module.SetScoresGroup.transform.Find("SetScore" + (i + 1)).Find("ScoreBox1").Find("Score").GetComponent<TextMesh>().text = i < Sets.Length ? Sets[i].Player1Score.ToString() : "";
                module.SetScoresGroup.transform.Find("SetScore" + (i + 1)).Find("ScoreBox2").Find("Score").GetComponent<TextMesh>().text = i < Sets.Length ? Sets[i].Player2Score.ToString() : "";
            }
        }
    }
    sealed class GameStateVictory : GameState
    {
        public bool Player1Wins;

        public override void Setup(TennisModule module, string name1, string name2)
        {
            module.ScoresGroup.SetActive(false);
            module.TrophyGroup.SetActive(true);
            module.TrophyGroup.transform.Find("Trophy").GetComponent<MeshRenderer>().material.mainTexture = module.Trophies[Rnd.Range(0, module.Trophies.Length)];
            module.TrophyGroup.transform.Find("Winner").Find("Name").GetComponent<TextMesh>().text = Player1Wins ? name1 : name2;
        }

        public override string ToString()
        {
            return string.Format("Player {0} wins.", Player1Wins ? 1 : 2);
        }
    }
}
