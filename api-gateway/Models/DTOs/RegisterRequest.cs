namespace api_gateway.Models.DTOs
{
    public class RegisterRequest
    {
        public string Email { get; set; } = default!;

        public string Password { get; set; } = default!;

        public string FullName { get; set; } = default!;
    }
}
