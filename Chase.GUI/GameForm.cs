﻿using Chase.Engine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chase.GUI
{
    public partial class GameForm : Form
    {
        Game game;
        GameType type;

        int depth = 2;

        int selectedFromTile = -1;
        int selectedToTile = -1;

        public GameForm()
        {
            InitializeComponent();

            game = new Game();
            game.OnSearchProgress += Game_OnSearchProgress;
            game.OnFoundBestMove += Game_OnFoundBestMove;

            type = GameType.NotStarted;

            InitializeGameGUI();
            RefreshBoard();
        }

        private void selfPlayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            type = GameType.ComputerSelfPlay;

            game.StartNew();
            game.BeginGetBestMove(depth);
        }

        private void Game_OnFoundBestMove(SearchResult result)
        {
            if (result.BestMove != null)
            {
                game.MakeMove(result.BestMove);
            }

            RefreshBoard(result.BestMove);

            Player winner = game.GetWinner();
            if (winner == Player.None)
            {
                // If it's a computer self-play game, always make a move. If the computer is playing a human, then it might have to make multiple moves in a row after a capture
                if ((type == GameType.ComputerSelfPlay) || (game.PlayerToMove == Player.Blue && computerPlaysBlueToolStripMenuItem.Checked) || (game.PlayerToMove == Player.Red && !computerPlaysBlueToolStripMenuItem.Checked))
                {
                    game.BeginGetBestMove(depth);
                }
            }
            else
            {
                game.SaveGameToFile("game." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
                MessageBox.Show(winner.ToString() + " wins!");
            }
        }

        private void Game_OnSearchProgress(SearchStatus status)
        {
            searchStatusLabel.Text = "best: " + status.BestMoveSoFar.BestMove.ToString() +
                " score: " + status.BestMoveSoFar.Score +
                " nps: " + status.NodesPerSecond.ToString("0") + 
                " pv: " + status.BestMoveSoFar.PrimaryVariation;
        }

        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TestForm test = new TestForm();
            test.ShowDialog();
        }

        private void newGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            type = GameType.ComputerVsHuman;

            game.StartNew();

            RefreshBoard();

            if (!computerPlaysBlueToolStripMenuItem.Checked)
            {
                game.BeginGetBestMove(depth);
            }
        }

        private void RefreshBoard()
        {
            RefreshBoard(null);
        }

        private void RefreshBoard(Move lastmove)
        {
            for (int i = 0; i < 81; i++)
            {
                int piece = game.Board[i];
                if (i == 40)
                {
                    SetButton(i, "CH", Color.Gray);
                }
                else if (piece == 0)
                {
                    SetButton(i, "", Color.Black);
                }
                else
                {
                    SetButton(i, Math.Abs(piece), piece > 0 ? Color.Blue : Color.Red);
                }
            }

            if (lastmove != null)
            {
                if (lastmove.FromIndex > 0)
                {
                    gamePanel.Controls["tile" + lastmove.FromIndex].BackColor = Color.LightYellow;
                }
                if (lastmove.ToIndex > 0)
                {
                    gamePanel.Controls["tile" + lastmove.ToIndex].BackColor = Color.LightYellow;
                }
            }

            // Show valid moves
            if (highlightValidMovesToolStripMenuItem.Checked && game.PlayerToMove == (computerPlaysBlueToolStripMenuItem.Checked ? Player.Red : Player.Blue))
            {
                List<Move> moves = game.GetAllMoves();
                if (moves.Count > 0 && moves[0].Increment > 0 && moves[0].FromIndex == -1)
                {
                    // Add points after a capture
                    foreach (Move move in moves)
                    {
                        gamePanel.Controls["tile" + move.ToIndex].Text += "+" + move.Increment;
                        gamePanel.Controls["tile" + move.ToIndex].BackColor = Color.LightGreen;
                    }
                }
                else if (selectedFromTile >= 0)
                {
                    // Find movement moves
                    foreach (Move move in moves)
                    {
                        if (move.FromIndex == selectedFromTile)
                        {
                            gamePanel.Controls["tile" + move.ToIndex].BackColor = Color.LightGreen;
                            gamePanel.Controls["tile" + move.FromIndex].BackColor = Color.Black;
                        }
                    }
                }
            }

            // Status information
            infoLabel.Text = game.PlayerToMove.ToString() + "'s Turn";
        }

        private void ClickTile(int index)
        {
            if (type == GameType.ComputerVsHuman && ((game.PlayerToMove == Player.Blue && !computerPlaysBlueToolStripMenuItem.Checked) || (game.PlayerToMove == Player.Red && computerPlaysBlueToolStripMenuItem.Checked)))
            {
                if (selectedFromTile >= 0)
                {
                    if (selectedFromTile == index)
                    {
                        // Unselect an already selected tile
                        selectedFromTile = -1;
                    }
                    else
                    {
                        // Select destination tile
                        selectedToTile = index;

                        List<Move> moves = game.GetAllMoves();
                        List<Move> options = moves.Where(x => x.FromIndex == selectedFromTile && x.ToIndex == selectedToTile && x.Increment > 0).ToList();
                        if (options.Count > 0)
                        {
                            // If we're transferring points to an adjacent piece
                            List<int> increments = options.Select(x => x.Increment).Distinct().ToList();

                            add1.Enabled = increments.Contains(1);
                            add2.Enabled = increments.Contains(2);
                            add3.Enabled = increments.Contains(3);
                            add4.Enabled = increments.Contains(4);
                            add5.Enabled = increments.Contains(5);

                            addPanel.Visible = true;
                        }
                        else
                        {
                            options = moves.Where(x => x.FromIndex == selectedFromTile && x.ToIndex == selectedToTile && x.Increment == 0).ToList();

                            if (options.Count > 0)
                            {
                                Move move = options.First();

                                MakeMove(move);
                            }
                            else
                            {
                                // Invalid from square?
                                selectedFromTile = -1;
                            }
                        }
                    }
                }
                else
                {
                    // Select a source tile
                    selectedFromTile = index;

                    // If the move we need to make is filling in points after a capture
                    List<Move> moves = game.GetAllMoves();
                    if (moves.Count > 0 && moves[0].Increment > 0)
                    {
                        Move move = moves.FirstOrDefault(x => x.ToIndex == selectedFromTile);

                        if (move != null)
                        {
                            MakeMove(move);
                        }
                    }
                }
            }

            RefreshBoard();
        }

        private void MakeMove(Move move)
        {
            game.MakeMove(move);

            RefreshBoard();

            selectedFromTile = -1;
            selectedToTile = -1;

            // If we're playing the computer, let the computer know it's his turn
            if (type == GameType.ComputerVsHuman)
            {
                if ((game.PlayerToMove == Player.Blue && computerPlaysBlueToolStripMenuItem.Checked) || (game.PlayerToMove == Player.Red && !computerPlaysBlueToolStripMenuItem.Checked))
                {
                    game.BeginGetBestMove(depth);
                }
            }
        }

        private void Button_Click(object sender, EventArgs e)
        {
            int index = int.Parse(((Button)sender).Name.Replace("tile", ""));
            ClickTile(index);
        }

        private void InitializeGameGUI()
        {
            double hexFactor = 0.2471264367816092;
            Point firstTile = new Point(40, 0);
            Point nextTile = firstTile;
            Point bottomright;
            Size size;
            
            for (int i = 0; i < 81; i++)
            {
                CreateButton(i, nextTile, out bottomright, out size);
                
                if (i == 8 || i == 26 || i == 44 || i == 62)
                {
                    nextTile = new Point(firstTile.X - size.Width / 2, bottomright.Y - (int)(size.Height * hexFactor));
                }
                else if (i == 17 || i == 35 || i == 53 || i == 71)
                {
                    nextTile = new Point(firstTile.X, bottomright.Y - (int)(size.Height * hexFactor));
                }
                else
                {
                    nextTile = new Point(bottomright.X, nextTile.Y);
                }
            }
        }

        private void CreateButton(int moveIndex, Point location, out Point lowerright, out Size size)
        {
            int space = 5;
            float scale = 0.5f;
            PointF[] pts = {
                new PointF(0 * scale + space, 43 * scale + space),
                new PointF(0 * scale + space, 131 * scale + space),
                new PointF(76 * scale + space, 174 * scale + space),
                new PointF(152 * scale + space, 131 * scale + space),
                new PointF(152 * scale + space, 43 * scale + space),
                new PointF(76 * scale + space, 0 * scale + space)
            };
            GraphicsPath polygon_path = new GraphicsPath(FillMode.Winding);
            polygon_path.AddPolygon(pts);
            Region polygon_region = new Region(polygon_path);

            Button button = new Button();
            button.Location = location;
            button.Name = "tile" + moveIndex;
            button.Size = new Size((int)pts[3].X, (int)pts[2].Y);
            button.TabIndex = 7;
            button.Text = moveIndex.ToString();
            button.UseVisualStyleBackColor = true;
            button.Font = new Font("Tahoma", 14.0f, FontStyle.Bold);
            button.ForeColor = Color.Red;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Region = polygon_region;
            button.SetBounds(button.Location.X, button.Location.Y, (int)pts[3].X + space * 2, (int)pts[2].Y + space * 2);
            button.Click += Button_Click;
            gamePanel.Controls.Add(button);

            lowerright = new Point(button.Location.X + button.Width - space * 2, button.Location.Y + button.Height - space * 2);
            size = new Size(button.Size.Width - space * 2, button.Size.Height - space * 2);
        }

        private void SetButton(int index, int value, Color team)
        {
            SetButton(index, value.ToString(), team);
        }

        private void SetButton(int index, string value, Color team)
        {
            gamePanel.Controls["tile" + index].Text = value;
            gamePanel.Controls["tile" + index].ForeColor = team;
            (gamePanel.Controls["tile" + index] as Button).UseVisualStyleBackColor = true;
        }

        private void add1_Click(object sender, EventArgs e)
        {
            int amount = int.Parse(((Button)sender).Name.Replace("add", ""));

            List<Move> options = game.GetAllMoves().Where(x => x.FromIndex == selectedFromTile && x.ToIndex == selectedToTile && x.Increment  == amount).ToList();
            if (options.Count > 0)
            {
                Move move = options.First();
                MakeMove(move);
            }

            addPanel.Visible = false;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            addPanel.Visible = false;
        }
    }
}
