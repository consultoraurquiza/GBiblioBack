public class ImagenService : IImagenService
{
    private readonly IWebHostEnvironment _env;

    public ImagenService(IWebHostEnvironment env) { _env = env; }

    public async Task<string> DescargarImagenDesdeUrl(string url, int libroId)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode) return null;

        var contentStream = await response.Content.ReadAsStreamAsync();
        return await GuardarStream(contentStream, libroId, "google");
    }

    public async Task<string> GuardarImagenSubida(IFormFile archivo, int libroId)
    {
        if (archivo == null || archivo.Length == 0) return null;
        return await GuardarStream(archivo.OpenReadStream(), libroId, "subida");
    }

    private async Task<string> GuardarStream(Stream stream, int libroId, string origen)
    {
        // Ruta absoluta: C:/.../wwwroot/portadas/libro_17_google.jpg
        var portadasPath = Path.Combine(_env.WebRootPath, "portadas");
        var fileName = $"libro_{libroId}_{origen}.jpg"; 
        var fullPath = Path.Combine(portadasPath, fileName);

        using var fileStream = new FileStream(fullPath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        // Devolvemos la URL relativa para guardar en BD: "/portadas/libro_17_google.jpg"
        return $"/portadas/{fileName}";
    }
}