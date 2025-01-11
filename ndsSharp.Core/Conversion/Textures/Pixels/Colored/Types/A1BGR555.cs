using ndsSharp.Core.Conversion.Textures.Colors.Types;
using ndsSharp.Core.Data;

namespace ndsSharp.Core.Conversion.Textures.Pixels.Colored.Types;

public class A1BGR555 : PixelDeserializer<ushort>
{
    public override IPixel[] Deserialize(DataReader reader, int pixelCount)
    {
        var pixels = new IPixel[pixelCount];
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            var value = reader.Read<ushort>();
            pixels[pixelIndex] = ProvidePixel(value);
        }
        
        return pixels;
    }

    protected override IPixel ProvidePixel(ushort value)
    {
        var deserializer = new BGR555();
        var color = deserializer.ProvideColor(value);
        color.A = (byte) (value >> 15 == 1 ? 255 : 0);
        return new ColoredPixel(color);
    }
}