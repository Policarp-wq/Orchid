namespace Orchid.Core;

public record Tone(int Integer, bool Half = false)
{
    public const float HalfTone = 0.5f;
    public float ToNum() => Integer + (Half ? HalfTone : 0);

    public static Tone FromNum(float num)
    {
        var integer = checked((int)MathF.Floor(num));
        var fraction = num - integer;
        if (fraction != 0 && fraction != HalfTone)
            throw new InvalidOperationException("Fractional part must be 0 or 0.5, but was " + fraction);

        return new Tone(integer, fraction == HalfTone);
    }
}
