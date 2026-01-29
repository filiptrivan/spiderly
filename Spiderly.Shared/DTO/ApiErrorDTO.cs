namespace Spiderly.Shared.DTO
{
    public class ApiErrorDTO
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
    }
}