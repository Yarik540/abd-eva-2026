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
                var apiKey = _config["OpenAI:ApiKey"];
                var model = _config["OpenAI:Model"];

                var client = _httpClientFactory.CreateClient();

                var requestBody = new { model = model, input = texto };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                sw.Stop();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(body);

                using var doc = JsonDocument.Parse(body);
                var embedding = doc.RootElement.GetProperty("data")[0].GetProperty("embedding")
                    .EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();

                Console.WriteLine($"Embedding generado exitosamente con OpenAI ({embedding.Length} dimensiones)");

                return embedding;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR EMBEDDING OPENAI: {ex.Message}");
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
