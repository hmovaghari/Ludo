# Ludo Console Game

This project is a console-based implementation of `Ludo` (Mensch / Pachisi-style board game) built with `C#` and `.NET 8`.

## Project Vision

The game is intended to run entirely in the terminal and support three main scenarios:

- Single-player mode with one human player and computer-controlled bots
- Network multiplayer mode with host/join support
- Automatic bot filling for any empty seats

## Planned Features

- Clean and readable console interface
- `Single Player` mode with one real player and AI-controlled opponents
- `Network Game` mode for hosting and joining multiplayer sessions
- Turn management, dice rolling, token movement, and core Ludo rules
- Bot logic for replacing missing players in local or network matches
- Maintainable project structure for future expansion and refactoring

## Game Modes

### 1. Single Player

In this mode, one player competes against bots. The bots are expected to make simple gameplay decisions based on dice results and board state, such as:

- bringing a token out of base when allowed
- prioritizing captures when possible
- advancing tokens that are closer to the home path

### 2. Network Multiplayer

In this mode, one player hosts a match and other players join remotely. such as:

- the host chooses how many human players will join
- remote players connect using `IP` and `Port`
- any remaining empty slots are filled with bots automatically

## Core Ludo Rules

The game is intended to follow the standard rules of Ludo:

- each player has 4 tokens
- a token usually needs a roll of `6` to leave the base
- players take turns rolling the dice
- rolling `6` may allow an extra turn or bringing a token into play
- landing on an opponent's token sends that token back to base
- the objective is to move all 4 tokens into the final home area

## Project Structure

- `Ludo/Ludo/Program.cs`: application entry point and main gameplay implementation
- `Ludo/Ludo/Ludo.csproj`: `.NET 8` project file
- `Ludo/Ludo.sln`: Visual Studio solution file

## Requirements

- `.NET 8 SDK`
- A terminal with proper `UTF-8` support

## Build and Run

```bash
dotnet build Ludo/Ludo.sln
dotnet run --project Ludo/Ludo/Ludo.csproj
```

## How to Play

1. Start the game and choose either `Single Player`, `Host Network Game`, or `Join Network Game`.
2. On your turn, roll the dice and review which of your tokens can move.
3. If you roll a `6`, you can usually bring a token out of base or move a token already on the board.
4. Choose the token you want to move based on the dice result and current board situation.
5. If your token lands on an opponent's token, that opponent's token is sent back to base.
6. Keep moving your tokens around the board and into the final home path.
7. The first player to get all 4 tokens into the home area wins the match.

## Current Status

At the moment, this repository is prepared as the foundation for the `Ludo` project, and this `README` has been updated to reflect the intended direction of development. The next steps can include implementing the complete game loop, bot behavior, and network gameplay flow.
