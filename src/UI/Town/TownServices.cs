using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.State;
using Vespera.UI.Common;
using Vespera.UI.Screens;

namespace Vespera.UI.Town;

/// <summary>
/// Diálogos, lojas e serviços da cidade da v2 (<c>world.js:199-319</c> + <c>extra.js:94,97,128</c>), ligados aos
/// <c>InteractionRequested</c> do World: Odete, Mural, Brás (melhorar arma, nova arma, receitas, vender sobras e
/// Estilhaços), Rua dos Ofícios, portais de Brecha, Lúcia e a cama da Estalagem, Gaspar (infusões), Vesna (afinar Ecos,
/// Diário), Ivo (poções) e Abel (relíquias). Falas verbatim de <c>npcs.json</c>; preços pelo <see cref="GameState"/>
/// (×0,9, ×1,35 em invasão, arredondamento JS). Tudo usa a <see cref="DialogScreen"/>, como na v2 (spec 02 §4.2).
/// </summary>
public sealed class TownServices(GameUi ui)
{
    private static GameState State => GameManager.Instance.State;

    private static GameDatabase Db => Database.Db;

    /// <summary>Despacha um pedido de interação. Devolve false para tipos que não são da Interface.</summary>
    public bool Handle(string kind, string id)
    {
        switch (kind)
        {
            case "talkClerk":
                TalkClerk();
                return true;
            case "openBoard":
                ui.OpenBoard();
                return true;
            case "talkSmith":
                TalkSmith();
                return true;
            case "oficiosDialog":
                OficiosDialog();
                return true;
            case "portalDialog":
                PortalDialog(id);
                return true;
            case "talkInn":
                TalkInn();
                return true;
            case "restDialog":
                RestDialog();
                return true;
            case "talkGaspar":
                TalkGaspar();
                return true;
            case "talkVesna":
                TalkVesna();
                return true;
            case "talkIvo":
                TalkIvo();
                return true;
            case "talkAbel":
                TalkAbel();
                return true;
            default:
                return false;
        }
    }

    // --- Guilda: Odete (world.js:205-216 + extra.js:128) ---

    public void TalkClerk()
    {
        var state = State;
        GameUi.CountOdeteTalk(state); // qc.odete = true (extra.js:128)

        var options = new List<DialogChoice>();
        if (state.Save.CarriedLanterns.Count > 0)
        {
            var count = state.Save.CarriedLanterns.Count;
            options.Add(new DialogChoice($"Entregar lanternas encontradas ({count})", Run: () =>
            {
                var names = string.Join(", ", state.Save.CarriedLanterns);
                var (lumes, _) = state.DeliverLanterns();
                GameManager.Instance.SaveGame();
                Say("Odete", Line("odete", "delivered").Replace("{names}", names, StringComparison.Ordinal).Replace("{lumes}", lumes.ToString(), StringComparison.Ordinal));
            }));
        }

        if (state.HasLostLantern)
        {
            var rescue = Db.Services["odete_rescue"];
            options.Add(new DialogChoice(rescue.Label, $"{state.CostLumes(rescue.Cost.Lumes)} Lumes", () =>
            {
                if (Buy(rescue) == PurchaseResult.Ok)
                {
                    ui.RescuePaid = true;
                    EventBus.Instance.EmitSignal(EventBus.SignalName.LanternRescueRequested, state.LanternRules.OdeteRescueDelay);
                    Say("Odete", Line("odete", "rescue"));
                }
            }, !state.CanPay(rescue.Cost) || ui.RescuePaid));
        }

        options.Add(new DialogChoice("Como funcionam as Brechas?", Run: () => Say("Odete", Line("odete", "aboutBrechas"),
        [
            new DialogChoice("E os monstros?", Run: () => Say("Odete", Line("odete", "aboutMonsters"))),
            new DialogChoice("Entendi"),
        ])));
        options.Add(new DialogChoice("Sobre os Selos", Run: () => Say("Odete", SealsText())));
        options.Add(new DialogChoice("Até logo"));

        var greeting = state.HasLostLantern ? Line("odete", "lostLantern") : Pick("odete", "greetings");
        Say("Odete", greeting, options);
    }

