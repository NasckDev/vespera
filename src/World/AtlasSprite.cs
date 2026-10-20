using Godot;

namespace Vespera.World;

/// <summary>
/// Sprite da v2 por nome (spec 01 §9.1): textura do recorte, <c>offset = -âncora</c> (pivô no pé) e
/// <c>scale = SpriteScale / k</c>, então o nó fica na posição do pé em u. Para espelhar, use <c>scale.x = -1</c> num
/// <see cref="Node2D"/> pai (gira em torno da âncora, como <c>ctx.scale(-1, 1)</c>). Trocar <see cref="Key"/> só
/// troca a textura/offset; não aloca se a chave for a mesma.
/// </summary>
[GlobalClass]
public partial class AtlasSprite : Sprite2D
{
    private string _key = string.Empty;
    private float _spriteScale = 1f;

    /// <summary>Nome do recorte (ex.: <c>c_sword_front</c>, <c>shadow</c>, <c>cnev_f</c>).</summary>
    [Export]
    public string Key
    {
        get => _key;
        set
        {
            if (_key == value)
            {
                return;
            }

            _key = value;
            if (IsNodeReady())
            {
                Apply();
            }
        }
    }

    /// <summary>Escala extra do objeto (<c>o.s</c> do protótipo), multiplicada por 1/k.</summary>
    [Export]
    public float SpriteScale
    {
        get => _spriteScale;
        set
        {
            _spriteScale = value;
            if (IsNodeReady())
            {
                Apply();
            }
        }
    }

    /// <summary>Densidade (px por u) do recorte atual; 1 sem recorte.</summary>
    public float Density { get; private set; } = 1f;

    public override void _Ready() => Apply();

    private void Apply()
    {
        if (_key.Length == 0 || !AtlasLibrary.TryGet(_key, out var frame))
        {
            Texture = null;
            return;
        }

        Texture = frame.Texture;
        Centered = false;
        Offset = -frame.Anchor;
        Density = frame.Density;
        Scale = Vector2.One * (_spriteScale / frame.Density);
    }
}
