using System.Text.Encodings.Web;
using TentacionSana.Application.Deliveries;

namespace TentacionSana.Web;

public static class ReceiptGalleryPage
{
    public static string Render(Guid orderId, long number, string customer, string? point, IReadOnlyList<EvidenceItem> evidence)
    {
        static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        var photos = string.Join("", evidence.Select((item, index) => $"""
            <figure><figcaption>Boleta {index + 1} de {evidence.Count} · {Encode(item.FileName)}</figcaption>
            <a href="/media/delivery-evidence/{item.Id:N}" target="_blank" rel="noopener noreferrer" aria-label="Abrir boleta {index + 1} a tamaño completo"><img src="/media/delivery-evidence/{item.Id:N}" alt="Boleta {index + 1} del pedido {number}" loading="{(index == 0 ? "eager" : "lazy")}" /></a></figure>
            """));
        return $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Boletas del pedido #{{number}}</title>
            <style>body{margin:0;padding:1rem;background:#f7f6f0;color:#24332a;font:1rem/1.5 'Segoe UI',sans-serif}header{max-width:70rem;margin:auto}h1{font-size:1.3rem;margin:0}p{margin:.5rem 0}a{color:#216d34}main{max-width:70rem;margin:1rem auto}figure{margin:0 0 1.5rem}figcaption{padding:.6rem;background:#e9f0e5;overflow-wrap:anywhere}img{display:block;max-width:100%;max-height:80vh;width:auto;height:auto;margin:.6rem auto;background:white}a:focus-visible{outline:3px solid #267537;outline-offset:3px}</style></head>
            <body><header><h1>Boletas · Pedido #{{number}}</h1><p>{{Encode(customer)}} · {{Encode(point)}}</p><p>Pulsa la foto para abrirla a tamaño completo.</p></header><main>{{(evidence.Count == 0 ? $"<p>Este pedido no tiene boletas adjuntas. <a href='/app/pedidos/{orderId}' target='_top'>Abrir pedido para adjuntar la boleta</a>.</p>" : photos)}}</main></body></html>
            """;
    }
}