    private static string SealsText()
    {
        var state = State;
        var rank = state.RankDef;
        var next = state.RankIndex + 1 < Db.Ranks.Count ? Db.Ranks[state.RankIndex + 1] : null;
        var nextText = next is null ? string.Empty : Line("odete", "aboutSealsNext")
            .Replace("{xp}", (next.Xp - state.Save.Xp).ToString(), StringComparison.Ordinal)
            .Replace("{nextRank}", next.Id, StringComparison.Ordinal);
        return Line("odete", "aboutSeals")
            .Replace("{rank}", rank.Id, StringComparison.Ordinal)
            .Replace("{title}", rank.Title, StringComparison.Ordinal)
            .Replace("{next}", nextText, StringComparison.Ordinal);
    }

    // --- Forja do Brás (world.js:223-255, extra.js:94) ---

    public void TalkSmith()
    {
        var state = State;
        if (state.Invasion)
        {
            Say("Brás", Line("bras", "invasion"));
            return;
        }

        var hour = SeasonCalendar.HourOfDay(state.Save.ClockMinutes);
        var greeting = hour >= 23 || hour < 5 ? Line("bras", "lateNight") : Pick("bras", "greetings");
        var quote = state.ScrapQuote();
        var shards = state.Shards;
        Say("Brás", greeting,
        [
            new DialogChoice("Melhorar armas", Run: SmithUpgrade),
            new DialogChoice("Forjar nova arma", Run: SmithUnlock),
            new DialogChoice("Forjar equipamento", Run: SmithGear),
            new DialogChoice("Vender sobras de Brecha", quote.IsEmpty ? "Nada para vender" : $"{quote.Count} itens sem uso em receitas · {quote.Lumes} Lumes", () =>
            {
                state.SellScraps(); // "+N Lumes" aparece na caixa de loot
                UiSound.Coin();
                GameManager.Instance.SaveGame();
                TalkSmith();
            }, quote.IsEmpty),
            new DialogChoice("Vender Estilhaços", $"{shards} × {Db.Rules.Economy.ShardSellPrice} Lumes", () =>
            {
                state.SellShards(); // coin toca sozinho (FragmentsChanged, D35)
                GameManager.Instance.SaveGame();
                TalkSmith();
            }, shards <= 0),
            new DialogChoice("Sair"),
        ]);
    }

    public void SmithUpgrade()
    {
        var state = State;
        var options = new List<DialogChoice>();
        foreach (var weapon in Db.WeaponOrder)
        {
            var level = state.Save.WeaponLevel(weapon.Id);
            if (level == 0)
            {
                continue;
            }

            var icon = UiItems.WeaponIcon(state, weapon.Id);
            if (state.Upgrades.WeaponUpgrade(weapon.Id, level) is not { } cost)
            {
                options.Add(new DialogChoice($"{weapon.Name} +{level}", "Nível máximo", Disabled: true, Icon: icon));
                continue;
            }

            var id = weapon.Id;
            options.Add(new DialogChoice($"{weapon.Name}  +{level} → +{level + 1}", state.DescribeCost(cost), () =>
            {
                if (state.TryUpgradeWeapon(id) == PurchaseResult.Ok)
                {
                    UiSound.Play("boom");
                    EventBus.Instance.EmitSignal(EventBus.SignalName.CameraShakeRequested, 4f);
                    ui.Note($"{weapon.Name} agora é +{level + 1}", icon);
                    GameManager.Instance.SaveGame();
                }

                SmithUpgrade();
            }, !state.CanPay(cost), icon) { Tooltip = () => UiItems.Weapon(State, id) });
        }

        options.Add(new DialogChoice("Voltar", Run: TalkSmith));
        Say("Brás", Line("bras", "upgrade"), options);
    }

