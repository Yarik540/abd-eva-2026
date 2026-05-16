using System.Text;
using System.Text.Json;

namespace abd.Services
{
    public class EmbeddingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public EmbeddingService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<float[]> GenerarEmbeddingAsync(string texto)
        {
            try
            {
                var apiKey = _config["HuggingFace:ApiKey"];
                var model = _config["HuggingFace:Model"];

                var client = _httpClientFactory.CreateClient();

                var json = JsonSerializer.Serialize(new
                {
                    inputs = $"Represent this sentence for searching relevant passages: {texto}"
                });

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://router.huggingface.co/hf-inference/models/{model}"
                );

                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                request.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.SendAsync(request);

                var body = await response.Content.ReadAsStringAsync();

                Console.WriteLine("===== HUGGINGFACE RESPONSE =====");
                Console.WriteLine(body.Substring(0, Math.Min(300, body.Length)));

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(body);
                }

                var embedding = JsonSerializer.Deserialize<float[]>(body);

                if (embedding == null || embedding.Length == 0)
                {
                    throw new Exception("Embedding vacío");
                }

                Console.WriteLine($"Embedding generado: {embedding.Length} dimensiones");

                return embedding;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR EMBEDDING: {ex.Message}");
                throw;
            }
        }

        public double SimilitudCoseno(float[] a, float[] b)
        {
            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct /
                   (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}