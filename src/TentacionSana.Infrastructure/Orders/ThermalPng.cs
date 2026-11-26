using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace TentacionSana.Infrastructure.Orders;

// Codec limitado a PNG, sin dependencias gráficas del sistema operativo.
// QuestPDF produce PNG de 8 bits RGB/RGBA; también se aceptan PNG térmicos de 1 bit.
public static class ThermalPng
{
    public static byte[] Monochrome(byte[] png, int width, float paperWidthMm, float printableWidthMm)
    {
        var image = Decode(png);
        var fullWidth = (int)Math.Round(width * paperWidthMm / printableWidthMm);
        var height = Math.Max(1, (int)Math.Round((double)image.Height * fullWidth / image.Width));
        if (width is < 128 or > 1024 || height > 30000) throw new ArgumentException("La nota supera las dimensiones térmicas permitidas.");
        var left = (fullWidth - width) / 2;
        var stride = (width + 7) / 8;
        var raw = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var sy = Math.Min(image.Height - 1, (int)((long)y * image.Height / height));
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Min(image.Width - 1, (int)((long)(x + left) * image.Width / fullWidth));
                var index = (sy * image.Width + sx) * 4;
                var alpha = image.Rgba[index + 3];
                var luminance = (299 * image.Rgba[index] + 587 * image.Rgba[index + 1] + 114 * image.Rgba[index + 2]) / 1000;
                luminance = (luminance * alpha + 255 * (255 - alpha)) / 255;
                if (luminance >= 180) raw[y * (stride + 1) + 1 + x / 8] |= (byte)(1 << (7 - x % 8));
            }
        }
        return Encode(width, height, raw);
    }
    public static (int Width, int Height, byte[] Rgba) Decode(byte[] png)
    {
        if (png.Length < 33 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new ArgumentException("El archivo no es un PNG válido.");
        var width = 0; var height = 0; var depth = 0; var type = 0;
        using var compressed = new MemoryStream();
        byte[]? palette = null; byte[]? transparency = null;
        for (var position = 8; position + 12 <= png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position, 4));
            if (length < 0 || length > png.Length - position - 12) throw new ArgumentException("PNG incompleto.");
            var name = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, length);
            if (name == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]); height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                depth = data[8]; type = data[9];
                if (width < 1 || height < 1 || (long)width * height > 24_000_000 || data[12] != 0)
                    throw new ArgumentException("Dimensiones PNG no admitidas o imagen entrelazada.");
            }
            else if (name == "IDAT") compressed.Write(data);
            else if (name == "PLTE") palette = data.ToArray();
            else if (name == "tRNS") transparency = data.ToArray();
            position += length + 12;
            if (name == "IEND") break;
        }
        var channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new ArgumentException("Formato PNG no admitido.") };
        if (width == 0 || depth != 8 && !(type is 0 or 3 && depth is 1 or 2 or 4))
            throw new ArgumentException("Profundidad PNG no admitida.");
        var stride = checked((width * channels * depth + 7) / 8);
        var bytesPerPixel = Math.Max(1, (channels * depth + 7) / 8);
        var rows = new byte[checked((stride + 1) * height)];
        compressed.Position = 0;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress)) zlib.ReadExactly(rows);
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = rows[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                var a = x >= bytesPerPixel ? pixels[y * stride + x - bytesPerPixel] : 0;
                var b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                var c = y > 0 && x >= bytesPerPixel ? pixels[(y - 1) * stride + x - bytesPerPixel] : 0;
                var predictor = filter switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a, b, c), _ => throw new ArgumentException("Filtro PNG no válido.") };
                pixels[y * stride + x] = unchecked((byte)(rows[y * (stride + 1) + x + 1] + predictor));
            }
        }
        var rgba = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var dest = (y * width + x) * 4;
            var source = y * stride + x * channels;
            var sample = depth == 8 ? pixels[source] : (pixels[y * stride + x * depth / 8] >> (8 - depth - x * depth % 8)) & ((1 << depth) - 1);
            if (type == 3)
            {
                if (palette is null || sample * 3 + 2 >= palette.Length) throw new ArgumentException("Paleta PNG incompleta.");
                rgba[dest] = palette[sample * 3]; rgba[dest + 1] = palette[sample * 3 + 1]; rgba[dest + 2] = palette[sample * 3 + 2];
                rgba[dest + 3] = transparency is not null && sample < transparency.Length ? transparency[sample] : (byte)255;
            }
            else if (type is 0 or 4)
            {
                var gray = (byte)(sample * 255 / ((1 << depth) - 1));
                rgba[dest] = rgba[dest + 1] = rgba[dest + 2] = gray;
                rgba[dest + 3] = type == 4 ? pixels[source + 1] : (byte)255;
            }
            else
            {
                rgba[dest] = pixels[source]; rgba[dest + 1] = pixels[source + 1]; rgba[dest + 2] = pixels[source + 2];
                rgba[dest + 3] = type == 6 ? pixels[source + 3] : (byte)255;
            }
        }
        return (width, height, rgba);
    }
    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c; var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
    private static byte[] Encode(int width, int height, byte[] rows)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 1; // PNG grayscale de 1 bit: únicamente negro y blanco.
        Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) zlib.Write(rows);
        Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []);
        return output.ToArray();
    }
    private static void Chunk(Stream output, string name, byte[] data)
    {
        var type = Encoding.ASCII.GetBytes(name); var size = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, data.Length); output.Write(size); output.Write(type); output.Write(data);
        var crc = 0xffffffffu;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(size, ~crc); output.Write(size);
    }
}