    /// <summary>
    /// Efetivo (<c>extra.js:94</c>): o Brás não forja arma de outra classe. Se faltar a arma da própria classe (save
    /// antigo), oferece o desbloqueio com o custo da v2.
    /// </summary>
    public void SmithUnlock()
    {
        var state = State;
        var cls = state.Save.ClassId;
        var options = new List<DialogChoice>();
        if (state.Save.WeaponLevel(cls) == 0 && Db.Weapons.TryGetValue(cls, out var own))
        {
            var cost = state.Upgrades.WeaponUnlock(cls);
            options.Add(new DialogChoice(own.Name, state.DescribeCost(cost), () =>
            {
                if (state.TryUnlockWeapon(cls) == PurchaseResult.Ok)
                {
                    UiSound.Play("boom");
                    ui.Toast($"Nova arma: {own.Name}", own.Desc, UiPalette.Ambar, own.Icon);
                    GameManager.Instance.SaveGame();
                }

                SmithUnlock();
            }, !state.CanPay(cost), own.Icon));
        }

        options.Add(new DialogChoice("Melhorar minha arma", Run: SmithUpgrade));
        options.Add(new DialogChoice("Voltar", Run: TalkSmith));
        Say("Brás", Line("bras", "unlockRefusal").Replace("{class}", state.EquipmentRules.ClassName(cls), StringComparison.Ordinal), options);
    }

    public void SmithGear()
    {
        var state = State;
        var options = new List<DialogChoice>();
        foreach (var recipe in Db.Recipes)
        {
            var gear = Db.Gear[recipe.Gear];
            var r = recipe;
            options.Add(new DialogChoice(gear.Name, state.DescribeCost(recipe.Cost), () =>
            {
                if (state.TryForge(r) == PurchaseResult.Ok)
                {
                    UiSound.Play("boom");
                    var rarity = UiItems.Rarity(gear.Rarity);
                    ui.Toast($"Forjado: {gear.Name}", $"{rarity.Name} · equipe no inventário [Tab]", UiItems.RarityColor(gear.Rarity), gear.Icon);
                    GameManager.Instance.SaveGame();
                }

                SmithGear();
            }, !state.CanPay(recipe.Cost), gear.Icon) { Tooltip = () => UiItems.Gear(State, r.Gear) });
        }

        options.Add(new DialogChoice("Voltar", Run: TalkSmith));
        Say("Brás", Line("bras", "gear"), options);
    }

    // --- Rua dos Ofícios (world.js:271-273) ---

    public void OficiosDialog()
    {
        var options = new List<DialogChoice>();
        foreach (var oficio in Db.Oficios)
        {
            var o = oficio;
            options.Add(new DialogChoice(o.Name, o.Desc, () => ui.GoToMap(o.Map)));
        }

        options.Add(new DialogChoice("Ficar na rua"));
        Say("Rua dos Ofícios", "Uma placa de madeira aponta para as oficinas do bairro. Lanternas pequenas marcam cada porta.", options);
    }

    // --- Portais (world.js:218-221 + extra.js:97) ---

