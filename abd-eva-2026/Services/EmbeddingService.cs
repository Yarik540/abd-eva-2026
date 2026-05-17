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
            var apiKey = _config["OpenAI:ApiKey"];
            var model = _config["OpenAI:Model"]; // 👈 IMPORTANTE

            var client = _httpClientFactory.CreateClient();

            var requestBody = new
            {
                model = model,
                input = texto
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/embeddings"
            );

            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Console.WriteLine("===== OPENAI RESPONSE =====");
            Console.WriteLine(body.Substring(0, Math.Min(300, body.Length)));

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            using var doc = JsonDocument.Parse(body);

            var embedding = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(x => x.GetDouble())   // 👈 FIX IMPORTANTE
                .Select(x => (float)x)
                .ToArray();

            Console.WriteLine($"Embedding generado: {embedding.Length} dimensiones");

            // OpenAI embeddings-3-small = 1536
            if (embedding.Length != 1536)
                throw new Exception($"Dimensión incorrecta: {embedding.Length}");

            return embedding;
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