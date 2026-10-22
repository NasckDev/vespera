<div align="center">

<img src="docs/brand/key-art.png" alt="Véspera key art: a hunter and companion overlook Lumeira at dusk while a magenta Breach tears the sky" width="100%">

# Véspera

**A 2D pixel-art action RPG for desktop. Every dusk, Breaches open across Lumeira. Someone has to close them.**

[![CI](https://github.com/NasckDev/vespera/actions/workflows/ci.yml/badge.svg)](https://github.com/NasckDev/vespera/actions/workflows/ci.yml)
[![Godot 4.7 .NET](https://img.shields.io/badge/Godot-4.7.2%20.NET-478cbf?logo=godotengine&logoColor=white)](https://godotengine.org)
[![v2: in progress](https://img.shields.io/badge/v2-in%20progress-E64A8E)](#-a-new-version-is-coming)
[![Code: MIT](https://img.shields.io/badge/code-MIT-F2A541)](LICENSE)
[![Art: CC BY-NC-SA 4.0](https://img.shields.io/badge/art-CC%20BY--NC--SA%204.0-E64A8E)](LICENSE-ASSETS.md)

[**🌅 New version**](#-a-new-version-is-coming) · [**🏮 The game**](#-what-is-it) · [**🛠️ Build from source**](#️-build-from-source) · [**🤝 Contribute**](CONTRIBUTING.md) · [🇧🇷 Português](README.md)

</div>

---

## 🌅 A new version is coming

> **Véspera is being rebuilt from scratch.** The next version, **v2**, will be the game's **public beta**.

We played v0.1 end to end and were honest about it: the systems worked, but the game wasn't living up to its idea.
Characters looked like still images sliding around, maps let the player walk through walls, and the art mixed styles.
So we're starting over on solid ground:

- **16×16 tile world** where collision comes from the tiles themselves.
- **Real frame-by-frame animation** in 4 directions, plus **5 visual evolution stages** per class.
- **One pixel grid, one palette**, with art made for the game.
- The full **story campaign** from the [design document](docs/design/GDD.md), from the first Breach to the **Alvor** rank.
- **Solo story mode** and **co-op**: local first (2 players on one PC), then online (2–4).
- Three classes at launch (**Blade, Bow, Hammer**); Staff and Daggers right after.

The full plan (in Portuguese) is at [`docs/v2/PLANO-V2.md`](https://github.com/NasckDev/vespera/blob/v2/docs/v2/PLANO-V2.md) on the
[`v2`](https://github.com/NasckDev/vespera/tree/v2) branch. v0.1 is preserved at the [`v0.1-final`](https://github.com/NasckDev/vespera/tree/v0.1-final) tag.

## 🏮 What is it

Twenty years ago the sun set over Lumeira and didn't come back for a whole year: **the Year Without Dawn**. That's when the first
**Breaches** opened, rifts into the **Underside**, an inverted Lumeira made of everything the city lost. Hunters go in for
**Fragments**; the **Order of the Lantern** ranks them from **E** to **SSS**, and legend says whoever reaches **Alvor**
could bring the sun back for good. Nobody ever has.

You arrive with an E seal burned into your wrist and an old lantern inherited from someone you never met.

- **Breach rank = loot rank**: a D Breach drops D gear, and sometimes something from the rank above.
- **Rank exams and Guardians** gate your progress, not just XP.
- **Breakable monster parts** drop specific materials for the forge.
- **The Lantern**: die, and your Fragments stay inside the Breach until you go back for them. Your gear is never lost.

## 🛠️ Build from source

```bash
# Requirements: .NET 8 SDK, Godot 4.7.2 .NET, Git + Git LFS
git clone https://github.com/NasckDev/vespera.git && cd vespera
git lfs pull
git switch v2                     # or: git switch --detach v0.1-final
dotnet build Vespera.sln
$GODOT --path .                   # GODOT = path to Godot .NET
```

## 📜 License

Code: [MIT](LICENSE) · Art, maps, audio, lore and brand: [CC BY-NC-SA 4.0](LICENSE-ASSETS.md) · Fonts: SIL OFL 1.1.