    public void PortalDialog(string interactableId)
    {
        var spot = interactableId.Length > 0 && char.IsDigit(interactableId[^1]) ? interactableId[^1] - '0' : 0;
        var state = State;
        var brecha = BrechaTracker.Current?.OpenAt(spot);
        if (brecha is null)
        {
            // Sem anúncio da Cidade (PortalOpened): só a pergunta.
            Say("Brecha", "Uma Brecha pulsa no meio da rua.",
            [
                new DialogChoice("Entrar na Brecha", Run: () => EnterBrecha(spot)),
                new DialogChoice("Ainda não"),
            ]);
            return;
        }

        var rankDef = Db.Ranks[Math.Clamp(brecha.Rank, 0, Db.Ranks.Count - 1)];
        var bairro = BrechaTracker.Bairro(brecha.ThemeId);
        var speaker = $"Brecha · {bairro}";
        if (!state.Brechas.CanEnter(brecha.Rank, state.RankIndex))
        {
            Say(speaker, $"O Código da Véspera proíbe: esta Brecha é Selo {rankDef.Id} e o seu é {state.RankDef.Id}. Só é permitido entrar até um Selo acima do seu. Avise outro caçador ou suba de Selo antes que ela rompa.");
            return;
        }

        var season = Db.SeasonData.Seasons;
        var mod = string.Empty;
        foreach (var s in season)
        {
            if (s.Id == brecha.SeasonId)
            {
                mod = s.ModText;
            }
        }

        var warn = brecha.Rank > state.RankIndex ? $" Esta Brecha é Rank {rankDef.Id}, acima do seu Selo." : string.Empty;
        var lantern = state.Save.LostLantern is { } lost && lost.ThemeId == brecha.ThemeId ? " Sua Lanterna está lá dentro." : string.Empty;
        Say(speaker, $"{BrechaTracker.Title(brecha)}. {mod}{warn}{lantern}",
        [
            new DialogChoice("Entrar na Brecha", $"Rank {rankDef.Id}", () => EnterBrecha(spot)),
            new DialogChoice("Ainda não"),
        ]);
    }

