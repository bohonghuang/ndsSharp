using ndsSharp.Core.Data;

namespace ndsSharp.Core.Conversion.Textures.Colors;

public abstract class ColorDeserializer
{
    public abstract int Size { get; }

    public abstract Color Deserialize(DataReader reader);
}


public abstract class ColorDeserializer<T> : ColorDeserializer where T : unmanaged
{
    public override unsafe int Size => sizeof(T);

    public override Color Deserialize(DataReader reader)
    {
        var value = reader.Read<T>();
        return ProvideColor(value);
    }

    public abstract Color ProvideColor(T value);
}
