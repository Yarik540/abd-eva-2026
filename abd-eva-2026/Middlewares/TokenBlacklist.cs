namespace abd.Middlewares
{
    public class TokenBlacklist
    {
        private readonly HashSet<string> _tokens = new();

        public void Add(string token) => _tokens.Add(token);
        public bool Contains(string token) => _tokens.Contains(token);
    }
}