    private static void EnterBrecha(int spot) => EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStartRequested, spot);

    // --- Estalagem: Lúcia e a cama (world.js:256-268) ---

    public void TalkInn()
    {
        var state = State;
        var potion = Db.Services["inn_potion"];
        var rest = Db.Services["inn_rest"];
        var greeting = state.Invasion ? Line("lucia", "invasion") : Pick("lucia", "greetings");
        Say("Lúcia", greeting,
        [
            new DialogChoice(potion.Label, $"{state.CostLumes(potion.Cost.Lumes)} Lumes · Ivo vende mais barato", () =>
            {
                Buy(potion); // a caixa de loot mostra o que entrou

                TalkInn();
            }, !state.CanPay(potion.Cost), "ic_potion"),
            new DialogChoice(rest.Label, $"{state.CostLumes(rest.Cost.Lumes)} Lumes", RestDialog),
            new DialogChoice("Ouvir um boato", Run: () => Say("Lúcia", State.Random.Pick(Db.Social.Rumors))),
            new DialogChoice("Sair"),
        ]);
    }

    public void RestDialog()
    {
        if (BrechaTracker.Current?.InsideTheme is not null)
        {
            return; // if(G.run) return;
        }

        var state = State;
        var rest = Db.Services["inn_rest"];
        Say("Repouso", "Dormir até a próxima véspera? Cura total, o dia avança e o jogo é salvo.",
        [
            new DialogChoice("Dormir", $"{state.CostLumes(rest.Cost.Lumes)} Lumes", () => ui.Sleep(rest), !state.CanPay(rest.Cost)),
            new DialogChoice("Agora não"),
        ]);
    }

    // --- Rua dos Ofícios: Gaspar, Vesna, Ivo, Abel (world.js:275-315) ---

    public void TalkGaspar()
    {
        var state = State;
        var options = new List<DialogChoice>();
        foreach (var weapon in Db.WeaponOrder)
        {
            if (state.Save.WeaponLevel(weapon.Id) == 0)
            {
                continue;
            }

            var id = weapon.Id;
            options.Add(new DialogChoice(UiItems.WeaponName(state, id), "Escolher infusão", () => GasparWeapon(id), Icon: UiItems.WeaponIcon(state, id)));
        }

        options.Add(new DialogChoice("Sair"));
        Say("Gaspar", Line("gaspar", "greetings"), options);
    }

    public void GasparWeapon(string weaponId)
    {
        var state = State;
        var weapon = Db.Weapons[weaponId];
        var owned = state.Save.Infusions.TryGetValue(weaponId, out var list) ? list : null;
        state.Save.ActiveInfusion.TryGetValue(weaponId, out var active);
        var options = new List<DialogChoice>();
        for (var i = 0; i < weapon.Elements.Count; i++)
        {
            var element = Db.Elements[weapon.Elements[i]];
            var icon = UiItems.ElementIcon(weaponId, i);
            if (active == element.Id)
            {
                options.Add(new DialogChoice($"{element.Name}  (ativa)", "Remover infusão", () =>
                {
                    State.SetActiveInfusion(weaponId, null);
                    GameManager.Instance.SaveGame();
                    GasparWeapon(weaponId);
                }, Icon: icon));
            }
            else if (owned?.Contains(element.Id) == true)
            {
                options.Add(new DialogChoice(element.Name, "Já forjada · ativar", () =>
                {
                    State.SetActiveInfusion(weaponId, element.Id);
                    UiSound.Play("magic");
                    GameManager.Instance.SaveGame();
                    GasparWeapon(weaponId);
                }, Icon: icon));
            }
            else
            {
                var cost = state.Upgrades.Infusion(element.Id);
                options.Add(new DialogChoice(element.Name, state.DescribeCost(cost), () =>
                {
                    if (State.TryBuyInfusion(weaponId, element.Id) == PurchaseResult.Ok)
                    {
                        UiSound.Play("boom");
                        EventBus.Instance.EmitSignal(EventBus.SignalName.CameraShakeRequested, 3f);
                        ui.Toast(UiItems.WeaponName(State, weaponId), $"Infusão {element.Name} forjada. Fraquezas aparecem em cada golpe.", UiPalette.FromHex(element.Color, UiPalette.Ambar), icon);
                        GameManager.Instance.SaveGame();
                    }

                    GasparWeapon(weaponId);
                }, !state.CanPay(cost), icon));
            }
        }

        options.Add(new DialogChoice("Voltar", Run: TalkGaspar));
        Say("Gaspar", Line("gaspar", "weapon"), options);
    }

    public void TalkVesna()
    {
        var state = State;
        var options = new List<DialogChoice>();
        foreach (var ecoId in state.Save.Ecos)
        {
            var eco = Db.Ecos[ecoId];
            var level = state.Save.EcoLevels.GetValueOrDefault(ecoId);
            if (state.Upgrades.EcoTuning(level) is not { } cost)
            {
                options.Add(new DialogChoice($"{eco.Name}  (afinado ×2)", $"Custo atual: {state.EcoCost(ecoId)} de Essência", Disabled: true, Icon: "i_eco"));
                continue;
            }

            var id = ecoId;
            var now = state.EcoCost(ecoId);
            options.Add(new DialogChoice($"Afinar {eco.Name}", $"{state.DescribeCost(cost)} · custo {now} → {now - Db.EcoData.Tuning.CostReductionPerLevel}", () =>
            {
                if (State.TryTuneEco(id) == PurchaseResult.Ok)
                {
                    UiSound.Play("eco");
                    ui.Toast($"{eco.Name} afinado", $"Agora custa {State.EcoCost(id)} de Essência.", new Color("#c06cf0"), "i_eco");
                    GameManager.Instance.SaveGame();
                }

                TalkVesna();
            }, !state.CanPay(cost), "i_eco"));
        }

        var book = Db.Services["vesna_book"];
        if (book.Gear is { } bookGear && !state.Save.OwnsGear(bookGear))
        {
            options.Add(new DialogChoice(book.Label, $"{state.DescribeCost(book.Cost)} · Ecos carregam mais rápido", () =>
            {
                Buy(book); // a caixa de loot mostra o que entrou

                TalkVesna();
            }, !state.CanPay(book.Cost), Db.Gear[bookGear].Icon) { Tooltip = () => UiItems.Gear(State, bookGear) });
        }

        if (state.Save.Ecos.Count == 0)
        {
            options.Add(new DialogChoice("Ainda não tenho Ecos", Disabled: true));
        }

        options.Add(new DialogChoice("Sair"));
        Say("Vesna", state.Save.Ecos.Count > 0 ? Line("vesna", "greetings") : Line("vesna", "noEcos"), options);
    }

    public void TalkIvo()
    {
        var state = State;
        var single = Db.Services["ivo_potion"];
        var box = Db.Services["ivo_box"];
        var distill = Db.Services["ivo_distill"];
        Say("Ivo", Line("ivo", "greetings"),
        [
            new DialogChoice(single.Label, $"{state.DescribeCost(single.Cost)} · você tem {state.Save.Potions}", () =>
            {
                Buy(single); // a caixa de loot mostra o que entrou

                TalkIvo();
            }, !state.CanPay(single.Cost), "ic_potion"),
            new DialogChoice(box.Label, state.DescribeCost(box.Cost), () =>
            {
                Buy(box); // a caixa de loot mostra o que entrou

                TalkIvo();
            }, !state.CanPay(box.Cost), "ic_potion2"),
            new DialogChoice(distill.Label, $"{state.DescribeCost(distill.Cost)} → 2 Poções", () =>
            {
                Buy(distill, sound: "magic"); // a caixa de loot mostra o que entrou

                TalkIvo();
            }, !state.CanPay(distill.Cost), "i_leaf"),
            new DialogChoice("Sair"),
        ]);
    }

    public void TalkAbel()
    {
        var state = State;
        var options = new List<DialogChoice>();
        foreach (var offer in Db.Shops.Relics)
        {
            var gear = Db.Gear[offer.Gear];
            var has = state.Save.OwnsGear(offer.Gear);
            var o = offer;
            options.Add(new DialogChoice(gear.Name, has ? "Você já tem" : state.DescribeCost(offer.Cost), () =>
            {
                if (State.TryBuyRelic(o) == PurchaseResult.Ok)
                {
                    UiSound.Open();
                    var rarity = UiItems.Rarity(gear.Rarity);
                    ui.Toast(gear.Name, $"{rarity.Name} · equipe no inventário [Tab]", UiItems.RarityColor(gear.Rarity), gear.Icon);
                    GameManager.Instance.SaveGame();
                }

                TalkAbel();
            }, has || !state.CanPay(offer.Cost), gear.Icon) { Tooltip = () => UiItems.Gear(State, o.Gear) });
        }

        options.Add(new DialogChoice("Sair"));
        Say("Abel", Line("abel", "greetings"), options);
    }

    // --- utilidades ---

    private void Say(string speaker, string text, IReadOnlyList<DialogChoice>? options = null) => ui.ShowDialog(speaker, text, options);

    /// <summary>Compra um serviço; toca <c>coin</c> (ou outro som) e salva (a v2 salva em compras, spec 01 §1.11).</summary>
    private static PurchaseResult Buy(ServiceDef service, string sound = "coin")
    {
        var result = State.TryBuyService(service);
        if (result == PurchaseResult.Ok)
        {
            UiSound.Play(sound);
            GameManager.Instance.SaveGame();
        }
        else
        {
            UiSound.Denied();
        }

        return result;
    }

    /// <summary>Primeira fala de <c>npcs.json</c> (<c>dialogue[npc][chave][0]</c>) com {name}/{class} preenchidos.</summary>
    public static string Line(string npc, string key) => Fill(Db.Npcs.Dialogue[npc][key][0]);

    /// <summary>Fala sorteada (<c>pick([...])</c>).</summary>
    private static string Pick(string npc, string key) => Fill(State.Random.Pick(Db.Npcs.Dialogue[npc][key]));

    public static string Fill(string text)
    {
        var state = State;
        var cls = state.Save.ClassId;
        var filled = text
            .Replace("{name}", state.Save.Name, StringComparison.Ordinal)
            .Replace("{class}", state.EquipmentRules.ClassName(cls), StringComparison.Ordinal)
            .Replace("{ultimate}", Db.Ultimates.TryGetValue(cls, out var ult) ? ult.Name : string.Empty, StringComparison.Ordinal);
        return KeyText.Expand(filled); // POLI-4: teclas do dispositivo e do remapeamento
    }
}
