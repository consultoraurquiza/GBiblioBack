// IImagenService.cs
public interface IImagenService
{
    Task<string> DescargarImagenDesdeUrl(string url, int libroId);
    Task<string> GuardarImagenSubida(IFormFile archivo, int libroId);
}

