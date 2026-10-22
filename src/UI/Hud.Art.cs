using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.HudKit;

namespace Vespera.UI;

/// <summary>
/// Ícones do HUD do designer (pacote v1 + 2 do v4, issues #7/#8; DECISIONS C2-D7): Supremo da classe, Eco equipado,
/// poção e esquiva na hotbar com a moldura de slot desenhada, e uma fileira de estados embaixo das barras que só mostra o
/// que está valendo agora (invasão, Brecha rompida, escudo dos i-frames, lento, veloz). Os ícones ficam pré-criados; os
/// sinais só ligam/desligam (nada é criado por quadro).
/// </summary>
public partial class Hud
{
    /// <summary>Estados com ícone e quem liga cada um.</summary>
    public static readonly string[] StatusIds = ["invasion", "rupture", "shield", "slow", "speed"];

    private readonly Dictionary<string, HotbarSlot> _statusSlots = [];
    private readonly HashSet<int> _rupturedBrechas = [];
    private HBoxContainer _statusRow = null!;
    private HotbarSlot _dodgeSlot = null!;
    private float _dashCooldown;
    private float _dashTotal = 1f;

    /// <summary>Slot da esquiva na hotbar.</summary>
    public HotbarSlot DodgeSlot => _dodgeSlot;

    /// <summary>Estado com o ícone à vista agora (para os testes).</summary>
    public bool IsStatusShown(string id) => _statusSlots.TryGetValue(id, out var slot) && slot.Visible;

    private Control BuildArtStatusRow()
    {
        _statusRow = Ui.Row(2);
        foreach (var id in StatusIds)
        {
            var slot = new HotbarSlot { Name = $"Status_{id}", CustomMinimumSize = new Vector2(11, 11), FramedIcon = UiIcons.Hud($"status_{id}"), Visible = false };
            _statusSlots[id] = slot;
            _statusRow.AddChild(slot);
        }

        _statusRow.Visible = false;
        return _statusRow;
    }

    private HotbarSlot BuildDodgeSlot()
    {
        _dodgeSlot = new HotbarSlot { Name = "DodgeSlot", IconKey = "c_dag_side", IconScale = 0.7f, FramedIcon = UiIcons.Hud("dodge") };
        _dodgeSlot.SetKey("Esp", UiPalette.Dim);
        return _dodgeSlot;
    }

    private void ConnectArt(EventBus bus)
    {
        bus.PlayerStatusChanged += OnPlayerStatus;
        bus.BrechaStatusChanged += OnBrechaStatus;
        bus.InvasionStateChanged += OnArtInvasion;
    }

    private void DisconnectArt(EventBus bus)
    {
        bus.PlayerStatusChanged -= OnPlayerStatus;
        bus.BrechaStatusChanged -= OnBrechaStatus;
        bus.InvasionStateChanged -= OnArtInvasion;
    }

    private void StepArt(float dt)
    {
        if (_dashCooldown > 0f)
        {
            _dashCooldown = Mathf.Max(0f, _dashCooldown - dt);
            _dodgeSlot.VeilRatio = _dashCooldown / _dashTotal;
        }
    }

    private void OnDashCooldown(float remaining, float total)
    {
        _dashCooldown = remaining;
        _dashTotal = Mathf.Max(0.01f, total);
        _dodgeSlot.VeilRatio = remaining / _dashTotal;
    }

    /// <summary>Energia abaixo do custo da esquiva: ícone apagado.</summary>
    private void UpdateDodgeEnergy(float current) => _dodgeSlot.IconAlpha = current >= (float)Core.Combat.PlayerCombatState.DashCost ? 1f : 0.4f;

    /// <summary>Ícone do Supremo da classe (o miolo do ícone, sem a moldura quadrada, para o botão redondo).</summary>
    private static Texture2D? UltimateArt(string classId) => UiIcons.HudInner($"ultimate_{classId}");

    /// <summary>Ícone do Eco equipado (Abissal, Ceifador, Uivo, Arauto), ou null.</summary>
    private static Texture2D? EcoArt(string? ecoId) => ecoId is { Length: > 0 } ? UiIcons.Hud($"eco_{ecoId}") : null;

    private void OnPlayerStatus(string status, bool active) => SetStatus(status, active);

    private void OnArtInvasion(bool active) => SetStatus("invasion", active);

    private void OnBrechaStatus(int brechaId, int status)
    {
        if (status == (int)Core.World.BrechaStatus.Ruptured)
        {
            _rupturedBrechas.Add(brechaId);
        }
        else
        {
            _rupturedBrechas.Remove(brechaId);
        }

        SetStatus("rupture", _rupturedBrechas.Count > 0);
    }

    private void SetStatus(string id, bool active)
    {
        if (!_statusSlots.TryGetValue(id, out var slot) || slot.Visible == active)
        {
            return;
        }

        slot.Visible = active;
        var any = false;
        foreach (var s in _statusSlots.Values)
        {
            any |= s.Visible;
        }

        _statusRow.Visible = any;
    }
}
